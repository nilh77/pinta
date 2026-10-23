"use client";

import { Loader2, Plus, Trash2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/problem";
import type { AdminQuote, DraftQuoteRequest, Schemas } from "@/lib/jobs/api";
import { parseEurosToCents, toDraftRequest, withMeasures, type DraftElement } from "@/lib/jobs/draft";
import { useCatalogExtras, useSaveDraft } from "@/lib/jobs/use-admin-jobs";
import { useCatalog } from "@/lib/quote/use-quote-data";
import { formatArea, formatCents } from "@/lib/format";
import { cn } from "@/lib/utils";
import { BreakdownCard } from "./breakdown-card";
import { DecimalInput } from "./decimal-input";

const SAVE_DELAY_MS = 700;
const selectClass =
  "h-10 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:bg-background";

/**
 * The review draft (A2.3–A2.7): every change is saved as the draft and repriced on the server a moment later, so the
 * breakdown always comes from the engine. Read-only outside "En revisión".
 */
export function DraftEditor({ jobId, quote, readOnly }: { jobId: string; quote: AdminQuote; readOnly: boolean }) {
  const t = useTranslations("admin.job.draft");
  const locale = useLocale();
  const [request, setRequest] = useState<DraftQuoteRequest>(() => toDraftRequest(quote));
  const [breakdown, setBreakdown] = useState(quote.breakdown);
  const [elementAreas, setElementAreas] = useState(() => quote.elements.map((e) => e.areaM2));
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const save = useSaveDraft(jobId);
  const sequence = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const catalog = useCatalog();

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  const change = (next: DraftQuoteRequest) => {
    setRequest(next);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      const mine = ++sequence.current;
      save.mutate(next, {
        onSuccess: (draft) => {
          if (mine !== sequence.current) return;
          setBreakdown(draft.breakdown);
          setElementAreas(draft.elements.map((e) => e.areaM2));
          setErrors({});
        },
        onError: (error) => {
          if (mine !== sequence.current) return;
          setErrors(error instanceof ApiError ? (error.problem?.errors ?? { "": [error.message] }) : { "": [t("saveError")] });
        },
      });
    }, SAVE_DELAY_MS);
  };
  const setElement = (index: number, patch: Partial<DraftElement>) =>
    change({ ...request, elements: request.elements.map((e, i) => (i === index ? { ...e, ...patch } : e)) });

  const info = catalog.info;
  const names = {
    room: Object.fromEntries((catalog.data?.roomTypes ?? []).map((r) => [r.id, r.name])),
    surface: Object.fromEntries((catalog.data?.surfaceTypes ?? []).map((s) => [s.id, s.name])),
  };
  const errorEntries = Object.entries(errors);

  return (
    <div className="@container">
      <div className="grid gap-4 @4xl:grid-cols-[minmax(0,1fr)_340px]">
        <fieldset disabled={readOnly} className="flex min-w-0 flex-col gap-4">
          <legend className="sr-only">{t("legend")}</legend>
          <div className="flex flex-wrap items-end gap-3">
            <label className="flex flex-col gap-1 text-[13px] font-semibold">
              {t("occupancy")}
              <select
                className={cn(selectClass, "w-48")}
                value={request.occupancy}
                onChange={(e) => change({ ...request, occupancy: e.target.value as DraftQuoteRequest["occupancy"] })}
              >
                {(catalog.data?.occupancies ?? []).map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.name}
                  </option>
                ))}
              </select>
            </label>
            <span aria-live="polite" className="flex min-h-10 items-center gap-2 text-xs text-muted-foreground">
              {save.isPending ? (
                <>
                  <Loader2 aria-hidden="true" className="size-3.5 animate-spin motion-reduce:animate-none" />
                  {t("saving")}
                </>
              ) : save.isSuccess ? (
                t("saved")
              ) : readOnly ? (
                t("readOnly")
              ) : null}
            </span>
          </div>

          {errorEntries.length > 0 && (
            <ul role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 px-3.5 py-2.5 text-sm text-destructive">
              {errorEntries.map(([path, messages]) => (
                <li key={path}>
                  {path && <span className="font-semibold">{path}: </span>}
                  {messages.join(" ")}
                </li>
              ))}
            </ul>
          )}

          <ul className="flex flex-col gap-2">
            {request.elements.map((element, index) => (
              <li key={index} className="rounded-2xl border border-border bg-card p-3">
                <ElementRow
                  element={element}
                  index={index}
                  title={[element.kind === "room" ? names.room[element.roomTypeId ?? ""] : names.surface[element.surfaceTypeId ?? ""], element.name]
                    .filter(Boolean)
                    .join(" · ")}
                  areaText={elementAreas[index] != null ? formatArea(locale, Math.round(elementAreas[index]! * 100)) : "—"}
                  paints={catalog.data?.paints ?? []}
                  onChange={(patch) => setElement(index, patch)}
                  onToggleMode={() => setElement(index, withMeasures(element, info?.roomTypes[element.roomTypeId ?? ""]?.sizes, info?.defaultHeight ?? 2.6))}
                  onRemove={request.elements.length > 1 ? () => change({ ...request, elements: request.elements.filter((_, i) => i !== index) }) : undefined}
                />
              </li>
            ))}
          </ul>
          {!readOnly && (
            <AddRoom
              roomTypes={catalog.data?.roomTypes ?? []}
              onAdd={(roomTypeId) => {
                const suggested = catalog.data?.roomTypes.find((r) => r.id === roomTypeId)?.suggestedPaintId ?? info?.defaultPaintId ?? "matte";
                const room: DraftElement = {
                  kind: "room",
                  name: null,
                  roomTypeId,
                  size: "M",
                  length: null,
                  width: null,
                  height: null,
                  walls: [true, true, true, true],
                  ceiling: true,
                  surfaceTypeId: null,
                  paintId: suggested,
                  damage: false,
                  clientKey: null,
                };
                change({ ...request, elements: [...request.elements, room] });
              }}
            />
          )}

          <Extras request={request} onChange={change} readOnly={readOnly} />
          <Adjustments request={request} onChange={change} />
        </fieldset>

        <div className="@4xl:sticky @4xl:top-4 @4xl:self-start">
          <BreakdownCard breakdown={breakdown} saving={save.isPending} />
          <p className="mt-2 text-xs text-muted-foreground">{t("clientSees", { total: formatCents(locale, breakdown.totalCents) })}</p>
        </div>
      </div>
    </div>
  );
}

function ElementRow({
  element,
  index,
  title,
  areaText,
  paints,
  onChange,
  onToggleMode,
  onRemove,
}: {
  element: DraftElement;
  index: number;
  title: string;
  areaText: string;
  paints: Schemas["CatalogOption"][];
  onChange: (patch: Partial<DraftElement>) => void;
  onToggleMode: () => void;
  onRemove?: () => void;
}) {
  const t = useTranslations("admin.job.draft");
  const id = useId();
  const isRoom = element.kind === "room";
  const measured = !isRoom || element.size == null;

  return (
    <div className="@container flex flex-col gap-2.5">
      <div className="flex items-center justify-between gap-2">
        <p className="min-w-0 truncate text-sm font-bold">
          {index + 1}. {title || t(isRoom ? "room" : "surface")}
        </p>
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold whitespace-nowrap text-primary">{areaText}</span>
          {onRemove && (
            <Button type="button" variant="ghost" size="icon" aria-label={t("remove", { index: index + 1 })} onClick={onRemove}>
              <Trash2 aria-hidden="true" />
            </Button>
          )}
        </div>
      </div>
      <div className="grid grid-cols-2 gap-2 @md:grid-cols-4 @2xl:grid-cols-6">
        {isRoom && (
          <label className="flex flex-col gap-1 text-xs font-semibold">
            {t("mode")}
            <select className={selectClass} value={measured ? "measures" : "size"} onChange={onToggleMode}>
              <option value="size">{t("bySize")}</option>
              <option value="measures">{t("byMeasures")}</option>
            </select>
          </label>
        )}
        {isRoom && !measured ? (
          <label className="flex flex-col gap-1 text-xs font-semibold">
            {t("size")}
            <select className={selectClass} value={element.size ?? "M"} onChange={(e) => onChange({ size: e.target.value as DraftElement["size"] })}>
              {(["S", "M", "L"] as const).map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <>
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t("length")}
              <DecimalInput value={element.length} onChange={(length) => onChange({ length })} />
            </label>
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t(isRoom ? "width" : "heightOrWidth")}
              <DecimalInput value={element.width} onChange={(width) => onChange({ width })} />
            </label>
            {isRoom && (
              <label className="flex flex-col gap-1 text-xs font-semibold">
                {t("height")}
                <DecimalInput value={element.height} onChange={(height) => onChange({ height })} placeholder={t("defaultHeight")} />
              </label>
            )}
          </>
        )}
        <label className="col-span-2 flex flex-col gap-1 text-xs font-semibold @md:col-span-1 @2xl:col-span-2">
          {t("paint")}
          <select className={selectClass} value={element.paintId} onChange={(e) => onChange({ paintId: e.target.value })}>
            {paints.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
        {isRoom && (
          <fieldset className="flex items-center gap-2">
            <legend className="sr-only">{t("walls")}</legend>
            <span aria-hidden="true" className="text-xs font-semibold text-muted-foreground">
              {t("walls")}
            </span>
            {(["A", "B", "C", "D"] as const).map((wall, w) => (
              <label key={wall} className="flex min-h-9 items-center gap-1">
                <input
                  type="checkbox"
                  className="size-4 accent-[var(--primary)]"
                  checked={element.walls?.[w] ?? false}
                  onChange={(e) => onChange({ walls: (element.walls ?? [false, false, false, false]).map((v, i) => (i === w ? e.target.checked : v)) })}
                />
                {wall}
              </label>
            ))}
          </fieldset>
        )}
        {isRoom && (
          <label className="flex min-h-9 items-center gap-1.5">
            <input id={`${id}-ceiling`} type="checkbox" className="size-4 accent-[var(--primary)]" checked={element.ceiling} onChange={(e) => onChange({ ceiling: e.target.checked })} />
            {t("ceiling")}
          </label>
        )}
        <label className="flex min-h-9 items-center gap-1.5">
          <input type="checkbox" className="size-4 accent-[var(--primary)]" checked={element.damage} onChange={(e) => onChange({ damage: e.target.checked })} />
          {t("damage")}
        </label>
      </div>
    </div>
  );
}

function AddRoom({ roomTypes, onAdd }: { roomTypes: Schemas["CatalogRoomType"][]; onAdd: (roomTypeId: string) => void }) {
  const t = useTranslations("admin.job.draft");
  const [roomType, setRoomType] = useState("");
  return (
    <div className="flex flex-wrap items-end gap-2">
      <label className="flex flex-col gap-1 text-xs font-semibold">
        {t("addRoom")}
        <select className={cn(selectClass, "w-48")} value={roomType} onChange={(e) => setRoomType(e.target.value)}>
          <option value="">{t("chooseRoomType")}</option>
          {roomTypes.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </label>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!roomType}
        aria-label={t("addRoom")}
        onClick={() => {
          onAdd(roomType);
          setRoomType("");
        }}
      >
        <Plus aria-hidden="true" />
        {t("add")}
      </Button>
    </div>
  );
}

/** A2.4: extras from the catalog (by key) or free, with client price and painter pay before VAT. */
function Extras({ request, onChange, readOnly }: { request: DraftQuoteRequest; onChange: (next: DraftQuoteRequest) => void; readOnly: boolean }) {
  const t = useTranslations("admin.job.draft");
  const locale = useLocale();
  const catalogExtras = useCatalogExtras();
  const [catalogKey, setCatalogKey] = useState("");
  const [free, setFree] = useState({ description: "", client: "", painter: "" });
  const extras = request.extras ?? [];
  const freeClient = parseEurosToCents(free.client);
  const freePainter = parseEurosToCents(free.painter);
  const extraName = (key: string) => {
    const item = catalogExtras.data?.find((x) => x.key === key);
    return item ? (item.name[locale as "pt" | "es" | "en"] ?? item.name.pt) : key;
  };

  return (
    <section aria-labelledby="extras-title" className="flex flex-col gap-2">
      <h3 id="extras-title" className="text-sm font-bold">
        {t("extras")}
      </h3>
      {extras.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("noExtras")}</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {extras.map((extra, i) => (
            <li key={i} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm">
              <span className="font-medium">{extra.catalogKey ? extraName(extra.catalogKey) : extra.description}</span>
              <span className="flex items-center gap-3 text-muted-foreground">
                {extra.clientPriceCents != null && t("extraPrices", { client: formatCents(locale, extra.clientPriceCents), painter: formatCents(locale, extra.painterPayCents ?? 0) })}
                {!readOnly && (
                  <Button type="button" variant="ghost" size="icon" aria-label={t("removeExtra")} onClick={() => onChange({ ...request, extras: extras.filter((_, j) => j !== i) })}>
                    <Trash2 aria-hidden="true" />
                  </Button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
      {!readOnly && (
        <div className="flex flex-col gap-3 rounded-xl bg-background p-3">
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t("catalogExtra")}
              <select className={cn(selectClass, "w-60")} value={catalogKey} onChange={(e) => setCatalogKey(e.target.value)}>
                <option value="">{t("chooseExtra")}</option>
                {(catalogExtras.data ?? [])
                  .filter((x) => x.isActive)
                  .map((x) => (
                    <option key={x.key} value={x.key}>
                      {x.name[locale as "pt" | "es" | "en"] ?? x.name.pt} · {formatCents(locale, x.clientPriceCents)}
                    </option>
                  ))}
              </select>
            </label>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={!catalogKey}
              aria-label={t("addCatalogExtra")}
              onClick={() => {
                const item = catalogExtras.data?.find((x) => x.key === catalogKey);
                onChange({
                  ...request,
                  extras: [...extras, { catalogKey, description: null, clientPriceCents: item?.clientPriceCents ?? null, painterPayCents: item?.painterPayCents ?? null }],
                });
                setCatalogKey("");
              }}
            >
              <Plus aria-hidden="true" />
              {t("add")}
            </Button>
          </div>
          <div className="grid gap-2 sm:grid-cols-[minmax(0,2fr)_1fr_1fr_auto] sm:items-end">
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t("freeExtra")}
              <input
                className={selectClass}
                maxLength={200}
                value={free.description}
                placeholder={t("freeExtraPlaceholder")}
                onChange={(e) => setFree({ ...free, description: e.target.value })}
              />
            </label>
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t("clientPrice")}
              <input className={selectClass} inputMode="decimal" value={free.client} aria-invalid={free.client !== "" && freeClient === null ? true : undefined} onChange={(e) => setFree({ ...free, client: e.target.value })} />
            </label>
            <label className="flex flex-col gap-1 text-xs font-semibold">
              {t("painterPay")}
              <input className={selectClass} inputMode="decimal" value={free.painter} aria-invalid={free.painter !== "" && freePainter === null ? true : undefined} onChange={(e) => setFree({ ...free, painter: e.target.value })} />
            </label>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={!free.description.trim() || freeClient === null || freePainter === null}
              aria-label={t("addFreeExtra")}
              onClick={() => {
                onChange({ ...request, extras: [...extras, { catalogKey: null, description: free.description.trim(), clientPriceCents: freeClient, painterPayCents: freePainter }] });
                setFree({ description: "", client: "", painter: "" });
              }}
            >
              <Plus aria-hidden="true" />
              {t("add")}
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">{t("beforeVat")}</p>
        </div>
      )}
    </section>
  );
}

/** A2.5 Premium by hand and A2.7 manual subtotal with its reason. */
function Adjustments({ request, onChange }: { request: DraftQuoteRequest; onChange: (next: DraftQuoteRequest) => void }) {
  const t = useTranslations("admin.job.draft");
  const id = useId();
  const [manualText, setManualText] = useState(request.manualSubtotalCents != null ? (request.manualSubtotalCents / 100).toFixed(2).replace(".", ",") : "");
  const manual = request.manualSubtotalCents != null || manualText !== "";

  return (
    <section aria-labelledby={`${id}-title`} className="flex flex-col gap-3">
      <h3 id={`${id}-title`} className="text-sm font-bold">
        {t("adjustments")}
      </h3>
      <label className="flex flex-col gap-1 text-xs font-semibold">
        {t("premium")}
        <select
          className={cn(selectClass, "w-48")}
          value={request.premium == null ? "auto" : request.premium ? "yes" : "no"}
          onChange={(e) => onChange({ ...request, premium: e.target.value === "auto" ? null : e.target.value === "yes" })}
        >
          <option value="auto">{t("premiumAuto")}</option>
          <option value="yes">{t("premiumYes")}</option>
          <option value="no">{t("premiumNo")}</option>
        </select>
      </label>
      <label className="flex min-h-9 items-center gap-2 text-sm font-medium">
        <input
          type="checkbox"
          className="size-4 accent-[var(--primary)]"
          checked={manual}
          onChange={(e) => {
            if (!e.target.checked) {
              setManualText("");
              onChange({ ...request, manualSubtotalCents: null, adjustmentReason: null });
            } else setManualText(" ");
          }}
        />
        {t("manualSubtotal")}
      </label>
      {manual && (
        <div className="grid gap-2 sm:grid-cols-[180px_minmax(0,1fr)]">
          <label className="flex flex-col gap-1 text-xs font-semibold">
            {t("subtotal")}
            <input
              className={selectClass}
              inputMode="decimal"
              value={manualText.trim()}
              aria-invalid={manualText.trim() !== "" && parseEurosToCents(manualText) === null ? true : undefined}
              onChange={(e) => {
                setManualText(e.target.value);
                const cents = parseEurosToCents(e.target.value);
                if (cents !== null) onChange({ ...request, manualSubtotalCents: cents });
              }}
            />
          </label>
          <label className="flex flex-col gap-1 text-xs font-semibold">
            {t("adjustmentReason")}
            <input
              className={selectClass}
              maxLength={500}
              value={request.adjustmentReason ?? ""}
              onChange={(e) => onChange({ ...request, adjustmentReason: e.target.value })}
            />
          </label>
        </div>
      )}
    </section>
  );
}
