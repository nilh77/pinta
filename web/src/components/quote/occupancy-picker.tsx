"use client";

import { useTranslations } from "next-intl";
import type { Occupancy } from "@/lib/quote/draft";
import type { PublicCatalog } from "@/lib/quote/use-quote-data";

/** Empty, furnished or lived in, with why it changes the price (docs/screens.md, screen 2). */
export function OccupancyPicker({
  catalog,
  value,
  onChange,
  error,
}: {
  catalog: PublicCatalog;
  value: Occupancy | null;
  onChange: (value: Occupancy) => void;
  error?: boolean;
}) {
  const t = useTranslations("quote.occupancy");
  return (
    <fieldset id="occupancy" className="flex flex-col gap-2" aria-describedby={error ? "occupancy-error" : "occupancy-hint"}>
      <legend className="mb-2 text-sm font-semibold">{t("question")}</legend>
      <div className="grid grid-cols-3 gap-1 rounded-2xl border border-border bg-card p-1 aria-invalid:border-destructive" aria-invalid={error || undefined}>
        {catalog.occupancies.map((option) => (
          <button
            key={option.id}
            type="button"
            aria-pressed={value === option.id}
            onClick={() => onChange(option.id as Occupancy)}
            className="h-11 rounded-xl px-1 text-[13px] font-medium text-muted-foreground transition-colors aria-pressed:bg-primary aria-pressed:font-semibold aria-pressed:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
          >
            {option.name}
          </button>
        ))}
      </div>
      {error ? (
        <p id="occupancy-error" role="alert" className="text-sm font-medium text-destructive">
          {t("required")}
        </p>
      ) : (
        <p id="occupancy-hint" className="text-xs text-muted-foreground">
          {t("hint")}
        </p>
      )}
    </fieldset>
  );
}
