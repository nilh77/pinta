"use client";

import { Clock, Droplets, Info } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useMemo } from "react";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { formatArea } from "@/lib/format";
import { displayNames, elementAreaOf, toEstimateRequest, validateElement } from "@/lib/quote/draft";
import { useEstimate } from "@/lib/quote/use-quote-data";
import { cn } from "@/lib/utils";
import { PriceRange } from "./price-range";
import { QuoteShell, type QuoteContext } from "./quote-shell";

/** Screen 4 · Estimación (board Estimacion) [C2.8]. Only range, areas and flags: never rates, material or margin. */
export function EstimateStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={2} back={{ href: "/cotizar/esquema", label: t("estimate.back") }}>
      {(context) => <EstimateView {...context} />}
    </QuoteShell>
  );
}

function EstimateView({ draft, catalog, info }: QuoteContext) {
  const t = useTranslations("quote.estimate");
  const tSchema = useTranslations("quote.schema");
  const locale = useLocale();
  const router = useRouter();
  const request = useMemo(() => (draft ? toEstimateRequest(draft, info) : null), [draft, info]);
  const { estimate, isStale, isError, retry } = useEstimate(request, { debounceMs: 0 });

  useEffect(() => {
    if (!draft) router.replace("/cotizar");
    else if (!request) router.replace("/cotizar/esquema");
  }, [draft, request, router]);
  if (!draft || !request) return null;

  const names = displayNames(draft.elements, Object.fromEntries(catalog.roomTypes.map((r) => [r.id, r.name])), tSchema("surfaceName"));
  const priced = draft.elements
    .map((element, index) => ({ element, name: names[index], area: elementAreaOf(element, info) }))
    .filter(({ element, area }) => element.described && Object.keys(validateElement(element)).length === 0 && (area?.total ?? 0) > 0);
  const damaged = priced.filter(({ element }) => element.damage).map(({ name }) => name);
  const occupancy = catalog.occupancies.find((o) => o.id === request.occupancy)?.name;
  const paintName = (id: string) => catalog.paints.find((p) => p.id === id)?.name ?? id;
  const kindLabel = t(`kind.${draft.type}`, { count: priced.length });

  return (
    <div className="flex flex-1 flex-col pb-44 lg:pb-12">
      <h1 className="text-[26px] leading-tight font-bold tracking-tight">{t("title")}</h1>

      <section
        aria-live="polite"
        aria-busy={isStale}
        className="relative mt-4 overflow-hidden rounded-[22px] border border-border bg-card p-5"
      >
        <svg width="120" height="120" viewBox="0 0 120 120" aria-hidden="true" className="absolute -top-7 -right-7">
          <circle cx="60" cy="60" r="56" fill="var(--accent-soft)" />
          <circle cx="60" cy="60" r="34" fill="none" stroke="var(--accent)" strokeWidth="3" />
        </svg>
        <div className="relative">
          <p className="text-[13px] text-muted-foreground">
            {kindLabel} · {estimate ? formatArea(locale, Math.round(estimate.areaM2 * 100)) : "…"}
          </p>
          {isError ? (
            <div className="mt-3 flex flex-col items-start gap-2">
              <p className="font-semibold text-destructive">{t("error")}</p>
              <Button variant="outline" size="sm" onClick={retry}>
                {t("retry")}
              </Button>
            </div>
          ) : !estimate ? (
            <div className="mt-3 h-10 w-56 animate-pulse rounded-xl bg-primary-soft motion-reduce:animate-none" />
          ) : estimate.overMax || estimate.rangeMinCents == null || estimate.rangeMaxCents == null ? (
            <p className={cn("mt-2 font-display text-2xl leading-tight font-extrabold", isStale && "opacity-50")}>{t("custom")}</p>
          ) : (
            <>
              <PriceRange
                minCents={estimate.rangeMinCents}
                maxCents={estimate.rangeMaxCents}
                className={cn("mt-2 block font-display text-[34px] leading-tight font-extrabold tracking-tight tabular-nums transition-opacity motion-reduce:transition-none", isStale && "opacity-50")}
              />
              <p className="mt-1 text-[13px] text-muted-foreground">{t("vatIncluded")}</p>
            </>
          )}
          <ul aria-label={t("badgesLabel")} className="mt-3.5 flex flex-wrap gap-1.5">
            <Badge tone="accent">{t("badges.material")}</Badge>
            {occupancy && <Badge>{occupancy}</Badge>}
            {estimate?.minimumApplied && <Badge tone="accent">{t("badges.minimum")}</Badge>}
            {estimate?.premium && <Badge>{t("badges.premium")}</Badge>}
            {estimate?.rangeWidened && !estimate.overMax && <Badge>{t("badges.widened")}</Badge>}
          </ul>
        </div>
      </section>

      {estimate?.overMax && <Note icon={Info}>{t("overMaxNote")}</Note>}
      <Note icon={Clock}>{t("review", { days: catalog.reviewDeadlineBusinessDays })}</Note>
      {damaged.length > 0 && (
        <Note icon={Droplets} tone="accent">
          {t("damage", { rooms: damaged.join(", ") })}
        </Note>
      )}

      <section aria-labelledby="summary-title" className="mt-5">
        <h2 id="summary-title" className="mb-2 text-[13px] font-semibold">
          {t("summary")}
        </h2>
        <ul className="rounded-2xl border border-border bg-card px-4">
          {priced.map(({ element, name, area }) => (
            <li key={element.id} className="flex items-center justify-between gap-3 border-b border-border/70 py-3 last:border-b-0">
              <div className="min-w-0">
                <div className="flex items-center gap-1.5">
                  <span className="truncate text-sm font-semibold">{name}</span>
                  {element.damage && <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-ink">{tSchema("damageBadge")}</span>}
                </div>
                <div className="text-xs text-muted-foreground">{paintName(element.paintId)}</div>
              </div>
              <span className="shrink-0 text-sm font-medium tabular-nums">{formatArea(locale, area!.total)}</span>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="includes-title" className="mt-5 grid gap-3 sm:grid-cols-2">
        <div className="rounded-2xl border border-border bg-card p-4">
          <h2 id="includes-title" className="text-[13px] font-semibold">
            {t("includes.title")}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("includes.body")}</p>
        </div>
        <div className="rounded-2xl border border-border bg-card p-4">
          <h2 className="text-[13px] font-semibold">{t("next.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("next.body", { reviewDays: catalog.reviewDeadlineBusinessDays, validityDays: catalog.quoteValidityDays })}
          </p>
        </div>
      </section>

      <div className="fixed inset-x-0 bottom-0 z-30 rounded-t-[20px] border-t border-border bg-card px-5 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))] lg:static lg:mt-6 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0">
        <div className="mx-auto flex max-w-2xl flex-col gap-2">
          {/* Screen 5 (photos and details) arrives in H3. */}
          <Button size="lg" className="w-full" disabled aria-describedby="continue-soon">
            {t("continue")}
          </Button>
          <p id="continue-soon" className="text-center text-xs text-muted-foreground">
            {t("continueSoon")}
          </p>
          <Button asChild variant="ghost">
            <Link href="/cotizar/esquema">{t("edit")}</Link>
          </Button>
        </div>
      </div>
    </div>
  );
}

function Badge({ children, tone = "primary" }: { children: React.ReactNode; tone?: "primary" | "accent" }) {
  return (
    <li className={cn("rounded-[10px] px-2.5 py-1 text-xs font-semibold", tone === "accent" ? "bg-accent-soft text-accent-ink" : "bg-primary-soft text-[#0b5f59]")}>
      {children}
    </li>
  );
}

function Note({ icon: Icon, tone = "primary", children }: { icon: typeof Clock; tone?: "primary" | "accent"; children: React.ReactNode }) {
  return (
    <div className={cn("mt-2 flex items-start gap-3 rounded-2xl border p-3.5", tone === "accent" ? "border-[#f2d27a] bg-[#fffbeb] text-[#4a3700]" : "border-border bg-card")}>
      <Icon aria-hidden="true" className={cn("mt-px size-5 shrink-0", tone === "accent" ? "text-accent-ink" : "text-primary")} />
      <p className="text-[13px] leading-relaxed">{children}</p>
    </div>
  );
}
