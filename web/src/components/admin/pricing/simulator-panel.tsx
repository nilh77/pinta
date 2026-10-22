"use client";

import { AlertTriangle, Calculator, Loader2, Plus, X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useDeferredValue, useId, useMemo, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { localName, type PricingCatalog } from "@/lib/admin/pricing-form";
import {
  defaultSampleJob,
  newRoom,
  newSurface,
  toSimulateRequest,
  type Occupancy,
  type QuoteBreakdown,
  type SampleElement,
  type SampleJob,
} from "@/lib/admin/simulator";
import { useSimulation } from "@/lib/admin/use-pricing";
import { formatEuros } from "@/lib/format";
import { cn } from "@/lib/utils";

const control =
  "h-11 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

function Field({ label, children, className }: { label: string; children: (id: string) => ReactNode; className?: string }) {
  const id = useId();
  return (
    <div className={cn("flex min-w-0 flex-col gap-1", className)}>
      <label htmlFor={id} className="text-xs font-semibold text-muted-foreground">
        {label}
      </label>
      {children(id)}
    </div>
  );
}

/** A1.8: a sample job priced with the current (saved) configuration. Nothing is stored. */
export function SimulatorPanel({ catalog, catalogVersion }: { catalog: PricingCatalog; catalogVersion: number }) {
  const t = useTranslations("admin.simulator");
  const locale = useLocale();
  const [job, setJob] = useState<SampleJob>(defaultSampleJob);
  const request = useMemo(() => toSimulateRequest(job), [job]);
  const simulation = useSimulation(useDeferredValue(request), catalogVersion);

  const active = <T extends { isActive: boolean }>(items: T[]) => items.filter((item) => item.isActive);
  const options = {
    rooms: active(catalog.roomTypes).map((r) => ({ value: r.key, label: localName(r.name, locale) })),
    surfaces: active(catalog.surfaces).map((s) => ({ value: s.key, label: localName(s.name, locale) })),
    paints: active(catalog.paints).map((p) => ({ value: p.key, label: localName(p.name, locale) })),
    occupancies: active(catalog.modifiers).map((m) => ({ value: m.key as Occupancy, label: localName(m.name, locale), factor: m.factor })),
  };
  const surfaceNames = new Map(catalog.surfaces.map((s) => [s.key, localName(s.name, locale)]));

  const update = (index: number, patch: Partial<SampleElement>) =>
    setJob((current) => ({ ...current, elements: current.elements.map((e, i) => (i === index ? { ...e, ...patch } : e)) }));

  const result = simulation.data;
  return (
    <section aria-labelledby="simulator-title" className="flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 sm:p-5">
      <div>
        <div className="flex items-center gap-2">
          <Calculator aria-hidden="true" className="size-5 text-primary" />
          <h2 id="simulator-title" className="text-lg font-bold">
            {t("title")}
          </h2>
        </div>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </div>

      <Field label={t("occupancy")}>
        {(id) => (
          <select id={id} className={control} value={job.occupancy} onChange={(e) => setJob({ ...job, occupancy: e.target.value as Occupancy })}>
            {options.occupancies.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label} · ×{o.factor.toLocaleString(locale === "en" ? "en-GB" : "es-ES", { minimumFractionDigits: 2 })}
              </option>
            ))}
          </select>
        )}
      </Field>

      <ol className="flex flex-col gap-3">
        {job.elements.map((element, index) => (
          <li key={index} className="flex flex-col gap-2.5 rounded-xl border border-border bg-background p-3">
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm font-semibold">
                {t(element.kind === "room" ? "room" : "surface", { number: index + 1 })}
              </span>
              {job.elements.length > 1 && (
                <button
                  type="button"
                  aria-label={t("remove", { number: index + 1 })}
                  onClick={() => setJob({ ...job, elements: job.elements.filter((_, i) => i !== index) })}
                  className="flex size-9 items-center justify-center rounded-lg text-muted-foreground hover:bg-primary-soft hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
                >
                  <X aria-hidden="true" className="size-4" />
                </button>
              )}
            </div>
            <div className="grid grid-cols-2 gap-2">
              {element.kind === "room" ? (
                <Field label={t("roomType")}>
                  {(id) => (
                    <select id={id} className={control} value={element.roomTypeId} onChange={(e) => update(index, { roomTypeId: e.target.value })}>
                      {options.rooms.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                  )}
                </Field>
              ) : (
                <Field label={t("surfaceType")}>
                  {(id) => (
                    <select id={id} className={control} value={element.surfaceTypeId} onChange={(e) => update(index, { surfaceTypeId: e.target.value })}>
                      {options.surfaces.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </select>
                  )}
                </Field>
              )}
              <Field label={t("paint")}>
                {(id) => (
                  <select id={id} className={control} value={element.paintId} onChange={(e) => update(index, { paintId: e.target.value })}>
                    {options.paints.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                )}
              </Field>
              {element.kind === "room" && (
                <Field label={t("size")} className="col-span-2">
                  {(id) => (
                    <select id={id} className={control} value={element.size} onChange={(e) => update(index, { size: e.target.value as SampleElement["size"] })}>
                      <option value="">{t("exactMeasures")}</option>
                      {(["S", "M", "L"] as const).map((s) => <option key={s} value={s}>{s}</option>)}
                    </select>
                  )}
                </Field>
              )}
              {(element.kind === "surface" || !element.size) && (
                <div className={cn("col-span-2 grid gap-2", element.kind === "room" ? "grid-cols-3" : "grid-cols-2")}>
                  <Field label={t("length")}>
                    {(id) => <input id={id} inputMode="decimal" className={control} value={element.length} onChange={(e) => update(index, { length: e.target.value })} />}
                  </Field>
                  <Field label={t(element.kind === "room" ? "width" : "heightOrWidth")}>
                    {(id) => <input id={id} inputMode="decimal" className={control} value={element.width} onChange={(e) => update(index, { width: e.target.value })} />}
                  </Field>
                  {element.kind === "room" && (
                    <Field label={t("height")}>
                      {(id) => (
                        <input id={id} inputMode="decimal" placeholder={t("defaultHeight")} className={control} value={element.height} onChange={(e) => update(index, { height: e.target.value })} />
                      )}
                    </Field>
                  )}
                </div>
              )}
            </div>
            {element.kind === "room" && (
              <fieldset className="flex flex-wrap items-center gap-x-3 gap-y-1">
                <legend className="sr-only">{t("wallsAndCeiling")}</legend>
                {(["A", "B", "C", "D"] as const).map((wall, w) => (
                  <label key={wall} className="flex min-h-11 items-center gap-1.5 text-sm">
                    <Checkbox
                      checked={element.walls[w]}
                      onCheckedChange={(checked) =>
                        update(index, { walls: element.walls.map((on, i) => (i === w ? checked === true : on)) as SampleElement["walls"] })
                      }
                    />
                    {t("wall", { wall })}
                  </label>
                ))}
                <label className="flex min-h-11 items-center gap-1.5 text-sm">
                  <Checkbox checked={element.ceiling} onCheckedChange={(checked) => update(index, { ceiling: checked === true })} />
                  {t("ceiling")}
                </label>
              </fieldset>
            )}
            <label className="flex min-h-11 items-center gap-1.5 text-sm">
              <Checkbox checked={element.damage} onCheckedChange={(checked) => update(index, { damage: checked === true })} />
              {t("damage")}
            </label>
          </li>
        ))}
      </ol>
      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" size="sm" onClick={() => setJob({ ...job, elements: [...job.elements, newRoom()] })}>
          <Plus aria-hidden="true" />
          {t("addRoom")}
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={() => setJob({ ...job, elements: [...job.elements, newSurface()] })}>
          <Plus aria-hidden="true" />
          {t("addSurface")}
        </Button>
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-semibold">{t("extras")}</legend>
        {job.extras.map((extra, index) => (
          <div key={index} className="grid grid-cols-[1fr_5.5rem_5.5rem_auto] items-end gap-2">
            <Field label={t("extraDescription")}>
              {(id) => (
                <input id={id} className={control} value={extra.description} onChange={(e) => setJob({ ...job, extras: job.extras.map((x, i) => (i === index ? { ...x, description: e.target.value } : x)) })} />
              )}
            </Field>
            <Field label={t("extraClient")}>
              {(id) => (
                <input id={id} inputMode="decimal" className={control} value={extra.clientPrice} onChange={(e) => setJob({ ...job, extras: job.extras.map((x, i) => (i === index ? { ...x, clientPrice: e.target.value } : x)) })} />
              )}
            </Field>
            <Field label={t("extraPainter")}>
              {(id) => (
                <input id={id} inputMode="decimal" className={control} value={extra.painterPay} onChange={(e) => setJob({ ...job, extras: job.extras.map((x, i) => (i === index ? { ...x, painterPay: e.target.value } : x)) })} />
              )}
            </Field>
            <button
              type="button"
              aria-label={t("removeExtra", { number: index + 1 })}
              onClick={() => setJob({ ...job, extras: job.extras.filter((_, i) => i !== index) })}
              className="flex size-11 items-center justify-center rounded-lg text-muted-foreground hover:bg-primary-soft hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              <X aria-hidden="true" className="size-4" />
            </button>
          </div>
        ))}
        <div>
          <Button type="button" variant="ghost" size="sm" onClick={() => setJob({ ...job, extras: [...job.extras, { description: "", clientPrice: "", painterPay: "" }] })}>
            <Plus aria-hidden="true" />
            {t("addExtra")}
          </Button>
        </div>
      </fieldset>

      <div aria-live="polite" aria-busy={simulation.isFetching} className="flex flex-col gap-3">
        {simulation.isError ? (
          <p role="alert" className="rounded-xl bg-destructive/10 px-4 py-3 text-sm font-medium text-destructive">{t("error")}</p>
        ) : !result ? (
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
            {t("calculating")}
          </p>
        ) : "problem" in result ? (
          <div role="alert" className="rounded-xl bg-accent-soft px-4 py-3 text-sm text-accent-ink">
            <p className="font-semibold">{t("invalid")}</p>
            <ul className="mt-1 list-disc pl-5">
              {Object.values(result.problem.errors ?? {}).flat().map((message, i) => <li key={i}>{message}</li>)}
            </ul>
          </div>
        ) : (
          <Breakdown breakdown={result.breakdown} surfaceNames={surfaceNames} stale={simulation.isFetching} />
        )}
      </div>
    </section>
  );
}

function Breakdown({ breakdown: b, surfaceNames, stale }: { breakdown: QuoteBreakdown; surfaceNames: Map<string, string>; stale: boolean }) {
  const t = useTranslations("admin.simulator");
  const locale = useLocale();
  const eur = (cents: number) => formatEuros(locale, cents / 100);
  const range = (cents: number) => formatEuros(locale, cents / 100, 0);
  const dec = (value: number, digits = 2) =>
    value.toLocaleString(locale === "en" ? "en-GB" : "es-ES", { minimumFractionDigits: digits, maximumFractionDigits: digits });

  return (
    <div className={cn("flex flex-col gap-3 transition-opacity", stale && "opacity-60")}>
      <div className="rounded-xl bg-foreground p-4 text-background">
        <div className="text-xs font-medium opacity-80">{t("total")}</div>
        <div className="font-display text-3xl font-extrabold tabular-nums">{eur(b.totalCents)}</div>
        <div className="mt-1 text-sm opacity-90">
          {b.overMax || b.rangeMinCents == null || b.rangeMaxCents == null
            ? t("customQuote")
            : t("clientSees", { min: range(b.rangeMinCents), max: range(b.rangeMaxCents) })}
        </div>
        <div className="mt-2 flex flex-wrap gap-1.5">
          {b.minimumApplied && <Flag>{t("flags.minimum")}</Flag>}
          {b.overMax && <Flag>{t("flags.overMax")}</Flag>}
          {b.premium && <Flag>{t("flags.premium")}</Flag>}
          {b.rangeWidened && <Flag>{t("flags.widened")}</Flag>}
        </div>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[320px] border-collapse text-xs tabular-nums">
          <caption className="sr-only">{t("lines.caption")}</caption>
          <thead>
            <tr className="text-muted-foreground">
              <th className="pb-1 text-left font-medium">{t("lines.surface")}</th>
              <th className="pb-1 text-right font-medium">m²</th>
              <th className="pb-1 text-right font-medium">L</th>
              <th className="pb-1 text-right font-medium">{t("lines.labour")}</th>
              <th className="pb-1 text-right font-medium">{t("lines.painter")}</th>
            </tr>
          </thead>
          <tbody>
            {b.lines.map((line, i) => (
              <tr key={i} className="border-t border-border/60">
                <td className="py-1 pr-2">{line.element + 1} · {surfaceNames.get(line.surface) ?? line.surface}</td>
                <td className="py-1 text-right">{dec(line.areaM2)}</td>
                <td className="py-1 text-right">{dec(line.litres)}</td>
                <td className="py-1 text-right">{eur(line.labourCents)}</td>
                <td className="py-1 text-right">{eur(line.painterCents)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <dl className="flex flex-col gap-1.5 text-sm tabular-nums">
        <Row label={t("area")} value={`${dec(b.areaM2)} m²`} />
        <Row label={t("material", { litres: dec(b.litres) })} value={eur(b.materialCents)} />
        <Row label={t("labour")} value={eur(b.labourCents)} />
        {b.extrasCents > 0 && <Row label={t("extrasTotal")} value={eur(b.extrasCents)} />}
        <Row label={t("subtotal")} value={eur(b.subtotalCents)} strong />
        <Row label={t("vat")} value={eur(b.vatCents)} />
        <Row label={t("painterTotal")} value={eur(b.painterTotalCents)} />
        <Row label={t("deposit")} value={`${eur(b.depositCents)} · ${t("balance", { amount: eur(b.balanceCents) })}`} />
      </dl>

      <div className={cn("rounded-xl px-4 py-3", b.belowMinMargin ? "bg-accent-soft text-accent-ink" : "bg-primary-soft text-primary")}>
        <div className="flex items-center justify-between gap-2 font-semibold">
          <span className="flex items-center gap-1.5">
            {b.belowMinMargin && <AlertTriangle aria-hidden="true" className="size-4" />}
            {t("margin")}
          </span>
          <span className="tabular-nums">{eur(b.marginCents)}</span>
        </div>
        <div className="text-xs">{t("marginPct", { pct: dec(b.marginPct, 1) })}</div>
        {b.belowMinMargin && <div className="mt-1 text-xs font-semibold">{t("lowMargin")}</div>}
      </div>
    </div>
  );
}

function Flag({ children }: { children: ReactNode }) {
  return <span className="rounded-full bg-accent px-2 py-0.5 text-[11px] font-bold text-accent-foreground">{children}</span>;
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={cn("flex items-baseline justify-between gap-3", strong && "font-semibold")}>
      <dt className={strong ? undefined : "text-muted-foreground"}>{label}</dt>
      <dd className="text-right">{value}</dd>
    </div>
  );
}
