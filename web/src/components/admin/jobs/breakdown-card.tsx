"use client";

import { TriangleAlert } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import type { Schemas } from "@/lib/jobs/api";
import { formatArea, formatCents } from "@/lib/format";
import { cn } from "@/lib/utils";

type Breakdown = Schemas["QuoteBreakdownDto"];

/** The internal breakdown (admin only): material, labour, extras, VAT, painter pay and margin with its alert (A2.2). */
export function BreakdownCard({ breakdown, saving }: { breakdown: Breakdown; saving?: boolean }) {
  const t = useTranslations("admin.job.breakdown");
  const locale = useLocale();
  const money = (cents: number) => formatCents(locale, cents);

  return (
    <section aria-labelledby="breakdown-title" aria-busy={saving} className={cn("rounded-[20px] border border-border bg-card p-4 transition-opacity", saving && "opacity-60")}>
      <h3 id="breakdown-title" className="mb-3 font-bold">
        {t("title")}
      </h3>
      <dl className="flex flex-col gap-1.5 text-sm">
        <Row label={t("area")} value={`${formatArea(locale, Math.round(breakdown.areaM2 * 100))} · ${t("litres", { litres: breakdown.litres.toLocaleString(locale) })}`} />
        <Row label={t("material")} value={money(breakdown.materialCents)} />
        <Row label={t("labour")} value={money(breakdown.labourCents)} />
        {breakdown.extrasCents > 0 && <Row label={t("extras")} value={money(breakdown.extrasCents)} />}
        <Row label={t("subtotal")} value={money(breakdown.subtotalCents)} strong />
        <Row label={t("vat")} value={money(breakdown.vatCents)} />
        <Row label={t("total")} value={money(breakdown.totalCents)} strong big />
        <Row label={t("painter")} value={money(breakdown.painterTotalCents)} separated />
        <Row
          label={t("margin")}
          value={`${money(breakdown.marginCents)} · ${breakdown.marginPct.toLocaleString(locale, { maximumFractionDigits: 1 })}%`}
          strong
          tone={breakdown.belowMinMargin ? "danger" : "ok"}
        />
        <Row label={t("deposit")} value={money(breakdown.depositCents)} />
        <Row label={t("balance")} value={money(breakdown.balanceCents)} />
        {breakdown.rangeMinCents != null && breakdown.rangeMaxCents != null && (
          <Row label={t("range")} value={`${formatCents(locale, breakdown.rangeMinCents)} – ${formatCents(locale, breakdown.rangeMaxCents)}`} />
        )}
      </dl>
      <div className="mt-3 flex flex-wrap gap-1.5">
        {breakdown.premium && <Flag tone="accent">{t("premium")}</Flag>}
        {breakdown.minimumApplied && <Flag>{t("minimumApplied")}</Flag>}
        {breakdown.overMax && <Flag>{t("overMax")}</Flag>}
      </div>
      {breakdown.belowMinMargin && (
        <p role="alert" className="mt-3 flex items-start gap-2 rounded-xl bg-destructive/10 px-3 py-2 text-sm font-medium text-destructive">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {t("lowMargin")}
        </p>
      )}
    </section>
  );
}

function Row({ label, value, strong, big, tone, separated }: { label: string; value: string; strong?: boolean; big?: boolean; tone?: "ok" | "danger"; separated?: boolean }) {
  return (
    // A <dl> may only hold groups: the line between client price and internal figures is a border, not an <hr>.
    <div className={cn("flex justify-between gap-3", separated && "mt-1.5 border-t border-border pt-3")}>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={cn("text-right whitespace-nowrap", strong && "font-semibold", big && "text-lg font-extrabold", tone === "danger" && "text-destructive", tone === "ok" && "text-primary")}>
        {value}
      </dd>
    </div>
  );
}

function Flag({ children, tone }: { children: React.ReactNode; tone?: "accent" }) {
  return (
    <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", tone === "accent" ? "bg-accent-soft text-accent-ink" : "bg-primary-soft text-[#0b5f59]")}>
      {children}
    </span>
  );
}
