"use client";

import { useLocale } from "next-intl";
import { formatEuros } from "@/lib/format";

/** "440 € – 570 €": the client's range, in whole euros (they are multiples of 5). */
export function PriceRange({ minCents, maxCents, className }: { minCents: number; maxCents: number; className?: string }) {
  const locale = useLocale();
  return (
    <span className={className}>
      {formatEuros(locale, minCents / 100, 0)} – {formatEuros(locale, maxCents / 100, 0)}
    </span>
  );
}
