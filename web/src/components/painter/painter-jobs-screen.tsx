"use client";

import { CalendarDays, Check, ChevronRight, Loader2, MapPin, X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { FormDialog, TextAreaField } from "@/components/ui/form-dialog";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatArea, formatCents, formatDate } from "@/lib/format";
import { useAcceptOffer, useDeclineOffer, usePainterJobs, type PainterJobItem } from "@/lib/painter/use-painter";

const TABS = ["offers", "upcoming", "inProgress", "done"] as const;
type Tab = (typeof TABS)[number];

/** P1 · Mis trabajos (board PintorTrabajos) [P1.1, P1.2]. */
export function PainterJobsScreen() {
  const t = useTranslations("painter.jobs");
  const jobs = usePainterJobs();
  const [chosen, setChosen] = useState<Tab | null>(null);
  // Offers first when there are any, as they wait on the painter's answer.
  const tab = chosen ?? (jobs.data && jobs.data.offers.length === 0 ? "upcoming" : "offers");
  const items = jobs.data?.[tab] ?? [];

  return (
    <div className="flex flex-col gap-5">
      <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>

      <div role="tablist" aria-label={t("tabs.label")} className="grid grid-cols-2 gap-1 rounded-2xl border border-border bg-card p-1 sm:grid-cols-4">
        {TABS.map((key) => {
          const count = jobs.data?.[key].length ?? 0;
          return (
            <button
              key={key}
              role="tab"
              type="button"
              aria-selected={tab === key}
              onClick={() => setChosen(key)}
              className="h-11 rounded-xl text-sm font-medium text-muted-foreground aria-selected:bg-primary aria-selected:font-semibold aria-selected:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              {t(`tabs.${key}`)}
              {count > 0 && <span className="ml-1.5 tabular-nums">({count})</span>}
            </button>
          );
        })}
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
          <div className="flex flex-col items-center gap-2 rounded-[20px] border border-dashed border-border bg-card px-6 py-12 text-center">
            <p className="text-lg font-bold">{t(`empty.${tab}`)}</p>
            {tab === "offers" && <p className="text-sm text-muted-foreground">{t("empty.offersBody")}</p>}
          </div>
        ) : (
          <ul className="flex flex-col gap-3">
            {items.map((job) => (
              <li key={job.assignmentId}>{tab === "offers" ? <OfferCard job={job} onAccepted={() => setChosen("upcoming")} /> : <JobCard job={job} />}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function Facts({ job }: { job: PainterJobItem }) {
  const t = useTranslations("painter.jobs");
  const locale = useLocale();
  return (
    <>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[13px] font-semibold text-muted-foreground">{job.code}</p>
          <p className="flex items-center gap-1.5 truncate text-base font-bold">
            <MapPin aria-hidden="true" className="size-4 shrink-0 text-primary" />
            {job.locality}, {job.municipality}
          </p>
        </div>
        <JobStatusBadge status={job.jobStatus} />
      </div>
      <dl className="grid grid-cols-3 gap-2 text-sm">
        <div>
          <dt className="text-xs text-muted-foreground">{t("date")}</dt>
          <dd className="flex items-center gap-1 font-semibold">
            <CalendarDays aria-hidden="true" className="size-3.5" />
            {formatDate(locale, job.scheduledOn, true)}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-muted-foreground">{t("area")}</dt>
          <dd className="font-semibold">{formatArea(locale, Math.round(job.areaM2 * 100))}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted-foreground">{t("pay")}</dt>
          <dd className="font-bold text-primary">{formatCents(locale, job.painterPayCents)}</dd>
        </div>
      </dl>
    </>
  );
}

/** P1.2: accept (the job is scheduled for the offer's date) or decline with a reason. */
function OfferCard({ job, onAccepted }: { job: PainterJobItem; onAccepted: () => void }) {
  const t = useTranslations("painter.jobs.offer");
  const locale = useLocale();
  const errorText = useErrorText();
  const accept = useAcceptOffer();
  const decline = useDeclineOffer();
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState("");

  const failed = (error: unknown) =>
    toast.error(error instanceof ApiError && error.code === "assignment_taken" ? t("taken") : errorText(error));

  return (
    <article className="flex flex-col gap-3 rounded-[20px] border border-accent bg-card p-4 shadow-[0_0_0_3px_var(--accent-soft)]">
      <Facts job={job} />
      <div className="grid grid-cols-2 gap-2">
        <Button variant="outline" disabled={accept.isPending || decline.isPending} onClick={() => setDeclining(true)}>
          <X aria-hidden="true" />
          {t("decline")}
        </Button>
        <Button
          disabled={accept.isPending || decline.isPending}
          onClick={() =>
            accept.mutate(job.assignmentId, {
              onSuccess: () => {
                toast.success(t("accepted", { date: formatDate(locale, job.scheduledOn) }));
                onAccepted();
              },
              onError: failed,
            })
          }
        >
          {accept.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Check aria-hidden="true" />}
          {t("accept")}
        </Button>
      </div>
      <FormDialog
        open={declining}
        onOpenChange={setDeclining}
        title={t("declineTitle", { code: job.code })}
        description={t("declineBody")}
        submitLabel={t("declineSubmit")}
        cancelLabel={t("cancel")}
        pending={decline.isPending}
        submitDisabled={reason.trim().length === 0}
        destructive
        onSubmit={() =>
          decline.mutate(
            { assignmentId: job.assignmentId, reason: reason.trim() },
            {
              onSuccess: () => {
                setDeclining(false);
                toast.success(t("declined"));
              },
              onError: (error) => {
                setDeclining(false);
                failed(error);
              },
            },
          )
        }
      >
        <TextAreaField id={`reason-${job.assignmentId}`} label={t("reason")} value={reason} onChange={setReason} max={500} required />
      </FormDialog>
    </article>
  );
}

function JobCard({ job }: { job: PainterJobItem }) {
  const t = useTranslations("painter.jobs");
  return (
    <Link
      href={`/pintor/trabajos/${job.jobId}`}
      className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4 transition-colors hover:border-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <Facts job={job} />
      <span className="inline-flex items-center gap-1 self-end text-sm font-semibold text-primary">
        {t("open")}
        <ChevronRight aria-hidden="true" className="size-4" />
      </span>
    </Link>
  );
}
