"use client";

import { ArrowLeft, Loader2, Mail, MapPin, Phone } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import type { AdminJobDetail } from "@/lib/jobs/api";
import { useAdminJob } from "@/lib/jobs/use-admin-jobs";
import { formatCents, formatDate, formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";
import { DraftEditor } from "./draft-editor";
import { JobActions } from "./job-actions";
import { JobFiles } from "./job-files";
import { ReviewLightBadge } from "./review-light";

const TABS = ["quote", "versions", "files", "history", "payments", "assignment"] as const;
type Tab = (typeof TABS)[number];

/**
 * One job for the admin (A2.2, A3.5): the review screen shows it stacked; /admin/trabajos/{id} adds the tabs
 * Cotización, Versiones, Archivos, Historial, Pagos (H5) and Asignación (H6).
 */
export function AdminJobView({ id, layout }: { id: string; layout: "review" | "tabs" }) {
  const t = useTranslations("admin.job");
  const job = useAdminJob(id);

  if (job.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-12 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (job.isError) {
    const notFound = job.error instanceof ApiError && job.error.code === "not_found";
    return (
      <div role="alert" className="flex flex-col items-start gap-3 py-12">
        <p className="font-semibold">{t(notFound ? "notFound" : "loadError")}</p>
        {!notFound && <Button onClick={() => job.refetch()}>{t("retry")}</Button>}
      </div>
    );
  }

  return layout === "review" ? <Stacked job={job.data} /> : <Tabbed job={job.data} />;
}

function Stacked({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job");
  return (
    <div className="flex flex-col gap-5">
      <Overview job={job} />
      <section aria-labelledby="files-title">
        <h3 id="files-title" className="mb-2 text-sm font-bold">
          {t("tabs.files")}
        </h3>
        <JobFiles files={job.files} />
      </section>
      <QuoteSection job={job} />
    </div>
  );
}

function Tabbed({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job");
  const [tab, setTab] = useState<Tab>("quote");
  return (
    <div className="flex flex-col gap-5">
      <Link href="/admin/trabajos" className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("backToJobs")}
      </Link>
      <Overview job={job} />
      <div role="tablist" aria-label={t("tabs.label")} className="flex gap-1 overflow-x-auto border-b border-border">
        {TABS.map((key) => (
          <button
            key={key}
            id={`tab-${key}`}
            role="tab"
            type="button"
            aria-selected={tab === key}
            aria-controls={`panel-${key}`}
            onClick={() => setTab(key)}
            className="-mb-px min-h-11 border-b-2 border-transparent px-3 text-sm font-semibold whitespace-nowrap text-muted-foreground aria-selected:border-primary aria-selected:text-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            {t(`tabs.${key}`)}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
        {tab === "quote" && <QuoteSection job={job} />}
        {tab === "versions" && <Versions job={job} />}
        {tab === "files" && <JobFiles files={job.files} />}
        {tab === "history" && <History job={job} />}
        {tab === "payments" && <p className="text-sm text-muted-foreground">{t("later.payments")}</p>}
        {tab === "assignment" && <p className="text-sm text-muted-foreground">{t("later.assignment")}</p>}
      </div>
    </div>
  );
}

/** Client, address, dates, description and deadlines (A2.2). */
function Overview({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job");
  const locale = useLocale();
  const dates =
    job.preferredDates.asap || !job.preferredDates.from || !job.preferredDates.to
      ? t("asap")
      : `${formatDate(locale, job.preferredDates.from, true)} – ${formatDate(locale, job.preferredDates.to)}`;

  return (
    <section className="flex flex-col gap-4 rounded-[20px] border border-border bg-card p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-xl font-extrabold tracking-tight">{job.code}</h2>
        <JobStatusBadge status={job.status} />
        {job.light && <ReviewLightBadge light={job.light} dueOn={job.reviewDueOn} />}
        {job.premium && <span className="rounded-full bg-accent-soft px-2.5 py-1 text-xs font-semibold text-accent-ink">Premium</span>}
      </div>
      <div className="grid gap-4 text-sm md:grid-cols-2">
        <div className="flex flex-col gap-1">
          <p className="font-bold">{job.client.name}</p>
          <a href={`mailto:${job.client.email}`} className="inline-flex items-center gap-1.5 text-primary underline-offset-4 hover:underline">
            <Mail aria-hidden="true" className="size-4" />
            {job.client.email}
          </a>
          {job.client.phone && (
            <a href={`tel:${job.client.phone}`} className="inline-flex items-center gap-1.5 text-primary underline-offset-4 hover:underline">
              <Phone aria-hidden="true" className="size-4" />
              {job.client.phone}
            </a>
          )}
          {!job.client.emailConfirmed && <p className="text-xs text-accent-ink">{t("emailNotConfirmed")}</p>}
          {job.companyName && (
            <p className="text-muted-foreground">
              {job.companyName} · NIF {job.taxId}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-1">
          <p className="inline-flex items-start gap-1.5">
            <MapPin aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
            <span>
              {job.address.line1}
              {job.address.floor && `, ${job.address.floor}`} · {job.address.postalCode} {job.address.locality} ({job.address.municipality})
            </span>
          </p>
          {job.address.accessNotes && <p className="text-muted-foreground">{t("access", { notes: job.address.accessNotes })}</p>}
          <p>{t("dates", { dates })}</p>
          {job.quoteValidUntil && <p>{t("validUntil", { date: formatDate(locale, job.quoteValidUntil) })}</p>}
          {job.siteVisit && (
            <p>
              {t("siteVisit", {
                date: formatDateTime(locale, job.siteVisit.at),
                who: job.siteVisit.assigneeName,
                price: formatCents(locale, job.siteVisit.priceCents),
              })}
            </p>
          )}
        </div>
      </div>
      {job.description && (
        <blockquote className="rounded-xl bg-background px-3 py-2 text-sm">
          <p className="text-xs font-semibold text-muted-foreground">{t("description")}</p>
          <p className="mt-1 whitespace-pre-line">{job.description}</p>
        </blockquote>
      )}
      <JobActions job={job} />
    </section>
  );
}

function QuoteSection({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job");
  const quote = job.status === "in_review" ? (job.draft ?? job.currentQuote) : job.currentQuote;
  return (
    <section aria-labelledby="quote-title" className="flex flex-col gap-3">
      <h3 id="quote-title" className="text-sm font-bold">
        {job.status === "in_review" ? t("draftTitle") : t("quoteTitle", { number: quote.number })}
      </h3>
      <DraftEditor key={`${job.id}-${job.version}-${quote.id}`} jobId={job.id} quote={quote} readOnly={job.status !== "in_review"} />
    </section>
  );
}

function Versions({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.versions");
  const locale = useLocale();
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[560px] text-left text-sm">
        <thead className="text-xs text-muted-foreground">
          <tr>
            <th className="py-2 pr-3 font-semibold">{t("number")}</th>
            <th className="py-2 pr-3 font-semibold">{t("origin")}</th>
            <th className="py-2 pr-3 font-semibold">{t("area")}</th>
            <th className="py-2 pr-3 font-semibold">{t("total")}</th>
            <th className="py-2 pr-3 font-semibold">{t("state")}</th>
            <th className="py-2 font-semibold">{t("date")}</th>
          </tr>
        </thead>
        <tbody>
          {job.versions.map((v) => (
            <tr key={v.id} className="border-t border-border">
              <td className="py-2 pr-3 font-semibold">v{v.number}</td>
              <td className="py-2 pr-3">{t(`origins.${v.origin}`)}</td>
              <td className="py-2 pr-3">{v.areaM2.toLocaleString(locale)} m²</td>
              <td className="py-2 pr-3">
                {formatCents(locale, v.totalCents)}
                {v.manualAdjustment && <span className="ml-1 text-xs text-accent-ink">{t("manual")}</span>}
              </td>
              <td className="py-2 pr-3">
                {v.isDraft ? t("draft") : v.confirmedAt ? t("confirmed", { date: formatDate(locale, v.validUntil ?? v.confirmedAt) }) : t("estimate")}
              </td>
              <td className="py-2">{formatDateTime(locale, v.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A3.5: states with who, when and why, and the emails sent. */
function History({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.history");
  const tStatus = useTranslations("jobs.status");
  const locale = useLocale();
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <section aria-labelledby="states-title">
        <h3 id="states-title" className="mb-3 text-sm font-bold">
          {t("states")}
        </h3>
        <ol className="flex flex-col gap-3 border-l-2 border-border pl-4">
          {job.history.map((entry, i) => (
            <li key={i} className="relative text-sm">
              <span aria-hidden="true" className="absolute top-1.5 -left-[21px] size-2.5 rounded-full bg-primary" />
              <p className="font-semibold">
                {entry.from ? `${tStatus(entry.from)} → ` : ""}
                {tStatus(entry.to)}
              </p>
              <p className="text-xs text-muted-foreground">
                {formatDateTime(locale, entry.changedAt)} · {entry.userName ?? t(`actors.${entry.actor}`)}
              </p>
              {entry.reason && <p className="mt-1">{entry.reason}</p>}
            </li>
          ))}
        </ol>
      </section>
      <section aria-labelledby="emails-title">
        <h3 id="emails-title" className="mb-3 text-sm font-bold">
          {t("emails")}
        </h3>
        {job.emails.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("noEmails")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {job.emails.map((email, i) => (
              <li key={i} className="rounded-xl border border-border bg-card px-3 py-2 text-sm">
                <p className="font-semibold">{email.subject}</p>
                <p className="text-xs text-muted-foreground">
                  {email.to} · {formatDateTime(locale, email.createdAt)} ·{" "}
                  <span className={cn(email.status === "failed" && "font-semibold text-destructive")}>{t(`emailStatus.${email.status as "queued" | "sent" | "failed"}`)}</span>
                  {email.attempts > 1 && ` · ${t("attempts", { count: email.attempts })}`}
                </p>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
