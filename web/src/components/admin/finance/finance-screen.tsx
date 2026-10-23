"use client";

import { CheckCircle2, Download, Loader2, Lock, Pencil, Plus, Trash2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FormDialog } from "@/components/ui/form-dialog";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import {
  COST_CATEGORIES,
  currentMonth,
  exportFinanceUrl,
  todayInLisbon,
  useAddCost,
  useCloseMonth,
  useDeleteCost,
  useFinanceSummary,
  useMarkPayoutPaid,
  useUpdateCost,
  type CostCategory,
  type CostDto,
  type MonthSummary,
} from "@/lib/finance/use-finance";
import { formatCents, formatDate, formatDateTime } from "@/lib/format";
import { centsFromInput, centsToInput } from "@/lib/numbers";
import { useAdminPainters } from "@/lib/painter/use-admin-painters";
import { cn } from "@/lib/utils";

const fieldClass =
  "h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive";

/** Finanzas (A5.1–A5.4): the month's figures and 50/50 split, its costs, painter payouts and the closing. */
export function FinanceScreen() {
  const t = useTranslations("admin.finance");
  const locale = useLocale();
  const [month, setMonth] = useState(currentMonth);
  const summary = useFinanceSummary(month);
  const monthName = new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(`${month}-01T00:00:00Z`));

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-[13px] font-semibold">
            {t("month")}
            <input type="month" value={month} max={currentMonth()} onChange={(e) => e.target.value && setMonth(e.target.value)} className={fieldClass} />
          </label>
          <Button asChild variant="outline" className="h-11">
            <a href={exportFinanceUrl(month)} download>
              <Download aria-hidden="true" />
              {t("export")}
            </a>
          </Button>
          {summary.data && <CloseMonth summary={summary.data} monthName={monthName} />}
        </div>
      </header>

      {summary.isPending ? (
        <p role="status" className="flex items-center gap-2 py-10 text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : summary.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3 py-10">
          <p className="text-destructive">{t("error")}</p>
          <Button variant="outline" onClick={() => summary.refetch()}>
            {t("retry")}
          </Button>
        </div>
      ) : (
        <>
          {summary.data.closed && summary.data.closedAt && (
            <p className="flex items-center gap-2 self-start rounded-full bg-primary-soft px-3 py-1.5 text-sm font-semibold text-primary">
              <Lock aria-hidden="true" className="size-4" />
              {t("closedOn", { date: formatDateTime(locale, summary.data.closedAt) })}
            </p>
          )}
          <Figures summary={summary.data} />
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
            <Jobs summary={summary.data} />
            <Costs summary={summary.data} month={month} />
          </div>
          <Payouts summary={summary.data} />
        </>
      )}
    </div>
  );
}

function Figures({ summary: s }: { summary: MonthSummary }) {
  const t = useTranslations("admin.finance.figures");
  const tCategory = useTranslations("admin.finance.categories");
  const locale = useLocale();
  const cards: [string, number, string?][] = [
    [t("income"), s.incomeCents, t("incomeHint", { vat: formatCents(locale, s.vatCents) })],
    [t("material"), s.materialCents],
    [t("painters"), s.painterPayCents],
    [t("margin"), s.grossMarginCents, t("marginPct", { pct: s.marginPct.toLocaleString(locale) })],
    [t("costs"), s.costsCents],
    [t("profit"), s.profitCents],
  ];

  return (
    <section aria-labelledby="figures-title" className="flex flex-col gap-3">
      <h2 id="figures-title" className="sr-only">
        {t("title")}
      </h2>
      <dl className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        {cards.map(([label, cents, hint]) => (
          <div key={label} className="rounded-[20px] border border-border bg-card p-4">
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className={cn("text-xl font-extrabold tabular-nums", cents < 0 && "text-destructive")}>{formatCents(locale, cents)}</dd>
            {hint && <dd className="text-xs text-muted-foreground">{hint}</dd>}
          </div>
        ))}
      </dl>
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-[20px] border-2 border-primary bg-card p-4">
        <p className="font-bold">{t("split")}</p>
        <p className="text-2xl font-extrabold text-primary tabular-nums">{t("each", { amount: formatCents(locale, s.perPartnerCents) })}</p>
      </div>
      {s.costsByCategory.length > 0 && (
        <p className="text-sm text-muted-foreground">
          {s.costsByCategory.map((c) => `${tCategory(c.category)} ${formatCents(locale, c.amountCents)}`).join(" · ")}
        </p>
      )}
    </section>
  );
}

function Jobs({ summary }: { summary: MonthSummary }) {
  const t = useTranslations("admin.finance.jobs");
  const locale = useLocale();
  return (
    <section aria-labelledby="jobs-title" className="flex flex-col gap-3">
      <h2 id="jobs-title" className="font-bold">
        {t("title")}
      </h2>
      {summary.jobs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("empty")}</p>
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-border bg-card">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="text-xs text-muted-foreground">
              <tr>
                {(["code", "date", "income", "material", "painter", "margin"] as const).map((key) => (
                  <th key={key} className={cn("px-3 py-2.5 font-semibold", key !== "code" && key !== "date" && "text-right")}>
                    {t(`columns.${key}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {summary.jobs.map((j) => (
                <tr key={j.jobId} className="border-t border-border">
                  <td className="px-3 py-2.5">
                    <Link href={`/admin/trabajos/${j.jobId}`} className="font-semibold text-primary underline-offset-4 hover:underline">
                      {j.code}
                    </Link>
                    <JobStatusBadge status={j.status} className="ml-2" />
                  </td>
                  <td className="px-3 py-2.5">{formatDate(locale, j.at, true)}</td>
                  {[j.incomeCents, j.materialCents, j.painterPayCents, j.marginCents].map((cents, i) => (
                    <td key={i} className="px-3 py-2.5 text-right tabular-nums">
                      {formatCents(locale, cents)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/** A5.2: costs of the month; a closed month shows them read-only. */
function Costs({ summary, month }: { summary: MonthSummary; month: string }) {
  const t = useTranslations("admin.finance.costs");
  const tCategory = useTranslations("admin.finance.categories");
  const locale = useLocale();
  const errorText = useErrorText();
  const remove = useDeleteCost();
  const [editing, setEditing] = useState<CostDto | "new" | null>(null);
  const [deleting, setDeleting] = useState<CostDto | null>(null);
  const open = !summary.closed;

  return (
    <section aria-labelledby="costs-title" className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 id="costs-title" className="font-bold">
          {t("title")}
        </h2>
        {open && (
          <Button size="sm" variant="outline" onClick={() => setEditing("new")}>
            <Plus aria-hidden="true" />
            {t("add")}
          </Button>
        )}
      </div>
      {summary.costs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {summary.costs.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2 text-sm">
              <div className="min-w-0">
                <p className="truncate font-semibold">{c.description}</p>
                <p className="text-xs text-muted-foreground">
                  {formatDate(locale, c.date, true)} · {tCategory(c.category)}
                </p>
              </div>
              <span className="flex items-center gap-1">
                <span className="font-semibold tabular-nums">{formatCents(locale, c.amountCents)}</span>
                {open && (
                  <>
                    <Button variant="ghost" size="icon" aria-label={t("edit", { description: c.description })} onClick={() => setEditing(c)}>
                      <Pencil aria-hidden="true" />
                    </Button>
                    <Button variant="ghost" size="icon" aria-label={t("remove", { description: c.description })} onClick={() => setDeleting(c)}>
                      <Trash2 aria-hidden="true" />
                    </Button>
                  </>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
      {!open && <p className="text-xs text-muted-foreground">{t("closed")}</p>}
      {editing && <CostDialog cost={editing === "new" ? null : editing} month={month} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(next) => !next && setDeleting(null)}
        title={t("removeTitle")}
        description={deleting?.description}
        confirmLabel={t("removeConfirm")}
        cancelLabel={t("cancel")}
        onConfirm={() =>
          deleting && remove.mutate(deleting.id, { onSuccess: () => toast.success(t("removed")), onError: (e) => toast.error(errorText(e)) })
        }
      />
    </section>
  );
}

function CostDialog({ cost, month, onClose }: { cost: CostDto | null; month: string; onClose: () => void }) {
  const t = useTranslations("admin.finance.costs");
  const tCategory = useTranslations("admin.finance.categories");
  const locale = useLocale();
  const errorText = useErrorText();
  const add = useAddCost();
  const update = useUpdateCost();
  const today = todayInLisbon();
  const [date, setDate] = useState(cost?.date ?? (today.startsWith(month) ? today : `${month}-01`));
  const [category, setCategory] = useState<CostCategory>(cost?.category ?? "infrastructure");
  const [description, setDescription] = useState(cost?.description ?? "");
  const [amount, setAmount] = useState(cost ? centsToInput(locale, cost.amountCents) : "");
  const cents = centsFromInput(amount);
  const pending = add.isPending || update.isPending;
  const error = add.error ?? update.error;
  const fieldErrors = error instanceof ApiError ? (error.problem?.errors ?? {}) : {};

  const submit = () => {
    const body = { date, category, description: description.trim(), amountCents: cents!, version: cost?.version ?? null };
    const done = { onSuccess: () => (toast.success(t("saved")), onClose()), onError: (e: unknown) => !(e instanceof ApiError && e.problem?.errors) && toast.error(errorText(e)) };
    if (cost) update.mutate({ id: cost.id, body }, done);
    else add.mutate(body, done);
  };

  return (
    <FormDialog
      open
      onOpenChange={(next) => !next && onClose()}
      title={t(cost ? "editTitle" : "addTitle")}
      submitLabel={t("save")}
      cancelLabel={t("cancel")}
      pending={pending}
      submitDisabled={!description.trim() || cents === null || cents <= 0 || !date}
      onSubmit={submit}
    >
      <div className="flex flex-col gap-3">
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("date")}
          <input type="date" value={date} max={today} onChange={(e) => setDate(e.target.value)} className={fieldClass} aria-invalid={!!fieldErrors.date} />
          {fieldErrors.date && <span className="text-sm font-normal text-destructive">{fieldErrors.date[0]}</span>}
        </label>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("category")}
          <select value={category} onChange={(e) => setCategory(e.target.value as CostCategory)} className={fieldClass}>
            {COST_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {tCategory(c)}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("description")}
          <input value={description} maxLength={200} onChange={(e) => setDescription(e.target.value)} className={fieldClass} />
        </label>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("amount")}
          <input inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} className={fieldClass} aria-invalid={amount !== "" && cents === null} />
        </label>
      </div>
    </FormDialog>
  );
}

/** A5.4: what Pinta still owes its painters, of any month; marking one paid needs the transfer date and reference. */
function Payouts({ summary }: { summary: MonthSummary }) {
  const t = useTranslations("admin.finance.payouts");
  const locale = useLocale();
  const painters = useAdminPainters();
  const names = Object.fromEntries((painters.data ?? []).map((p) => [p.id, p.name]));
  const [paying, setPaying] = useState<MonthSummary["pendingPayouts"][number] | null>(null);

  return (
    <section aria-labelledby="payouts-title" className="flex flex-col gap-3">
      <h2 id="payouts-title" className="font-bold">
        {t("title")}
      </h2>
      {summary.pendingPayouts.length === 0 ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <CheckCircle2 aria-hidden="true" className="size-4 text-primary" />
          {t("empty")}
        </p>
      ) : (
        <ul className="grid gap-2 md:grid-cols-2">
          {summary.pendingPayouts.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-3 rounded-2xl border border-accent bg-card px-4 py-3">
              <div className="min-w-0">
                <p className="font-semibold">{names[p.painterId] ?? "—"}</p>
                <p className="text-xs text-muted-foreground">
                  <Link href={`/admin/trabajos/${p.jobId}`} className="text-primary">
                    {p.code}
                  </Link>{" "}
                  · {t("since", { date: formatDate(locale, p.createdAt, true) })}
                </p>
              </div>
              <span className="flex items-center gap-3">
                <span className="font-bold tabular-nums">{formatCents(locale, p.amountCents)}</span>
                <Button size="sm" onClick={() => setPaying(p)}>
                  {t("markPaid")}
                </Button>
              </span>
            </li>
          ))}
        </ul>
      )}
      {paying && <MarkPaidDialog payout={paying} painter={names[paying.painterId] ?? ""} onClose={() => setPaying(null)} />}
    </section>
  );
}

function MarkPaidDialog({ payout, painter, onClose }: { payout: MonthSummary["pendingPayouts"][number]; painter: string; onClose: () => void }) {
  const t = useTranslations("admin.finance.payouts");
  const locale = useLocale();
  const errorText = useErrorText();
  const mark = useMarkPayoutPaid();
  const today = todayInLisbon();
  const [paidOn, setPaidOn] = useState(today);
  const [reference, setReference] = useState("");
  return (
    <FormDialog
      open
      onOpenChange={(next) => !next && onClose()}
      title={t("dialogTitle", { code: payout.code })}
      description={t("dialogBody", { painter, amount: formatCents(locale, payout.amountCents) })}
      submitLabel={t("markPaid")}
      cancelLabel={t("cancel")}
      pending={mark.isPending}
      submitDisabled={!reference.trim() || !paidOn}
      onSubmit={() =>
        mark.mutate(
          { id: payout.id, paidOn, reference: reference.trim() },
          { onSuccess: () => (toast.success(t("done")), onClose()), onError: (e) => toast.error(errorText(e)) },
        )
      }
    >
      <div className="flex flex-col gap-3">
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("paidOn")}
          <input type="date" value={paidOn} max={today} onChange={(e) => setPaidOn(e.target.value)} className={fieldClass} />
        </label>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("reference")}
          <input value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} className={fieldClass} placeholder="TRF-…" />
        </label>
      </div>
    </FormDialog>
  );
}

/** A5.3: only a finished month, once, with confirmation; its figures stay as they are. */
function CloseMonth({ summary, monthName }: { summary: MonthSummary; monthName: string }) {
  const t = useTranslations("admin.finance.close");
  const locale = useLocale();
  const errorText = useErrorText();
  const close = useCloseMonth();
  const [confirming, setConfirming] = useState(false);
  if (!summary.canClose) return null;
  return (
    <>
      <Button className="h-11" onClick={() => setConfirming(true)} disabled={close.isPending}>
        <Lock aria-hidden="true" />
        {t("button")}
      </Button>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("title", { month: monthName })}
        description={t("body", { profit: formatCents(locale, summary.profitCents), each: formatCents(locale, summary.perPartnerCents) })}
        confirmLabel={t("confirm")}
        cancelLabel={t("cancel")}
        onConfirm={() => close.mutate(summary.month, { onSuccess: () => toast.success(t("done")), onError: (e) => toast.error(errorText(e)) })}
      />
    </>
  );
}
