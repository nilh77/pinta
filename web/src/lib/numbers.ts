/**
 * Decimal inputs (admin screens and the quote builder). People type "8,00" or "8.00"; the API wants cents (integers) for amounts and
 * plain numbers for measures, factors and percentages (docs/api.md).
 */

/** Parses "1.234,56", "1234.56", "8,5" or " 8 "; null when it is not a number. */
export function parseDecimal(text: string): number | null {
  const value = text.trim().replace(/\s/g, "");
  if (!/^-?[\d.,]+$/.test(value)) return null;
  const lastComma = value.lastIndexOf(",");
  const lastDot = value.lastIndexOf(".");
  const decimalSeparator = lastComma > lastDot ? "," : ".";
  const thousands = decimalSeparator === "," ? "." : ",";
  // With a single separator followed by exactly three digits ("1.250"), it is still read as a decimal separator.
  const normalised = (lastComma >= 0 && lastDot >= 0 ? value.split(thousands).join("") : value).replace(",", ".");
  if (!/^-?\d+(\.\d+)?$/.test(normalised)) return null;
  return Number(normalised);
}

/** Decimal places of what was typed, e.g. "8,125" → 3. */
export function decimalPlaces(text: string): number {
  const value = text.trim();
  const separator = Math.max(value.lastIndexOf(","), value.lastIndexOf("."));
  return separator < 0 ? 0 : value.length - separator - 1;
}

/** Euros typed with at most 2 decimals → cents; null otherwise. */
export function centsFromInput(text: string): number | null {
  const euros = parseDecimal(text);
  if (euros === null || decimalPlaces(text) > 2) return null;
  return Math.round(euros * 100);
}

const DECIMAL_LOCALES = { pt: "es-ES", es: "es-ES", en: "en-GB" } as const;

/** A number as the admin types it in each language: "8,00" in pt and es, "8.00" in en. No thousands separator. */
export function formatDecimalInput(locale: string, value: number, fractionDigits = 2): string {
  return new Intl.NumberFormat(DECIMAL_LOCALES[locale as keyof typeof DECIMAL_LOCALES] ?? DECIMAL_LOCALES.pt, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
    useGrouping: false,
  }).format(value);
}

export function centsToInput(locale: string, cents: number): string {
  return formatDecimalInput(locale, cents / 100);
}
