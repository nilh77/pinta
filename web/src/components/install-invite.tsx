"use client";

import { Download, Share, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState, useSyncExternalStore } from "react";
import { Button } from "@/components/ui/button";
import { installInvite, isStandalone, promptInstall, readDismissed, saveDismissed, useInstallPrompt } from "@/lib/pwa/install";

/** Screen 8: invitation to install Pinta after sending a request (docs/screens.md, PWA). */
export function InstallInvite() {
  const t = useTranslations("pwa.install");
  const prompt = useInstallPrompt();
  // Browser-only facts: none on the server render, so hydration matches.
  const mounted = useSyncExternalStore(subscribeNothing, () => true, () => false);
  const [dismissed, setDismissed] = useState(false);

  const kind = mounted
    ? installInvite({ canPrompt: !!prompt, userAgent: navigator.userAgent, standalone: isStandalone(), dismissed: dismissed || readDismissed() })
    : null;
  if (!kind) return null;

  const dismiss = () => {
    saveDismissed();
    setDismissed(true);
  };

  return (
    <aside aria-labelledby="install-title" className="relative mt-4 rounded-[18px] border border-border bg-card p-4 pr-12">
      <h2 id="install-title" className="text-sm font-semibold">
        {t("title")}
      </h2>
      {kind === "button" ? (
        <>
          <p className="mt-1 text-[13px] text-muted-foreground">{t("body")}</p>
          <Button size="sm" className="mt-3" onClick={() => promptInstall().then(dismiss)}>
            <Download aria-hidden="true" />
            {t("button")}
          </Button>
        </>
      ) : (
        <p className="mt-1 text-[13px] text-muted-foreground">
          {t.rich("ios", { share: () => <Share aria-label={t("share")} className="inline size-4 align-text-bottom" /> })}
        </p>
      )}
      <button
        type="button"
        onClick={dismiss}
        aria-label={t("dismiss")}
        className="absolute top-2 right-2 flex size-11 items-center justify-center rounded-xl text-muted-foreground hover:bg-muted focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
      >
        <X aria-hidden="true" className="size-4" />
      </button>
    </aside>
  );
}

const subscribeNothing = () => () => {};
