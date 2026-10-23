"use client";

import { Download, LayoutGrid, List, Loader2, Search } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useQuery } from "@tanstack/react-query";
import { useDeferredValue, useState } from "react";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { api } from "@/lib/api/client";
import type { AdminJobListItem } from "@/lib/jobs/api";
import { orThrow } from "@/lib/jobs/api";
import { ALL_STATUSES, type JobStatus } from "@/lib/jobs/status";
import { EMPTY_FILTERS, exportUrl, useAdminJobs, type AdminJobsFilters } from "@/lib/jobs/use-admin-jobs";
import { formatCents, formatDate, formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 25;
const BOARD_SIZE = 100;
const fieldClass =
  "h-10 min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

/** A3.1 · Lista y tablero: filters, search, paged list or board by status, CSV with the same filters. */
export function JobsBoard() {
  const t = useTranslations("admin.jobs");
  const tStatus = useTranslations("jobs.status");
  const [filters, setFilters] = useState<AdminJobsFilters>(EMPTY_FILTERS);
  const [view, setView] = useState<"list" | "board">("list");
  const [page, setPage] = useState(1);
  const deferred = useDeferredValue(filters);
  const jobs = useAdminJobs(deferred, view === "board" ? 1 : page, view === "board" ? BOARD_SIZE : PAGE_SIZE);
  const municipalities = useQuery({
    queryKey: ["admin", "coverage"],
    queryFn: async () => orThrow(await api.GET("/api/admin/coverage")),
    select: (areas) => [...new Set(areas.map((a) => a.municipality))].sort(),
    staleTime: 5 * 60_000,
  });
  const update = (patch: Partial<AdminJobsFilters>) => {
    setFilters({ ...filters, ...patch });
    setPage(1);
  };
  const data = jobs.data;
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const active = JSON.stringify(filters) !== JSON.stringify(EMPTY_FILTERS);

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{data ? t("count", { count: data.total }) : " "}</p>
        </div>
        <div className="flex gap-2">
          <div role="group" aria-label={t("view")} className="flex rounded-xl border border-border bg-card p-1">
            {(["list", "board"] as const).map((key) => (
              <button
                key={key}
                type="button"
                aria-pressed={view === key}
                onClick={() => setView(key)}
                className="flex h-9 items-center gap-1.5 rounded-lg px-3 text-sm font-medium text-muted-foreground aria-pressed:bg-primary aria-pressed:text-primary-foreground"
              >
                {key === "list" ? <List aria-hidden="true" className="size-4" /> : <LayoutGrid aria-hidden="true" className="size-4" />}
                {t(key)}
              </button>
            ))}
          </div>
          <Button asChild variant="outline" size="sm" className="h-11">
            <a href={exportUrl(filters)} download>
              <Download aria-hidden="true" />
              {t("export")}
            </a>
          </Button>
        </div>
      </header>

      <form role="search" aria-label={t("filters")} onSubmit={(e) => e.preventDefault()} className="grid gap-2 rounded-2xl border border-border bg-card p-3 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1.3fr))_repeat(2,minmax(0,1fr))]">
        <label className="relative flex items-center">
          <span className="sr-only">{t("search")}</span>
          <Search aria-hidden="true" className="pointer-events-none absolute left-2.5 size-4 text-muted-foreground" />
          <input type="search" className={cn(fieldClass, "w-full pl-8")} placeholder={t("search")} value={filters.q} onChange={(e) => update({ q: e.target.value })} />
        </label>
        <label className="flex flex-col">
          <span className="sr-only">{t("status")}</span>
          <select className={fieldClass} value={filters.status[0] ?? ""} onChange={(e) => update({ status: e.target.value ? [e.target.value as JobStatus] : [] })}>
            <option value="">{t("allStatuses")}</option>
            {ALL_STATUSES.map((s) => (
              <option key={s} value={s}>
                {tStatus(s)}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col">
          <span className="sr-only">{t("municipality")}</span>
          <select className={fieldClass} value={filters.municipality} onChange={(e) => update({ municipality: e.target.value })}>
            <option value="">{t("allMunicipalities")}</option>
            {(municipalities.data ?? []).map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col">
          <span className="sr-only">Premium</span>
          <select className={fieldClass} value={filters.premium} onChange={(e) => update({ premium: e.target.value as AdminJobsFilters["premium"] })}>
            <option value="">{t("premiumAny")}</option>
            <option value="yes">{t("premiumYes")}</option>
            <option value="no">{t("premiumNo")}</option>
          </select>
        </label>
        <label className="flex flex-col text-xs font-semibold">
          <span className="sr-only">{t("from")}</span>
          <input type="date" aria-label={t("from")} className={fieldClass} value={filters.from} onChange={(e) => update({ from: e.target.value })} />
        </label>
        <label className="flex flex-col text-xs font-semibold">
          <span className="sr-only">{t("to")}</span>
          <input type="date" aria-label={t("to")} className={fieldClass} value={filters.to} min={filters.from || undefined} onChange={(e) => update({ to: e.target.value })} />
        </label>
        {active && (
          <button type="button" className="justify-self-start text-sm font-semibold text-primary sm:col-span-2 lg:col-span-6" onClick={() => update(EMPTY_FILTERS)}>
            {t("clear")}
          </button>
        )}
      </form>

      <div aria-busy={jobs.isFetching} aria-live="polite">
        {jobs.isPending ? (
          <p role="status" className="flex items-center gap-2 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
        ) : jobs.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3">
            <p className="text-destructive">{t("loadError")}</p>
            <Button variant="outline" onClick={() => jobs.refetch()}>
              {t("retry")}
            </Button>
          </div>
        ) : data!.items.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-border bg-card p-6 text-center text-muted-foreground">{t(active ? "noResults" : "empty")}</p>
        ) : view === "list" ? (
          <>
            <JobsTable items={data!.items} />
            {pages > 1 && (
              <nav aria-label={t("pagination")} className="mt-4 flex items-center justify-between gap-3 text-sm">
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
        ) : (
          <Board items={data!.items} statuses={filters.status.length > 0 ? filters.status : ALL_STATUSES} truncated={data!.total > data!.items.length} />
        )}
      </div>
    </div>
  );
}

function JobsTable({ items }: { items: AdminJobListItem[] }) {
  const t = useTranslations("admin.jobs.columns");
  const locale = useLocale();
  return (
    <>
      <ul className="flex flex-col gap-2 md:hidden">
        {items.map((job) => (
          <li key={job.id}>
            <Link href={`/admin/trabajos/${job.id}`} className="flex flex-col gap-1.5 rounded-2xl border border-border bg-card p-3 text-sm">
              <span className="flex items-center justify-between gap-2">
                <span className="font-bold">{job.code}</span>
                <JobStatusBadge status={job.status} />
              </span>
              <span>
                {job.clientName} · {job.locality}
              </span>
              <span className="flex justify-between text-muted-foreground">
                <span>{formatCents(locale, job.totalCents)}</span>
                <span>{formatDateTime(locale, job.updatedAt)}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      <div className="hidden overflow-x-auto rounded-2xl border border-border bg-card md:block">
        <table className="w-full text-left text-sm">
          <thead className="bg-background text-xs text-muted-foreground">
            <tr>
              {(["code", "client", "locality", "status", "total", "margin", "painter", "scheduled", "activity"] as const).map((key) => (
                <th key={key} scope="col" className="px-3 py-2.5 font-semibold whitespace-nowrap">
                  {t(key)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((job) => (
              <tr key={job.id} className="border-t border-border hover:bg-primary-soft/40">
                <td className="px-3 py-2.5 font-bold whitespace-nowrap">
                  <Link href={`/admin/trabajos/${job.id}`} className="text-primary underline-offset-4 hover:underline">
                    {job.code}
                  </Link>
                  {job.premium && <span className="ml-1.5 rounded bg-accent-soft px-1 text-[10px] font-bold text-accent-ink">P</span>}
                </td>
                <td className="px-3 py-2.5">
                  <span className="block">{job.clientName}</span>
                  <span className="block text-xs text-muted-foreground">{job.clientEmail}</span>
                </td>
                <td className="px-3 py-2.5">{job.locality}</td>
                <td className="px-3 py-2.5">
                  <JobStatusBadge status={job.status} />
                </td>
                <td className="px-3 py-2.5 whitespace-nowrap">{formatCents(locale, job.totalCents)}</td>
                <td className="px-3 py-2.5 whitespace-nowrap">{formatCents(locale, job.marginCents)}</td>
                <td className="px-3 py-2.5 text-muted-foreground">{job.painterName ?? "—"}</td>
                <td className="px-3 py-2.5 whitespace-nowrap text-muted-foreground">{job.scheduledOn ? formatDate(locale, job.scheduledOn, true) : "—"}</td>
                <td className="px-3 py-2.5 whitespace-nowrap text-muted-foreground">{formatDateTime(locale, job.updatedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Board({ items, statuses, truncated }: { items: AdminJobListItem[]; statuses: JobStatus[]; truncated: boolean }) {
  const t = useTranslations("admin.jobs");
  const tStatus = useTranslations("jobs.status");
  const locale = useLocale();
  const columns = statuses.filter((s) => items.some((j) => j.status === s) || ["in_review", "quoted"].includes(s));
  return (
    <>
      {truncated && <p className="mb-2 text-xs text-muted-foreground">{t("boardTruncated", { count: items.length })}</p>}
      <div className="flex gap-3 overflow-x-auto pb-2">
        {columns.map((status) => {
          const column = items.filter((j) => j.status === status);
          return (
            <section key={status} aria-label={tStatus(status)} className="flex w-64 shrink-0 flex-col gap-2 rounded-2xl bg-primary-soft/50 p-2">
              <h2 className="flex items-center justify-between px-1 text-sm font-bold">
                {tStatus(status)}
                <span className="text-xs text-muted-foreground">{column.length}</span>
              </h2>
              {column.map((job) => (
                <Link key={job.id} href={`/admin/trabajos/${job.id}`} className="flex flex-col gap-1 rounded-xl border border-border bg-card p-2.5 text-sm hover:border-primary">
                  <span className="font-bold">{job.code}</span>
                  <span className="truncate">{job.clientName}</span>
                  <span className="flex justify-between text-xs text-muted-foreground">
                    <span>{job.locality}</span>
                    <span>{formatCents(locale, job.totalCents)}</span>
                  </span>
                </Link>
              ))}
            </section>
          );
        })}
      </div>
    </>
  );
}
