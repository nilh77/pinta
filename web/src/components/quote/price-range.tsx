"use client";

import { useLocale, useTranslations } from "next-intl";
import { formatCents, formatEuros, withoutVatCents } from "@/lib/format";
import { useCatalog } from "@/lib/quote/use-quote-data";
import { cn } from "@/lib/utils";

/** "440 € – 570 €": the client's range, in whole euros (they are multiples of 5). */
export function PriceRange({ minCents, maxCents, className }: { minCents: number; maxCents: number; className?: string }) {
  const locale = useLocale();
  return (
    <span className={className}>
      {formatEuros(locale, minCents / 100, 0)} – {formatEuros(locale, maxCents / 100, 0)}
    </span>
  );
}

/**
 * The same price without VAT, small, under the VAT-inclusive one (docs/screens.md, «Precios con y sin IVA»): the law
 * wants the VAT-inclusive price as the main figure for consumers. Ranges use the catalog's VAT; an exact net amount
 * (a confirmed quote's total minus its VAT) is passed as `netCents`.
 */
export function WithoutVat({
  minCents,
  maxCents,
  netCents,
  className,
}: {
  minCents?: number;
  maxCents?: number;
  netCents?: number;
  className?: string;
}) {
  const t = useTranslations("common");
  const locale = useLocale();
  const catalog = useCatalog();
  const vat = catalog.data?.vatPct;

  let text: string | null = null;
  if (netCents != null) text = formatCents(locale, netCents);
  else if (vat != null && minCents != null && maxCents != null)
    text = `${formatEuros(locale, Math.round(withoutVatCents(minCents, vat) / 100), 0)} – ${formatEuros(locale, Math.round(withoutVatCents(maxCents, vat) / 100), 0)}`;
  else if (vat != null && minCents != null) text = formatCents(locale, withoutVatCents(minCents, vat));
  if (!text) return null;

  return <span className={cn("block text-xs font-medium text-muted-foreground", className)}>{t("withoutVat", { amount: text })}</span>;
}
