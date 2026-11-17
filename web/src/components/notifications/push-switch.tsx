"use client";

import { BellRing } from "lucide-react";
import { useTranslations } from "next-intl";
import { useId } from "react";
import { usePush } from "@/lib/push";
import { cn } from "@/lib/utils";

/**
 * «Avisos en este dispositivo» (docs/screens.md, «Avisos»): asks the browser only when turned on. Hidden while the
 * server has no push keys; otherwise it explains why it cannot be turned on (unsupported, blocked, iPhone not installed).
 */
export function PushSwitch({ compact = false }: { compact?: boolean }) {
  const t = useTranslations("notifications");
  const { status, busy, failed, enable, disable } = usePush();
  const id = useId();

  if (status === "loading" || status === "unavailable") return null;
  const on = status === "on";
  const canToggle = status === "on" || status === "off";
  const hint =
    status === "blocked" ? t("pushBlocked")
    : status === "unsupported" ? t("pushUnsupported")
    : status === "ios-install" ? t("pushInstall")
    : failed ? t("pushError")
    : on ? t("pushOn")
    : t("pushOff");

  return (
    <div className={cn("flex items-start gap-3", compact ? "px-3 py-2.5" : "rounded-2xl border border-border bg-card p-4")}>
      <BellRing aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-primary" />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <label htmlFor={id} className="text-sm font-semibold">
          {t("push")}
        </label>
        <p id={`${id}-hint`} className={cn("text-xs", failed || status === "blocked" ? "text-destructive" : "text-muted-foreground")}>
          {hint}
        </p>
      </div>
      {canToggle && (
        <button
          id={id}
          type="button"
          role="switch"
          aria-checked={on}
          aria-describedby={`${id}-hint`}
          disabled={busy}
          onClick={() => (on ? disable() : enable())}
          className={cn(
            "relative mt-0.5 h-6 w-10 shrink-0 rounded-full outline-none transition-colors focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:opacity-60",
            on ? "bg-primary" : "bg-input",
          )}
        >
          <span
            aria-hidden="true"
            className={cn("absolute top-0.5 left-0.5 size-5 rounded-full bg-card shadow transition-transform motion-reduce:transition-none", on && "translate-x-4")}
          />
        </button>
      )}
    </div>
  );
}
