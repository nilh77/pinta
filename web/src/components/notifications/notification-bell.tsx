"use client";

import { Bell } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { DropdownMenu } from "radix-ui";
import { useState } from "react";
import { MorphMenuContent, morphMenuItemClass } from "@/components/ui/dropdown-menu";
import { Link, useRouter } from "@/i18n/navigation";
import { useMe } from "@/lib/auth/use-auth";
import { badgeText, kindKey, notificationHref, textValues, timeAgo, type NotificationDto, type Role } from "@/lib/notifications/notices";
import { useMarkAllRead, useMarkRead, useNotifications, useUnreadCount } from "@/lib/notifications/use-notifications";
import { cn } from "@/lib/utils";
import { controlClass, panelHeadClass } from "../header-controls";
import { PushSwitch } from "./push-switch";

/**
 * The bell in the header (docs/screens.md, «Avisos»), only with a session: the unread number on a dot, and a panel
 * with the last 20 notices, «Marcar todo como leído», «Ver todos» and this device's push switch.
 */
export function NotificationBell() {
  const t = useTranslations("notifications");
  const me = useMe();
  const unread = useUnreadCount();
  const [open, setOpen] = useState(false);
  const list = useNotifications(1, 20, open);
  const markAll = useMarkAllRead();

  if (!me.data) return null;
  const count = unread.data ?? 0;

  return (
    <DropdownMenu.Root modal={false} open={open} onOpenChange={setOpen}>
      <DropdownMenu.Trigger data-morph-trigger aria-label={t("bell", { count })} className={cn(controlClass, "relative")}>
        <Bell aria-hidden="true" className="size-4" />
        {count > 0 && (
          <span
            aria-hidden="true"
            className="absolute -top-0.5 -right-0.5 grid h-[18px] min-w-[18px] place-items-center rounded-full bg-accent px-1 text-[11px] leading-none font-bold text-accent-foreground tabular-nums"
          >
            {badgeText(count)}
          </span>
        )}
      </DropdownMenu.Trigger>
      <MorphMenuContent className="w-[min(calc(100vw-32px),360px)]">
        <div className={cn(panelHeadClass, "flex-row items-center justify-between gap-2")}>
          <span className="text-sm font-semibold">{t("title")}</span>
          {count > 0 && (
            <button
              type="button"
              onClick={() => markAll.mutate()}
              className="rounded-lg px-1.5 py-1 text-xs font-semibold text-primary outline-none hover:underline focus-visible:ring-[3px] focus-visible:ring-ring/50"
            >
              {t("markAll")}
            </button>
          )}
        </div>
        <DropdownMenu.Separator className="mx-1.5 my-1 h-px bg-glass-hairline" />
        <div className="max-h-[min(60vh,420px)] overflow-y-auto">
          {list.isPending ? (
            <div role="status" aria-label={t("loading")} className="flex flex-col gap-2 px-3 py-3">
              {[0, 1, 2].map((i) => (
                <span key={i} className="h-10 animate-pulse rounded-xl bg-primary-soft motion-reduce:animate-none" />
              ))}
            </div>
          ) : list.isError ? (
            <div role="alert" className="flex items-center justify-between gap-2 px-3 py-3 text-sm">
              <span className="text-destructive">{t("loadError")}</span>
              <button type="button" onClick={() => list.refetch()} className="font-semibold text-primary">
                {t("retry")}
              </button>
            </div>
          ) : list.data.items.length === 0 ? (
            <p className="px-3 py-6 text-center text-sm text-muted-foreground">{t("empty")}</p>
          ) : (
            list.data.items.map((n) => <NoticeItem key={n.id} notice={n} role={me.data!.role} onDone={() => setOpen(false)} />)
          )}
        </div>
        <DropdownMenu.Separator className="mx-1.5 my-1 h-px bg-glass-hairline" />
        <DropdownMenu.Item asChild className={cn(morphMenuItemClass, "justify-center text-primary")}>
          <Link href="/avisos">{t("seeAll")}</Link>
        </DropdownMenu.Item>
        <PushSwitch compact />
      </MorphMenuContent>
    </DropdownMenu.Root>
  );
}

function NoticeItem({ notice, role, onDone }: { notice: NotificationDto; role: Role; onDone: () => void }) {
  const t = useTranslations("notifications");
  const locale = useLocale();
  const router = useRouter();
  const markRead = useMarkRead();
  const unread = !notice.readAt;

  return (
    <DropdownMenu.Item
      onSelect={() => {
        if (unread) markRead.mutate(notice.id);
        onDone();
        router.push(notificationHref(notice, role));
      }}
      className={cn(morphMenuItemClass, "min-h-14 items-start py-2.5")}
    >
      <span aria-hidden="true" className={cn("mt-1.5 size-2 shrink-0 rounded-full", unread ? "bg-primary" : "bg-transparent")} />
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className={cn("text-sm leading-snug", unread ? "font-semibold" : "font-normal text-muted-foreground")}>
          {t(`kinds.${kindKey(notice.kind)}` as never, textValues(notice) as never)}
        </span>
        <span className="text-xs text-muted-foreground">
          {unread && <span className="sr-only">{t("unread")}, </span>}
          <time dateTime={notice.createdAt}>{timeAgo(locale, notice.createdAt)}</time>
        </span>
      </span>
    </DropdownMenu.Item>
  );
}
