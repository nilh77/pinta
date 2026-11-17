"use client";

import { BellRing, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState, useSyncExternalStore } from "react";
import { Button } from "@/components/ui/button";
import { useMe } from "@/lib/auth/use-auth";
import { usePush, type PushStatus } from "@/lib/push";
import { useCoarsePointer } from "@/lib/use-coarse-pointer";

const DISMISSED_KEY = "pinta.push-invite.dismissed";
const QUIET_DAYS = 14;

/** Whether to show the invitation (docs/screens.md, «Avisos», «Invitación»). */
export function shouldInvite(args: { signedIn: boolean; touch: boolean; status: PushStatus; dismissedAt: number | null; now: number }) {
  const quiet = args.dismissedAt !== null && args.now - args.dismissedAt < QUIET_DAYS * 24 * 60 * 60 * 1000;
  return args.signedIn && args.touch && args.status === "off" && !quiet;
}

function readDismissed(): number | null {
  try {
    const value = Number(localStorage.getItem(DISMISSED_KEY));
    return value > 0 ? value : null;
  } catch {
    return null;
  }
}

/**
 * «¿Te avisamos en este móvil?»: once per device, on touch screens with a session, while push can be turned on and
 * is not. The system's permission prompt only appears after a tap, so «Activar avisos» asks for it.
 */
export function PushInvite() {
  const me = useMe();
  const touch = useCoarsePointer();
  // The push state is read only for a signed-in touch device: no API call for visitors.
  return me.data && touch ? <Invitation role={me.data.role} /> : null;
}

function Invitation({ role }: { role: "client" | "painter" | "admin" }) {
  const t = useTranslations("notifications.invite");
  const push = usePush();
  const dismissedAt = useSyncExternalStore(() => () => {}, readDismissed, () => Date.now());
  const [closed, setClosed] = useState(false);
  const [openedAt] = useState(() => Date.now());

  if (closed || !shouldInvite({ signedIn: true, touch: true, status: push.status, dismissedAt, now: openedAt })) return null;

  const later = () => {
    try {
      localStorage.setItem(DISMISSED_KEY, String(Date.now()));
    } catch {
      // Private mode: it simply shows again next time.
    }
    setClosed(true);
  };

  return (
    <aside
      aria-labelledby="push-invite-title"
      className="fixed inset-x-3 bottom-[calc(1rem+env(safe-area-inset-bottom))] z-40 mx-auto flex max-w-md flex-col gap-3 rounded-[20px] border border-border bg-card p-4 shadow-lg"
    >
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-full bg-primary-soft text-primary">
          <BellRing aria-hidden="true" className="size-5" />
        </span>
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <p id="push-invite-title" className="font-bold">
            {t("title")}
          </p>
          <p className="text-sm text-muted-foreground">{t(`body.${role}`)}</p>
        </div>
        <button type="button" onClick={later} aria-label={t("later")} className="grid size-8 place-items-center rounded-full text-muted-foreground hover:bg-primary-soft">
          <X aria-hidden="true" className="size-4" />
        </button>
      </div>
      {push.failed && <p className="text-sm text-destructive">{t("error")}</p>}
      <div className="flex gap-2">
        {/* Turned on or blocked, the status changes and the invitation goes by itself. */}
        <Button className="flex-1" pending={push.busy} onClick={() => push.enable()}>
          {t("enable")}
        </Button>
        <Button variant="outline" onClick={later}>
          {t("later")}
        </Button>
      </div>
    </aside>
  );
}
