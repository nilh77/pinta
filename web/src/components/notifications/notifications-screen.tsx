"use client";

import { useLocale, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { CardListSkeleton } from "@/components/ui/skeleton";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { signedOut, useMe } from "@/lib/auth/use-auth";
import { kindKey, notificationHref, textValues, timeAgo } from "@/lib/notifications/notices";
import { useMarkAllRead, useMarkRead, useNotifications } from "@/lib/notifications/use-notifications";
import { cn } from "@/lib/utils";
import { PushSwitch } from "./push-switch";

const PAGE_SIZE = 25;

/** «Avisos» (docs/screens.md): every notice of the signed-in user, paged, for the three roles. */
export function NotificationsScreen() {
  const t = useTranslations("notifications");
  const locale = useLocale();
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();
  const [page, setPage] = useState(1);
  const list = useNotifications(page, PAGE_SIZE, !!me.data);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  useEffect(() => {
    if (me.data === null) router.replace(signedOut() ? "/" : `/entrar?next=${encodeURIComponent(pathname)}`);
  }, [me.data, pathname, router]);

  const pages = list.data ? Math.max(1, Math.ceil(list.data.total / PAGE_SIZE)) : 1;
  const anyUnread = list.data?.items.some((n) => !n.readAt);

  return (
    <div className="flex min-h-dvh flex-col">
      <SiteHeader />
      <main id="main" className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-5 px-4 pt-2 pb-16 sm:px-6">
        <header className="flex flex-wrap items-end justify-between gap-3">
          <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
          {anyUnread && (
            <Button variant="outline" size="sm" pending={markAll.isPending} onClick={() => markAll.mutate()}>
              {t("markAll")}
            </Button>
          )}
        </header>
        {me.data && <PushSwitch />}
        {!me.data || list.isPending ? (
          <CardListSkeleton label={t("loading")} />
        ) : list.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3">
            <p className="text-destructive">{t("loadError")}</p>
            <Button variant="outline" onClick={() => list.refetch()}>
              {t("retry")}
            </Button>
          </div>
        ) : list.data.items.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-border bg-card p-6 text-center text-muted-foreground">{t("empty")}</p>
        ) : (
          <>
            <ul className="flex flex-col gap-2">
              {list.data.items.map((n) => {
                const unread = !n.readAt;
                return (
                  <li key={n.id}>
                    <Link
                      href={notificationHref(n, me.data!.role)}
                      onClick={() => unread && markRead.mutate(n.id)}
                      className={cn(
                        "flex items-start gap-3 rounded-2xl border bg-card p-4 outline-none hover:border-primary focus-visible:ring-[3px] focus-visible:ring-ring/50",
                        unread ? "border-primary/40" : "border-border",
                      )}
                    >
                      <span aria-hidden="true" className={cn("mt-1.5 size-2 shrink-0 rounded-full", unread ? "bg-primary" : "bg-transparent")} />
                      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                        <span className={cn("leading-snug", unread ? "font-semibold" : "text-muted-foreground")}>
                          {t(`kinds.${kindKey(n.kind)}` as never, textValues(n) as never)}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          {unread && <span className="sr-only">{t("unread")}, </span>}
                          <time dateTime={n.createdAt}>{timeAgo(locale, n.createdAt)}</time>
                        </span>
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
            {pages > 1 && (
              <nav aria-label={t("title")} className="flex items-center justify-between gap-3 text-sm">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  {t("previous")}
                </Button>
                <span>{t("page", { page, pages })}</span>
                <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>
                  {t("next")}
                </Button>
              </nav>
            )}
          </>
        )}
      </main>
    </div>
  );
}
