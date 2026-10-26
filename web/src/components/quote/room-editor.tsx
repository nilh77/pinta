"use client";

import { Check, Droplets } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState, type ReactNode } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { formatArea, formatMetres } from "@/lib/format";
import { parseDecimal } from "@/lib/numbers";
import {
  elementAreaOf,
  setMeasureMode,
  setRoomType,
  type CatalogInfo,
  type DraftElement,
  type ElementErrors,
  type RoomSize,
} from "@/lib/quote/draft";
import type { PublicCatalog } from "@/lib/quote/use-quote-data";
import { radioGroupKeyDown, rovingTabIndex } from "@/lib/arrow-keys";
import { cn } from "@/lib/utils";

type RoomEditorProps = {
  element: DraftElement;
  onChange: (element: DraftElement) => void;
  /** Shown only after the client tries to save (or immediately in the desktop editor). */
  errors: ElementErrors;
  catalog: PublicCatalog;
  info: CatalogInfo;
};

const WALLS = ["A", "B", "C", "D"] as const;

/**
 * RoomEditor (docs/screens.md, screen 3): top view with walls A–D and ceiling the client taps, size or exact measures,
 * paint with the suggestion and damage. Area updates instantly in the browser; never a price here (C2.2–C2.6).
 */
export function RoomEditor({ element, onChange, errors, catalog, info }: RoomEditorProps) {
  const t = useTranslations("quote.editor");
  const locale = useLocale();
  const area = elementAreaOf(element, info);
  const suggested = element.kind === "room" ? info.roomTypes[element.roomTypeId]?.suggestedPaintId : undefined;

  return (
    <div className="flex flex-col gap-6">
      {element.kind === "room" ? (
        <Field label={t("roomType")}>
          {(id) => (
            <select id={id} className={selectClass} value={element.roomTypeId} onChange={(e) => onChange(setRoomType(element, e.target.value, info))}>
              {catalog.roomTypes.map((room) => (
                <option key={room.id} value={room.id}>
                  {room.name}
                </option>
              ))}
            </select>
          )}
        </Field>
      ) : (
        <Field label={t("surfaceType")}>
          {(id) => (
            <select id={id} className={selectClass} value={element.surfaceTypeId} onChange={(e) => onChange({ ...element, surfaceTypeId: e.target.value })}>
              {catalog.surfaceTypes.map((surface) => (
                <option key={surface.id} value={surface.id}>
                  {surface.name}
                </option>
              ))}
            </select>
          )}
        </Field>
      )}

      {element.kind === "room" && <TopView element={element} onChange={onChange} info={info} error={errors.walls} />}

      <section aria-labelledby={`${element.id}-measures`} className="flex flex-col gap-3">
        <h3 id={`${element.id}-measures`} className="text-sm font-semibold">
          {t("measures")}
        </h3>
        {element.kind === "room" && (
          <div role="group" aria-label={t("measuresMode")} className="grid grid-cols-2 gap-1 rounded-2xl border border-border bg-card p-1">
            {(["size", "exact"] as const).map((mode) => (
              <button
                key={mode}
                type="button"
                aria-pressed={element.mode === mode}
                onClick={() => onChange(setMeasureMode(element, mode, info, locale))}
                className="h-10 rounded-xl text-sm font-medium text-muted-foreground transition-colors aria-pressed:bg-primary aria-pressed:font-semibold aria-pressed:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
              >
                {t(mode === "size" ? "approximateSize" : "exactMeasures")}
              </button>
            ))}
          </div>
        )}
        {element.kind === "room" && element.mode === "size" ? (
          <>
            <div role="radiogroup" aria-label={t("size")} onKeyDown={radioGroupKeyDown} className="grid grid-cols-3 gap-2">
              {(["S", "M", "L"] as const).map((size: RoomSize, i) => {
                const [length, width] = info.roomTypes[element.roomTypeId]?.sizes[size] ?? [0, 0];
                return (
                  <button
                    key={size}
                    type="button"
                    role="radio"
                    aria-checked={element.size === size}
                    tabIndex={rovingTabIndex(element.size === size, i, element.size != null)}
                    onClick={() => onChange({ ...element, size })}
                    className="flex h-16 flex-col items-center justify-center gap-0.5 rounded-xl border-[1.5px] border-border bg-card transition-colors aria-checked:border-2 aria-checked:border-primary aria-checked:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
                  >
                    <span className="text-sm font-semibold">{t(`sizes.${size}`)}</span>
                    <span className="text-xs text-muted-foreground">
                      ≈ {formatMetres(locale, length).replace(" m", "")} × {formatMetres(locale, width)}
                    </span>
                  </button>
                );
              })}
            </div>
            <p className="text-xs text-muted-foreground">{t("sizeHint", { height: formatMetres(locale, info.defaultHeight) })}</p>
          </>
        ) : (
          <div className={cn("grid gap-2", element.kind === "room" ? "grid-cols-3" : "grid-cols-2")}>
            <MeasureInput label={t("length")} value={element.length} error={errors.length} onChange={(length) => onChange({ ...element, length })} />
            <MeasureInput
              label={t(element.kind === "room" ? "width" : "surfaceHeight")}
              value={element.width}
              error={errors.width}
              onChange={(width) => onChange({ ...element, width })}
            />
            {element.kind === "room" && (
              <MeasureInput
                label={t("height")}
                value={element.height}
                placeholder={formatMetres(locale, info.defaultHeight).replace(" m", "")}
                error={errors.height}
                onChange={(height) => onChange({ ...element, height })}
              />
            )}
          </div>
        )}
      </section>

      <section aria-labelledby={`${element.id}-paint`} className="flex flex-col gap-2">
        <h3 id={`${element.id}-paint`} className="text-sm font-semibold">
          {t("paint")}
        </h3>
        <div role="radiogroup" aria-labelledby={`${element.id}-paint`} onKeyDown={radioGroupKeyDown} className="flex flex-col gap-2">
          {catalog.paints.map((paint, i) => (
            <button
              key={paint.id}
              type="button"
              role="radio"
              aria-checked={element.paintId === paint.id}
              tabIndex={rovingTabIndex(element.paintId === paint.id, i, catalog.paints.some((p) => p.id === element.paintId))}
              onClick={() => onChange({ ...element, paintId: paint.id, paintChosen: true })}
              className="flex min-h-12 items-center gap-3 rounded-2xl border-[1.5px] border-border bg-card px-3.5 py-2.5 text-left transition-colors aria-checked:border-2 aria-checked:border-primary aria-checked:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
            >
              <span className="flex-1 text-sm font-semibold">{paint.name}</span>
              {paint.id === suggested && (
                <span className="rounded-md bg-accent-soft px-2 py-0.5 text-[11px] font-semibold text-accent-ink">{t("suggested")}</span>
              )}
              <span
                aria-hidden="true"
                className={cn(
                  "flex size-5 items-center justify-center rounded-full border-2",
                  element.paintId === paint.id ? "border-primary bg-primary text-primary-foreground" : "border-[#b9c9c4]",
                )}
              >
                {element.paintId === paint.id && <Check className="size-3" strokeWidth={3.2} />}
              </span>
            </button>
          ))}
        </div>
      </section>

      <label htmlFor={`${element.id}-damage`} className="flex min-h-12 items-center gap-3 rounded-2xl border-[1.5px] border-border bg-card px-3.5 py-2.5">
        <Checkbox id={`${element.id}-damage`} checked={element.damage} onCheckedChange={(checked) => onChange({ ...element, damage: checked === true })} />
        <Droplets aria-hidden="true" className="size-4 text-accent-ink" />
        <span className="text-sm font-medium">{t("damage")}</span>
      </label>

      <div className="flex items-baseline justify-between rounded-2xl bg-primary-soft px-4 py-3">
        <span className="text-sm font-medium text-primary">{t("area")}</span>
        <span aria-live="polite" className="font-display text-xl font-bold text-primary tabular-nums">
          {area ? formatArea(locale, area.total) : "—"}
        </span>
      </div>
    </div>
  );
}

/** Walls A (top) and C (bottom) measure the length; B (right) and D (left) the width. The ceiling is the centre. */
function TopView({ element, onChange, info, error }: { element: DraftElement; onChange: (e: DraftElement) => void; info: CatalogInfo; error?: string }) {
  const t = useTranslations("quote.editor");
  const tRoot = useTranslations();
  const locale = useLocale();
  const [announcement, setAnnouncement] = useState("");
  const area = elementAreaOf(element, info);
  const dims =
    element.mode === "size"
      ? (info.roomTypes[element.roomTypeId]?.sizes[element.size] ?? [0, 0])
      : [parseDecimal(element.length) ?? 0, parseDecimal(element.width) ?? 0];
  const [length, width] = dims;
  // The drawing follows the room's proportions, within limits so it stays tappable.
  const ratio = length > 0 && width > 0 ? Math.min(1.6, Math.max(0.75, length / width)) : 1;
  const wallCount = element.walls.filter(Boolean).length;

  const toggleWall = (index: number) => {
    const walls = element.walls.map((on, i) => (i === index ? !on : on)) as DraftElement["walls"];
    onChange({ ...element, walls });
    setAnnouncement(t(walls[index] ? "announce.wallOn" : "announce.wallOff", { wall: WALLS[index] }));
  };
  const toggleCeiling = () => {
    onChange({ ...element, ceiling: !element.ceiling });
    setAnnouncement(t(!element.ceiling ? "announce.ceilingOn" : "announce.ceilingOff"));
  };
  const setAll = (on: boolean) => {
    onChange({ ...element, walls: [on, on, on, on] });
    setAnnouncement(t(on ? "announce.allOn" : "announce.allOff"));
  };

  const wallLabel = (index: number) => {
    const metres = index % 2 === 0 ? length : width;
    return metres > 0 ? `${WALLS[index]} · ${formatMetres(locale, metres)}` : WALLS[index];
  };
  const wallClass =
    "absolute flex items-center justify-center rounded-md text-[11px] font-semibold transition-[background-color,color,transform] duration-200 ease-out active:scale-95 focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none motion-reduce:transition-none aria-pressed:bg-accent aria-pressed:text-accent-foreground bg-[#e3ebe8] text-muted-foreground";

  return (
    <section aria-labelledby={`${element.id}-walls`} className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h3 id={`${element.id}-walls`} className="text-sm font-semibold">
          {t("wallsTitle")}
        </h3>
        <div className="flex gap-1">
          <button type="button" onClick={() => setAll(true)} className={smallButton}>
            {t("allWalls")}
          </button>
          <button type="button" onClick={() => setAll(false)} className={smallButton}>
            {t("noWalls")}
          </button>
        </div>
      </div>
      <p className="text-sm text-muted-foreground">{t("wallsHint")}</p>
      <div className="mx-auto w-full max-w-[300px]">
        <div className="relative w-full" style={{ aspectRatio: `${ratio}` }}>
          <button type="button" aria-pressed={element.walls[0]} onClick={() => toggleWall(0)} className={cn(wallClass, "top-0 right-6 left-6 h-6")}>
            <span className="sr-only">{t("wall")} </span>
            {wallLabel(0)}
          </button>
          <button type="button" aria-pressed={element.walls[1]} onClick={() => toggleWall(1)} className={cn(wallClass, "top-6 right-0 bottom-6 w-6 [writing-mode:vertical-rl]")}>
            <span className="sr-only">{t("wall")} </span>
            {wallLabel(1)}
          </button>
          <button type="button" aria-pressed={element.walls[2]} onClick={() => toggleWall(2)} className={cn(wallClass, "right-6 bottom-0 left-6 h-6")}>
            <span className="sr-only">{t("wall")} </span>
            {wallLabel(2)}
          </button>
          <button type="button" aria-pressed={element.walls[3]} onClick={() => toggleWall(3)} className={cn(wallClass, "top-6 bottom-6 left-0 w-6 [writing-mode:vertical-rl]")}>
            <span className="sr-only">{t("wall")} </span>
            {wallLabel(3)}
          </button>
          <button
            type="button"
            aria-pressed={element.ceiling}
            onClick={toggleCeiling}
            className="absolute inset-9 flex flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-[#b9c9c4] bg-card text-muted-foreground transition-[background-color,border-color,color] duration-200 ease-out focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none motion-reduce:transition-none aria-pressed:border-solid aria-pressed:border-primary aria-pressed:bg-primary-soft aria-pressed:text-primary"
          >
            <span className="text-sm font-semibold">{t("ceiling")}</span>
            <span className="text-xs tabular-nums">{element.ceiling && area ? formatArea(locale, area.ceiling) : t("notPainted")}</span>
          </button>
        </div>
      </div>
      <p className="text-center text-sm text-muted-foreground tabular-nums">
        {t("wallsSummary", { count: wallCount, area: area ? formatArea(locale, area.walls) : "—" })}
      </p>
      {error && (
        <p role="alert" className="text-sm font-medium text-destructive">
          {tRoot(error as never)}
        </p>
      )}
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </section>
  );
}

const selectClass =
  "h-12 w-full min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";
const smallButton =
  "h-9 rounded-lg px-2.5 text-xs font-semibold text-primary hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none";

function Field({ label, children }: { label: string; children: (id: string) => ReactNode }) {
  const id = useId();
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold">
        {label}
      </label>
      {children(id)}
    </div>
  );
}

function MeasureInput({
  label,
  value,
  error,
  placeholder,
  onChange,
}: {
  label: string;
  value: string;
  error?: string;
  placeholder?: string;
  onChange: (value: string) => void;
}) {
  const t = useTranslations();
  const id = useId();
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <label htmlFor={id} className="text-xs font-semibold text-muted-foreground">
        {label}
      </label>
      <input
        id={id}
        inputMode="decimal"
        autoComplete="off"
        value={value}
        placeholder={placeholder}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        onChange={(e) => onChange(e.target.value)}
        className="h-12 w-full min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base tabular-nums outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive"
      />
      {error && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {t(error as never)}
        </p>
      )}
    </div>
  );
}
