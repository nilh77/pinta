"use client";

import { CheckCircle2, Loader2 } from "lucide-react";
import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import { AdminJobView } from "@/components/admin/jobs/admin-job-view";
import { ReviewLightBadge } from "@/components/admin/jobs/review-light";
import { PriceRange } from "@/components/quote/price-range";
import { Button } from "@/components/ui/button";
import { usePathname, useRouter } from "@/i18n/navigation";
import type { ReviewItem } from "@/lib/jobs/api";
import { useReviewQueue } from "@/lib/jobs/use-admin-jobs";
import { formatArea, formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";

/** A2 · Revisión (board AdminRevision): the queue oldest first with its traffic light, and the selected request. */
export function ReviewScreen() {
  const t = useTranslations("admin.review");
  const queue = useReviewQueue();
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const panel = useRef<HTMLDivElement>(null);
  const items = queue.data?.inReview ?? [];
  const selected = params.get("job") ?? items[0]?.id ?? null;

  const select = (id: string) => {
    router.replace(`${pathname}?job=${id}`, { scroll: false });
    if (window.matchMedia("(max-width: 1023px)").matches) panel.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    if (selected) panel.current?.focus({ preventScroll: true });
  }, [selected]);

  return (
    <div className="flex flex-col gap-5">
      <header>
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>

      {queue.isPending ? (
        <p role="status" className="flex items-center gap-2 text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : queue.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3">
          <p className="text-destructive">{t("loadError")}</p>
          <Button variant="outline" onClick={() => queue.refetch()}>
            {t("retry")}
          </Button>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
          <div className="flex flex-col gap-5">
            <section aria-labelledby="queue-title">
              <h2 id="queue-title" className="mb-2 text-sm font-bold">
                {t("queue", { count: items.length })}
              </h2>
              {items.length === 0 ? (
                <p className="flex items-center gap-2 rounded-2xl border border-dashed border-border bg-card p-4 text-sm font-semibold text-primary">
                  <CheckCircle2 aria-hidden="true" className="size-5" />
                  {t("empty")}
                </p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {items.map((item) => (
                    <li key={item.id}>
                      <QueueItem item={item} selected={item.id === selected} onSelect={() => select(item.id)} />
                    </li>
                  ))}
                </ul>
              )}
            </section>
            {(queue.data?.pendingEmail.length ?? 0) > 0 && (
              <section aria-labelledby="pending-title">
                <h2 id="pending-title" className="mb-1 text-sm font-bold">
                  {t("pendingEmail", { count: queue.data!.pendingEmail.length })}
                </h2>
                <p className="mb-2 text-xs text-muted-foreground">{t("pendingEmailHint")}</p>
                <ul className="flex flex-col gap-2">
                  {queue.data!.pendingEmail.map((item) => (
                    <li key={item.id}>
                      <QueueItem item={item} selected={item.id === selected} onSelect={() => select(item.id)} />
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
          <div ref={panel} tabIndex={-1} className="min-w-0 scroll-mt-4 outline-none">
            {selected ? <AdminJobView key={selected} id={selected} layout="review" /> : null}
          </div>
        </div>
      )}
    </div>
  );
}

function QueueItem({ item, selected, onSelect }: { item: ReviewItem; selected: boolean; onSelect: () => void }) {
  const t = useTranslations("admin.review");
  const locale = useLocale();
  return (
    <button
      type="button"
      aria-current={selected ? "true" : undefined}
      onClick={onSelect}
      className={cn(
        "flex w-full flex-col gap-1.5 rounded-2xl border bg-card p-3 text-left transition-colors hover:border-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none",
        selected ? "border-primary shadow-[0_0_0_3px_var(--primary-soft)]" : "border-border",
      )}
    >
      <span className="flex items-center justify-between gap-2">
        <span className="text-sm font-bold">{item.code}</span>
        {item.light && <ReviewLightBadge light={item.light} dueOn={item.reviewDueOn} />}
      </span>
      <span className="truncate text-sm">
        {item.clientName} · {item.municipality}
      </span>
      <span className="flex flex-wrap justify-between gap-2 text-xs text-muted-foreground">
        <span>
          {formatArea(locale, Math.round(item.areaM2 * 100))} ·{" "}
          {item.rangeMinCents != null && item.rangeMaxCents != null ? <PriceRange minCents={item.rangeMinCents} maxCents={item.rangeMaxCents} /> : t("custom")}
        </span>
        <span>{formatDateTime(locale, item.createdAt)}</span>
      </span>
    </button>
  );
}
