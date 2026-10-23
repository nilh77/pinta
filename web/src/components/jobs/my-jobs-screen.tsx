"use client";

import { ChevronRight, Loader2, Plus } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import type { ClientJobListItem } from "@/lib/jobs/api";
import { useMyJobs } from "@/lib/jobs/use-client-jobs";
import { formatArea, formatCents, formatDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { JobProgress } from "./job-progress";
import { JobStatusBadge } from "./job-status-badge";
import { PriceRange } from "@/components/quote/price-range";

/** Screen 9 · Mis trabajos (board MisTrabajos) [C5.1]. */
export function MyJobsScreen() {
  const t = useTranslations("myJobs");
  const [tab, setTab] = useState<"active" | "history">("active");
  const jobs = useMyJobs(tab);
  const items = jobs.data?.items ?? [];

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
        <Button asChild size="sm">
          <Link href="/cotizar">
            <Plus aria-hidden="true" />
            {t("new")}
          </Link>
        </Button>
      </div>

      <div role="tablist" aria-label={t("tabs.label")} className="grid grid-cols-2 gap-1 rounded-2xl border border-border bg-card p-1 sm:w-80">
        {(["active", "history"] as const).map((key) => (
          <button
            key={key}
            role="tab"
            type="button"
            aria-selected={tab === key}
            onClick={() => setTab(key)}
            className="h-11 rounded-xl text-sm font-medium text-muted-foreground aria-selected:bg-primary aria-selected:font-semibold aria-selected:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            {t(`tabs.${key}`)}
          </button>
        ))}
      </div>

      <div role="tabpanel" aria-busy={jobs.isFetching}>
        {jobs.isPending ? (
          <p role="status" className="flex items-center gap-2 py-10 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
        ) : jobs.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3 py-10">
            <p className="text-destructive">{t("error")}</p>
            <Button variant="outline" onClick={() => jobs.refetch()}>
              {t("retry")}
            </Button>
          </div>
        ) : items.length === 0 ? (
          <EmptyState history={tab === "history"} />
        ) : (
          <ul className="flex flex-col gap-3">
            {items.map((job) => (
              <li key={job.id}>
                <JobCard job={job} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function EmptyState({ history }: { history: boolean }) {
  const t = useTranslations("myJobs.empty");
  return (
    <div className="flex flex-col items-center gap-3 rounded-[20px] border border-dashed border-border bg-card px-6 py-12 text-center">
      <p className="text-lg font-bold">{t(history ? "historyTitle" : "title")}</p>
      {!history && (
        <>
          <p className="text-sm text-muted-foreground">{t("body")}</p>
          <Button asChild>
            <Link href="/cotizar">{t("cta")}</Link>
          </Button>
        </>
      )}
    </div>
  );
}

/** Code, locality, m², status and the next step ("Paga la señal antes del 6 nov"). */
function JobCard({ job }: { job: ClientJobListItem }) {
  const t = useTranslations("myJobs.card");
  const locale = useLocale();
  const next =
    job.status === "quoted" && job.quoteValidUntil
      ? t("decideBy", { date: formatDate(locale, job.quoteValidUntil, true) })
      : job.status === "site_visit" && job.siteVisitAt
        ? t("visitOn", { date: formatDate(locale, job.siteVisitAt, true) })
        : job.status === "pending_email"
          ? t("confirmEmail")
          : job.status === "in_review"
            ? t("inReview")
            : job.status === "scheduled" && job.scheduledOn
              ? t("startsOn", { date: formatDate(locale, job.scheduledOn, true) })
              : job.status === "finished"
                ? t("payBalance")
                : null;

  return (
    <Link
      href={`/mis-trabajos/${job.id}`}
      className={cn(
        "flex flex-col gap-3 rounded-[20px] border bg-card p-4 transition-colors hover:border-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none",
        job.needsAction ? "border-accent shadow-[0_0_0_3px_var(--accent-soft)]" : "border-border",
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[13px] font-semibold text-muted-foreground">{job.code}</p>
          <p className="truncate text-base font-bold">
            {job.locality} · {formatArea(locale, Math.round(job.areaM2 * 100))}
          </p>
        </div>
        <JobStatusBadge status={job.status} />
      </div>
      <JobProgress status={job.status} compact />
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="font-semibold">
          {job.totalCents != null ? (
            formatCents(locale, job.totalCents)
          ) : job.rangeMinCents != null && job.rangeMaxCents != null ? (
            <PriceRange minCents={job.rangeMinCents} maxCents={job.rangeMaxCents} />
          ) : (
            t("customQuote")
          )}
        </span>
        {job.needsAction ? (
          <span className="inline-flex items-center gap-1 rounded-xl bg-accent px-3 py-2 text-sm font-semibold text-accent-foreground">
            {t(job.status === "finished" ? "pay" : "review")}
            <ChevronRight aria-hidden="true" className="size-4" />
          </span>
        ) : (
          next && <span className="text-muted-foreground">{next}</span>
        )}
      </div>
      {job.needsAction && next && <p className="-mt-1 text-xs text-accent-ink">{next}</p>}
    </Link>
  );
}
