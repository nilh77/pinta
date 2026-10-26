"use client";

import { useQueryClient } from "@tanstack/react-query";
import { WifiOff } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { AlertDialog } from "radix-ui";
import { Button } from "@/components/ui/button";
import { usePathname, useRouter } from "@/i18n/navigation";
import { onSessionExpired } from "@/lib/api/client";
import { meQueryKey } from "@/lib/auth/use-auth";

/** «Sin conexión»: a band at the top while the device is offline (docs/screens.md, global states). */
export function OfflineBanner() {
  const t = useTranslations("globalStates.offline");
  const online = useSyncExternalStore(subscribeOnline, () => navigator.onLine, () => true);
  if (online) return null;
  return (
    <p role="status" className="sticky top-0 z-30 flex items-center justify-center gap-2 bg-foreground px-4 py-2 text-center text-sm font-semibold text-background">
      <WifiOff aria-hidden="true" className="size-4 shrink-0" />
      {t("message")}
    </p>
  );
}

function subscribeOnline(listener: () => void) {
  window.addEventListener("online", listener);
  window.addEventListener("offline", listener);
  return () => {
    window.removeEventListener("online", listener);
    window.removeEventListener("offline", listener);
  };
}

/** «Tu sesión ha caducado» with «Entrar», back to the same page; the local quote draft stays (docs/screens.md). */
export function SessionExpiredDialog() {
  const t = useTranslations("globalStates.sessionExpired");
  // The page where it expired: protected areas may already have moved to «Entrar» by the time the user answers.
  const [from, setFrom] = useState<string | null>(null);
  const queryClient = useQueryClient();
  const router = useRouter();
  const pathname = usePathname();
  const current = useRef(pathname);
  useEffect(() => {
    current.current = pathname;
  }, [pathname]);

  useEffect(
    () =>
      onSessionExpired(() => {
        setFrom(current.current);
        queryClient.setQueryData(meQueryKey, null);
      }),
    [queryClient],
  );

  return (
    <AlertDialog.Root open={from !== null} onOpenChange={(open) => !open && setFrom(null)}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-40 bg-foreground/40" />
        <AlertDialog.Content className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl">
          <AlertDialog.Title className="font-display text-xl font-bold">{t("title")}</AlertDialog.Title>
          <AlertDialog.Description className="mt-2 text-sm text-muted-foreground">{t("body")}</AlertDialog.Description>
          <div className="mt-6 flex justify-end">
            <AlertDialog.Action asChild>
              <Button onClick={() => router.push(`/entrar?next=${encodeURIComponent(from ?? "/")}`)}>{t("signIn")}</Button>
            </AlertDialog.Action>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}

const COOKIE_NOTICE = "pinta_cookie_notice";

/** Only technical cookies in the MVP: an informative notice, shown until closed (docs/screens.md, global states). */
export function CookieNotice() {
  const t = useTranslations("globalStates.cookies");
  const seen = useSyncExternalStore(subscribeNothing, () => document.cookie.split("; ").some((c) => c === `${COOKIE_NOTICE}=1`), () => true);
  const [closed, setClosed] = useState(false);
  if (seen || closed) return null;

  const close = () => {
    document.cookie = `${COOKIE_NOTICE}=1; path=/; max-age=${60 * 60 * 24 * 365}; samesite=lax`;
    setClosed(true);
  };

  return (
    <aside
      aria-label={t("label")}
      className="fixed inset-x-3 bottom-[calc(5.5rem+env(safe-area-inset-bottom))] z-30 mx-auto flex max-w-xl items-center gap-3 rounded-2xl border border-border bg-card p-3 pl-4 text-sm shadow-lg lg:bottom-4"
    >
      <p className="flex-1 text-muted-foreground">{t("message")}</p>
      <Button size="sm" variant="outline" onClick={close}>
        {t("ok")}
      </Button>
    </aside>
  );
}

const subscribeNothing = () => () => {};
