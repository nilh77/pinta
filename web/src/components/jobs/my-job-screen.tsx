"use client";

import { ArrowLeft, BadgeCheck, CalendarClock, CircleAlert, CircleCheck, Clock, Loader2, MessageCircleQuestion } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";
import { toast } from "sonner";
import { PriceRange } from "@/components/quote/price-range";
import { Button } from "@/components/ui/button";
import { FormDialog, TextAreaField } from "@/components/ui/form-dialog";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import type { ClientJobDetail } from "@/lib/jobs/api";
import { useAskQuestion, useCancelJob, useCancellationPreview, useMyJob, useRejectQuote, useRequote } from "@/lib/jobs/use-client-jobs";
import { useJobPayments, type Payment } from "@/lib/payments/use-payments";
import { PaymentsList } from "@/components/payments/payments-list";
import { daysUntil, formatArea, formatCents, formatDate, formatDateTime } from "@/lib/format";
import { JobProgress } from "./job-progress";
import { JobStatusBadge } from "./job-status-badge";

/** Screen 10 · Detalle del trabajo (board Trabajo) [C5.2, C5.6, C5.7]. Payment arrives in H5. */
export function MyJobScreen({ id }: { id: string }) {
  const t = useTranslations("myJobs.detail");
  const locale = useLocale();
  const job = useMyJob(id);
  const payments = useJobPayments(id);

  if (job.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (job.isError) {
    const notFound = job.error instanceof ApiError && job.error.code === "not_found";
    return (
      <div role="alert" className="flex flex-col items-start gap-4 py-16">
        <p className="text-lg font-semibold">{t(notFound ? "notFound" : "error")}</p>
        <div className="flex gap-2">
          {!notFound && <Button onClick={() => job.refetch()}>{t("retry")}</Button>}
          <Button asChild variant="outline">
            <Link href="/mis-trabajos">{t("back")}</Link>
          </Button>
        </div>
      </div>
    );
  }

  const data = job.data;
  return (
    <div className="flex flex-col gap-5">
      <Link href="/mis-trabajos" className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("back")}
      </Link>

      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-[13px] font-semibold text-muted-foreground">{data.code}</p>
          <JobStatusBadge status={data.status} />
        </div>
        <h1 className="text-[26px] leading-tight font-extrabold tracking-tight">
          {data.addressLine1}, {data.locality}
        </h1>
        <JobProgress status={data.status} />
      </header>

      <StatusSection job={data} payments={payments.data ?? []} />

      {data.quote && data.status !== "quoted" && <QuoteCard job={data} />}

      {(payments.data?.length ?? 0) > 0 && (
        <section aria-labelledby="payments-title">
          <h2 id="payments-title" className="mb-3 text-[13px] font-semibold">
            {t("payments")}
          </h2>
          <PaymentsList jobId={data.id} payments={payments.data!} emptyText="" />
        </section>
      )}

      <Timeline job={data} />

      <div className="flex flex-col gap-2 border-t border-border pt-5 sm:flex-row">
        {data.canRequote && <RequoteButton id={data.id} />}
        {data.canCancel && <CancelButton job={data} />}
      </div>
      <p className="text-xs text-muted-foreground">{t("createdAt", { date: formatDate(locale, data.createdAt) })}</p>
    </div>
  );
}

/** What the client sees in each state (screens.md, screen 10). */
function StatusSection({ job, payments }: { job: ClientJobDetail; payments: Payment[] }) {
  const t = useTranslations("myJobs.detail");
  const locale = useLocale();
  const estimate =
    job.rangeMinCents != null && job.rangeMaxCents != null ? <PriceRange minCents={job.rangeMinCents} maxCents={job.rangeMaxCents} /> : t("customQuote");
  const stopReason = [...job.timeline].reverse().find((entry) => entry.status === job.status)?.reason;

  switch (job.status) {
    case "pending_email":
      return <Notice icon={<CircleAlert aria-hidden="true" />} title={t("pendingEmail.title")} body={t("pendingEmail.body")} />;
    case "in_review":
      return (
        <Notice
          icon={<Clock aria-hidden="true" />}
          title={t("inReview.title")}
          body={job.reviewDueOn ? t("inReview.body", { date: formatDate(locale, job.reviewDueOn, true) }) : t("inReview.bodyNoDate")}
        >
          <p className="mt-3 text-sm">
            {t("estimate")}: <strong>{estimate}</strong> · {formatArea(locale, Math.round(job.areaM2 * 100))}
          </p>
        </Notice>
      );
    case "site_visit":
      return (
        <Notice icon={<CalendarClock aria-hidden="true" />} title={t("siteVisit.title")} body={t("siteVisit.body")}>
          {job.siteVisit && (
            <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt className="text-muted-foreground">{t("siteVisit.when")}</dt>
              <dd className="font-semibold">{formatDateTime(locale, job.siteVisit.at)}</dd>
              <dt className="text-muted-foreground">{t("siteVisit.who")}</dt>
              <dd className="font-semibold">{job.siteVisit.visitorName}</dd>
              <dt className="text-muted-foreground">{t("siteVisit.price")}</dt>
              <dd className="font-semibold">{job.siteVisit.priceCents === 0 ? t("siteVisit.free") : formatCents(locale, job.siteVisit.priceCents)}</dd>
            </dl>
          )}
          {job.siteVisit && job.siteVisit.priceCents > 0 && !payments.some((p) => p.type === "site_visit" && p.paidAt) && (
            <Button asChild className="mt-4">
              <Link href={`/mis-trabajos/${job.id}/pagar?tipo=site_visit`}>{t("siteVisit.pay")}</Link>
            </Button>
          )}
        </Notice>
      );
    case "quoted":
      return <QuotedSection job={job} payments={payments} />;
    case "deposit_paid":
    case "scheduled":
    case "in_progress":
      return (
        <Notice
          icon={<CircleCheck aria-hidden="true" />}
          title={t(`${job.status === "in_progress" ? "inProgress" : job.status === "scheduled" ? "scheduled" : "depositPaid"}.title`)}
          body={job.scheduledOn ? t("scheduled.body", { date: formatDate(locale, job.scheduledOn) }) : t("depositPaid.body")}
        >
          {job.painter && (
            <p className="mt-3 flex items-center gap-1.5 text-sm">
              {t("painter", { name: job.painter.firstName })}
              <span className="inline-flex items-center gap-1 rounded-full bg-primary-soft px-2 py-0.5 text-xs font-semibold text-primary">
                <BadgeCheck aria-hidden="true" className="size-3.5" />
                {t("verified")}
              </span>
            </p>
          )}
          {job.quote && <p className="mt-3 text-sm">{t("balanceDue", { amount: formatCents(locale, job.quote.balanceCents) })}</p>}
        </Notice>
      );
    case "finished":
      return (
        <Notice icon={<CircleCheck aria-hidden="true" />} title={t("finished.title")} body={t("finished.body")}>
          <ResultPhotos job={job} />
          {job.quote && (
            <Button asChild className="mt-4">
              <Link href={`/mis-trabajos/${job.id}/pagar?tipo=balance`}>{t("finished.pay", { amount: formatCents(locale, job.quote.balanceCents) })}</Link>
            </Button>
          )}
        </Notice>
      );
    case "closed":
      return (
        <Notice icon={<CircleCheck aria-hidden="true" />} title={t("closed.title")} body={t("closed.body")}>
          <ResultPhotos job={job} />
        </Notice>
      );
    case "expired":
      return <Notice icon={<Clock aria-hidden="true" />} title={t("expired.title")} body={t("expired.body")} />;
    case "rejected":
      return <Notice icon={<CircleAlert aria-hidden="true" />} title={t("rejected.title")} body={stopReason ?? t("rejected.body")} />;
    case "cancelled":
      return <Notice icon={<CircleAlert aria-hidden="true" />} title={t("cancelled.title")} body={stopReason ?? t("cancelled.body")} />;
    default:
      return null;
  }
}

/** C5.5: the painter's result photos, each opening full size. */
function ResultPhotos({ job }: { job: ClientJobDetail }) {
  const t = useTranslations("myJobs.detail");
  if (job.resultPhotos.length === 0) return null;
  return (
    <ul aria-label={t("resultPhotos")} className="mt-3 grid grid-cols-3 gap-2">
      {job.resultPhotos.map((photo, i) => (
        <li key={photo.id} className="aspect-square overflow-hidden rounded-xl bg-muted">
          <a href={photo.url} target="_blank" rel="noopener noreferrer">
            {/* eslint-disable-next-line @next/next/no-img-element -- signed storage URLs, not optimisable */}
            <img src={photo.url} alt={t("resultPhoto", { n: i + 1 })} className="size-full object-cover" loading="lazy" />
          </a>
        </li>
      ))}
    </ul>
  );
}

function Notice({ icon, title, body, children }: { icon: React.ReactNode; title: string; body: string; children?: React.ReactNode }) {
  return (
    <section className="rounded-[20px] border border-border bg-card p-4">
      <div className="flex gap-3">
        <span className="mt-0.5 text-primary [&_svg]:size-5">{icon}</span>
        <div className="min-w-0">
          <h2 className="font-bold">{title}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{body}</p>
          {children}
        </div>
      </div>
    </section>
  );
}

/** quoted: the closed price against the estimate, validity, deposit and the client's decisions (C5.2). */
function QuotedSection({ job, payments }: { job: ClientJobDetail; payments: Payment[] }) {
  const t = useTranslations("myJobs.detail.quoted");
  const locale = useLocale();
  const days = job.quoteValidUntil ? daysUntil(job.quoteValidUntil) : null;

  return (
    <section aria-labelledby="quoted-title" className="flex flex-col gap-4">
      <div className="rounded-[20px] border-2 border-accent bg-card p-4">
        <h2 id="quoted-title" className="text-lg font-bold">
          {t("title")}
        </h2>
        {job.quoteValidUntil && (
          <p className="mt-1 text-sm text-accent-ink">
            {t("validUntil", { date: formatDate(locale, job.quoteValidUntil) })}
            {days != null && days >= 0 && ` · ${t("daysLeft", { days })}`}
          </p>
        )}
      </div>
      <QuoteCard job={job} />
      <div className="flex flex-col gap-2">
        <Button asChild size="lg" className="w-full">
          <Link href={`/mis-trabajos/${job.id}/pagar?tipo=deposit`}>
            {payments.some((p) => p.type === "deposit" && p.status === "requires_action") ? t("seeMultibanco") : t("acceptAndPay")}
          </Link>
        </Button>
        <div className="grid gap-2 sm:grid-cols-2">
          <QuestionButton id={job.id} />
          <RejectButton id={job.id} />
        </div>
      </div>
      <p className="text-xs text-muted-foreground">{t("policy")}</p>
    </section>
  );
}

/** The client's breakdown (docs/pricing-engine.md 6.3): work line, extras, total, deposit and balance; VAT included. */
function QuoteCard({ job }: { job: ClientJobDetail }) {
  const t = useTranslations("myJobs.detail.quote");
  const locale = useLocale();
  const quote = job.quote!;

  return (
    <div className="rounded-[20px] border border-border bg-card p-4">
      <dl className="flex flex-col gap-2 text-sm">
        {quote.lines.map((line, i) => (
          <div key={i} className="flex justify-between gap-3">
            <dt>
              {line.kind === "work"
                ? t("work", { count: line.elementCount ?? 0, area: formatArea(locale, Math.round((line.areaM2 ?? 0) * 100)) })
                : line.description}
            </dt>
            <dd className="font-semibold whitespace-nowrap">{formatCents(locale, line.amountCents)}</dd>
          </div>
        ))}
        <div className="mt-1 flex items-baseline justify-between gap-3 border-t border-border pt-3">
          <dt className="font-bold">
            {t("total")} <span className="font-normal text-muted-foreground">{t("vatIncluded")}</span>
          </dt>
          <dd className="text-2xl font-extrabold">{formatCents(locale, quote.totalCents)}</dd>
        </div>
        {job.rangeMinCents != null && job.rangeMaxCents != null && (
          <div className="flex justify-between gap-3 text-muted-foreground">
            <dt>{t("estimate")}</dt>
            <dd>
              <PriceRange minCents={job.rangeMinCents} maxCents={job.rangeMaxCents} />
            </dd>
          </div>
        )}
        <div className="mt-2 grid grid-cols-2 gap-2">
          <div className="rounded-xl bg-primary-soft p-3">
            <dt className="text-xs text-muted-foreground">{t("deposit")}</dt>
            <dd className="font-bold">{formatCents(locale, quote.depositCents)}</dd>
          </div>
          <div className="rounded-xl bg-background p-3">
            <dt className="text-xs text-muted-foreground">{t("balance")}</dt>
            <dd className="font-bold">{formatCents(locale, quote.balanceCents)}</dd>
          </div>
        </div>
      </dl>
      {quote.note && (
        <blockquote className="mt-4 border-l-3 border-accent bg-accent-soft/50 px-3 py-2 text-sm">
          <p className="text-xs font-semibold text-accent-ink">{t("note")}</p>
          <p className="mt-1">{quote.note}</p>
        </blockquote>
      )}
    </div>
  );
}

function Timeline({ job }: { job: ClientJobDetail }) {
  const t = useTranslations("myJobs.detail");
  const tStatus = useTranslations("jobs.status");
  const locale = useLocale();
  return (
    <section aria-labelledby="timeline-title">
      <h2 id="timeline-title" className="mb-3 text-[13px] font-semibold">
        {t("timeline")}
      </h2>
      <ol className="flex flex-col gap-3 border-l-2 border-border pl-4">
        {job.timeline.map((entry, i) => (
          <li key={i} className="relative">
            <span aria-hidden="true" className="absolute top-1.5 -left-[21px] size-2.5 rounded-full bg-primary" />
            <p className="text-sm font-semibold">{tStatus(entry.status)}</p>
            <p className="text-xs text-muted-foreground">{formatDateTime(locale, entry.at)}</p>
            {entry.reason && <p className="mt-1 text-sm">{entry.reason}</p>}
          </li>
        ))}
      </ol>
    </section>
  );
}

function QuestionButton({ id }: { id: string }) {
  const t = useTranslations("myJobs.detail.question");
  const errorText = useErrorText();
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [touched, setTouched] = useState(false);
  const ask = useAskQuestion(id);

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <MessageCircleQuestion aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title")}
        description={t("body")}
        submitLabel={t("send")}
        cancelLabel={t("cancel")}
        pending={ask.isPending}
        onSubmit={() => {
          setTouched(true);
          if (!message.trim()) return;
          ask.mutate(message.trim(), {
            onSuccess: () => {
              setOpen(false);
              setMessage("");
              setTouched(false);
              toast.success(t("sent"));
            },
            onError: (error) => toast.error(errorText(error)),
          });
        }}
      >
        <TextAreaField
          id={fieldId}
          label={t("label")}
          value={message}
          onChange={setMessage}
          max={2000}
          required
          error={touched && !message.trim() ? t("required") : undefined}
        />
      </FormDialog>
    </>
  );
}

function RejectButton({ id }: { id: string }) {
  const t = useTranslations("myJobs.detail.reject");
  const errorText = useErrorText();
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const reject = useRejectQuote(id);

  return (
    <>
      <Button variant="ghost" className="text-destructive" onClick={() => setOpen(true)}>
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title")}
        description={t("body")}
        submitLabel={t("confirm")}
        cancelLabel={t("cancel")}
        destructive
        pending={reject.isPending}
        onSubmit={() =>
          reject.mutate(reason.trim() || null, {
            onSuccess: () => {
              setOpen(false);
              toast.success(t("done"));
            },
            onError: (error) => toast.error(errorText(error)),
          })
        }
      >
        <TextAreaField id={fieldId} label={t("reason")} hint={t("optional")} value={reason} onChange={setReason} max={500} />
      </FormDialog>
    </>
  );
}

/** C5.6: the refund under the policy before confirming; confirmed with a check box (screens.md). */
function CancelButton({ job }: { job: ClientJobDetail }) {
  const t = useTranslations("myJobs.detail.cancelJob");
  const errorText = useErrorText();
  const locale = useLocale();
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [sure, setSure] = useState(false);
  const preview = useCancellationPreview(job.id, open);
  const cancel = useCancelJob(job.id);

  return (
    <>
      <Button variant="ghost" className="text-destructive sm:ml-auto" onClick={() => setOpen(true)}>
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) setSure(false);
        }}
        title={t("title", { code: job.code })}
        description={
          preview.isPending ? (
            <span className="inline-flex items-center gap-2">
              <Loader2 aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
              {t("calculating")}
            </span>
          ) : preview.data && preview.data.depositPaidCents > 0 ? (
            t("refund", { amount: formatCents(locale, preview.data.refundCents) })
          ) : (
            t("noPayment")
          )
        }
        submitLabel={t("confirm")}
        cancelLabel={t("keep")}
        destructive
        submitDisabled={!sure}
        pending={cancel.isPending}
        onSubmit={() =>
          cancel.mutate(reason.trim() || null, {
            onSuccess: () => {
              setOpen(false);
              toast.success(t("done"));
            },
            onError: (error) => toast.error(errorText(error)),
          })
        }
      >
        <TextAreaField id={fieldId} label={t("reason")} hint={t("optional")} value={reason} onChange={setReason} max={500} />
        <label className="flex min-h-11 items-center gap-3 text-sm font-medium">
          <input type="checkbox" checked={sure} onChange={(e) => setSure(e.target.checked)} className="size-5 accent-[var(--destructive)]" />
          {t("sure")}
        </label>
      </FormDialog>
    </>
  );
}

function RequoteButton({ id }: { id: string }) {
  const t = useTranslations("myJobs.detail.requote");
  const errorText = useErrorText();
  const requote = useRequote(id);
  return (
    <Button
      disabled={requote.isPending}
      onClick={() =>
        requote.mutate(undefined, {
          onSuccess: () => toast.success(t("done")),
          onError: (error) => toast.error(errorText(error)),
        })
      }
    >
      {requote.isPending && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
      {t("open")}
    </Button>
  );
}
