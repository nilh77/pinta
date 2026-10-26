"use client";

import { useSyncExternalStore } from "react";

/** Chromium's install event (not in the DOM typings). */
export type InstallPromptEvent = Event & { prompt: () => Promise<void>; userChoice: Promise<{ outcome: "accepted" | "dismissed" }> };

const DISMISSED_KEY = "pinta.installDismissed";

let started = false;
let deferred: InstallPromptEvent | null = null;
const listeners = new Set<() => void>();
const notify = () => listeners.forEach((listener) => listener());

/**
 * Registers the service worker in production builds and keeps the browser's install event, which fires on any page,
 * for the invitation on screen 8 (docs/screens.md, PWA).
 */
export function startPwa() {
  if (typeof window === "undefined" || started) return;
  started = true;
  window.addEventListener("beforeinstallprompt", (event) => {
    event.preventDefault();
    deferred = event as InstallPromptEvent;
    notify();
  });
  window.addEventListener("appinstalled", () => {
    deferred = null;
    notify();
  });
  if (process.env.NODE_ENV === "production" && "serviceWorker" in navigator) {
    navigator.serviceWorker.register("/sw.js").catch(() => undefined);
  }
}

export function useInstallPrompt() {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => deferred,
    () => null,
  );
}

export async function promptInstall() {
  const event = deferred;
  if (!event) return;
  deferred = null;
  notify();
  await event.prompt();
}

/** iPhone and iPad Safari have no install event: the invitation explains «Partilhar › Adicionar ao ecrã principal». */
export function isIosSafari(userAgent: string) {
  const ios = /iPhone|iPad|iPod/.test(userAgent) || (/Macintosh/.test(userAgent) && /Mobile\//.test(userAgent));
  return ios && /Safari\//.test(userAgent) && !/CriOS|FxiOS|EdgiOS/.test(userAgent);
}

export type InstallInvite = "button" | "ios" | null;

/** What screen 8 offers: nothing once installed or dismissed; a button where the browser can install; a hint on iOS. */
export function installInvite({ canPrompt, userAgent, standalone, dismissed }: { canPrompt: boolean; userAgent: string; standalone: boolean; dismissed: boolean }): InstallInvite {
  if (standalone || dismissed) return null;
  if (canPrompt) return "button";
  return isIosSafari(userAgent) ? "ios" : null;
}

export function readDismissed() {
  try {
    return window.localStorage.getItem(DISMISSED_KEY) === "1";
  } catch {
    return false;
  }
}

export function saveDismissed() {
  try {
    window.localStorage.setItem(DISMISSED_KEY, "1");
  } catch {
    // Private mode: the invitation just comes back next time.
  }
}

export function isStandalone() {
  return window.matchMedia("(display-mode: standalone)").matches || (navigator as Navigator & { standalone?: boolean }).standalone === true;
}
