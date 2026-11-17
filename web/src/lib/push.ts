"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { api } from "@/lib/api/client";
import { orThrow } from "@/lib/jobs/api";

/**
 * Web push on this device (docs/screens.md, «Avisos»; docs/integrations.md). Permission is asked only when the person
 * turns the switch on. The service worker exists only in production builds, so in `pnpm dev` push reads as unsupported.
 */
export type PushStatus = "loading" | "unavailable" | "unsupported" | "ios-install" | "blocked" | "off" | "on";

type Env = { userAgent: string; standalone: boolean; hasPush: boolean };

/** What this browser can do before asking anything: iPhone and iPad only receive push from the installed app. */
export function pushSupport({ userAgent, standalone, hasPush }: Env): "ok" | "unsupported" | "ios-install" {
  const ios = /iPad|iPhone|iPod/.test(userAgent);
  if (ios && !standalone) return "ios-install";
  return hasPush ? "ok" : "unsupported";
}

/** The VAPID public key (base64url) as the bytes `pushManager.subscribe` wants. */
export function keyBytes(base64url: string): Uint8Array<ArrayBuffer> {
  const base64 = (base64url + "=".repeat((4 - (base64url.length % 4)) % 4)).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base64);
  const bytes = new Uint8Array(new ArrayBuffer(raw.length));
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}

function environment(): Env {
  return {
    userAgent: navigator.userAgent,
    standalone: window.matchMedia("(display-mode: standalone)").matches || (navigator as { standalone?: boolean }).standalone === true,
    hasPush: "serviceWorker" in navigator && "PushManager" in window && "Notification" in window,
  };
}

async function registration() {
  return "serviceWorker" in navigator ? navigator.serviceWorker.getRegistration() : undefined;
}

async function publicKey(): Promise<string | null> {
  const result = await api.GET("/api/push/public-key");
  return result.response.ok && result.data ? result.data.publicKey : null;
}

/** This device's state, read without asking the person anything. */
async function readStatus(): Promise<PushStatus> {
  const support = pushSupport(environment());
  if (support !== "ok") return support;
  const [key, reg] = await Promise.all([publicKey(), registration()]);
  if (!key) return "unavailable";
  if (!reg) return "unsupported";
  if (Notification.permission === "denied") return "blocked";
  const subscription = await reg.pushManager.getSubscription();
  if (!subscription) return "off";
  // The browser keeps its subscription even when the server lost it (a demo reset empties the table, or another
  // account signed in on this device): register it again, which is idempotent. Without permission it is useless.
  if (Notification.permission !== "granted") return "off";
  await saveSubscription(subscription).catch(() => undefined);
  return "on";
}

async function saveSubscription(subscription: PushSubscription) {
  const { endpoint, keys } = subscription.toJSON();
  orThrow(await api.POST("/api/push/subscriptions", { body: { endpoint: endpoint!, p256dh: keys!.p256dh, auth: keys!.auth } }));
}

const PUSH_KEY = ["push-status"] as const;

export function usePush() {
  const queryClient = useQueryClient();
  const state = useQuery({ queryKey: PUSH_KEY, queryFn: readStatus, staleTime: Infinity, retry: false });
  const status: PushStatus = state.isError ? "unsupported" : (state.data ?? "loading");
  const setStatus = useCallback((next: PushStatus) => queryClient.setQueryData(PUSH_KEY, next), [queryClient]);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  const enable = useCallback(async () => {
    setBusy(true);
    setFailed(false);
    try {
      if ((await Notification.requestPermission()) !== "granted") return setStatus("blocked");
      const [key, reg] = await Promise.all([publicKey(), registration()]);
      if (!key || !reg) return setStatus("unsupported");
      const subscription =
        (await reg.pushManager.getSubscription()) ??
        (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) }));
      await saveSubscription(subscription);
      setStatus("on");
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }, [setStatus]);

  const disable = useCallback(async () => {
    setBusy(true);
    setFailed(false);
    try {
      const subscription = await (await registration())?.pushManager.getSubscription();
      if (subscription) {
        await api.DELETE("/api/push/subscriptions", { params: { query: { endpoint: subscription.endpoint } } });
        await subscription.unsubscribe();
      }
      setStatus("off");
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }, [setStatus]);

  return { status, busy, failed, enable, disable };
}
