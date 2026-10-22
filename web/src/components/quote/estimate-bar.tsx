"use client";

import { RefreshCw, WifiOff } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { formatArea } from "@/lib/format";
import type { Estimate } from "@/lib/quote/use-quote-data";
import { cn } from "@/lib/utils";
import { PriceRange } from "./price-range";

type EstimateBarProps = {
  described: number;
  total: number;
  /** Hundredths of m² of the described elements, computed in the browser. */
  area: number;
  estimate: Estimate | undefined;
  isStale: boolean;
  isError: boolean;
  isPaused: boolean;
  onRetry: () => void;
};

/** Described rooms, total m² and provisional range, live (docs/screens.md, screen 2; C2.4). */
export function EstimateSummary({ described, total, area, estimate, isStale, isError, isPaused, onRetry }: EstimateBarProps) {
  const t = useTranslations("quote.estimateBar");
  const locale = useLocale();

  return (
    <div className="flex items-end justify-between gap-3">
      <div className="min-w-0">
        <div className="text-xs text-muted-foreground">{t("described", { described, total })}</div>
        <div className="mt-0.5 text-[15px] font-semibold tabular-nums">{formatArea(locale, area)}</div>
      </div>
      <div aria-live="polite" aria-busy={isStale} className="min-w-0 text-right">
        <div className="text-xs text-muted-foreground">{t("provisional")}</div>
        {isPaused ? (
          <div className="mt-0.5 flex items-center justify-end gap-1.5 text-sm font-medium text-muted-foreground">
            <WifiOff aria-hidden="true" className="size-4" />
            {t("offline")}
          </div>
        ) : isError ? (
          <button type="button" onClick={onRetry} className="mt-0.5 inline-flex min-h-11 items-center gap-1.5 text-sm font-semibold text-destructive underline-offset-4 hover:underline">
            {t("error")}
            <RefreshCw aria-hidden="true" className="size-4" />
          </button>
        ) : !estimate ? (
          <div className="mt-0.5 text-sm text-muted-foreground">{described === 0 ? t("describeFirst") : t("calculating")}</div>
        ) : (
          <div className={cn("mt-0.5 font-display text-xl font-bold text-primary tabular-nums transition-opacity motion-reduce:transition-none", isStale && "opacity-50")}>
            {estimate.overMax || estimate.rangeMinCents == null || estimate.rangeMaxCents == null ? (
              <span className="text-base">{t("custom")}</span>
            ) : (
              <PriceRange minCents={estimate.rangeMinCents} maxCents={estimate.rangeMaxCents} />
            )}
          </div>
        )}
      </div>
    </div>
  );
}
