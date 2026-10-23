/**
 * Number format per UI language. PT uses the es-ES format: docs/integrations.md asks for `1.270,00 €` in PT and ES,
 * while CLDR pt-PT groups thousands with a space (`1 270,00 €`).
 */
const INTL_LOCALES = { pt: "es-ES", es: "es-ES", en: "en-GB" } as const;

/**
 * Euros in the format of each language (docs/integrations.md: `1.270,00 €` in PT and ES).
 * `useGrouping: "always"` because es-ES skips the thousands separator on 4-digit amounts.
 */
export function formatEuros(locale: string, euros: number, fractionDigits = 2): string {
  return new Intl.NumberFormat(INTL_LOCALES[locale as keyof typeof INTL_LOCALES] ?? INTL_LOCALES.pt, {
    style: "currency",
    currency: "EUR",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
    useGrouping: "always",
  }).format(euros);
}

/** m² with 2 decimals in the format of each language ("48,40 m²" in PT and ES). Takes hundredths of m² (lib/quote/area). */
export function formatArea(locale: string, hundredths: number): string {
  return `${new Intl.NumberFormat(INTL_LOCALES[locale as keyof typeof INTL_LOCALES] ?? INTL_LOCALES.pt, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
    useGrouping: "always",
  }).format(hundredths / 100)} m²`;
}

/** Metres for labels ("3,50 m"). */
export function formatMetres(locale: string, metres: number): string {
  return `${new Intl.NumberFormat(INTL_LOCALES[locale as keyof typeof INTL_LOCALES] ?? INTL_LOCALES.pt, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(metres)} m`;
}

const DATE_LOCALES = { pt: "pt-PT", es: "es-ES", en: "en-GB" } as const;
const dateLocale = (locale: string) => DATE_LOCALES[locale as keyof typeof DATE_LOCALES] ?? DATE_LOCALES.pt;

/** API amounts are integer cents. */
export function formatCents(locale: string, cents: number): string {
  return formatEuros(locale, cents / 100);
}

/** A date without time from the API ("2026-11-06") as "6 de novembro de 2026"; `short` drops the year. */
export function formatDate(locale: string, isoDate: string, short = false): string {
  return new Intl.DateTimeFormat(dateLocale(locale), { day: "numeric", month: "long", ...(short ? {} : { year: "numeric" }), timeZone: "UTC" }).format(
    new Date(`${isoDate.slice(0, 10)}T00:00:00Z`),
  );
}

/** An instant from the API shown in Lisbon time (docs: display in Europe/Lisbon). */
export function formatDateTime(locale: string, isoInstant: string): string {
  return new Intl.DateTimeFormat(dateLocale(locale), {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Europe/Lisbon",
  }).format(new Date(isoInstant));
}

/** Whole days from today (Lisbon) to a date; negative once it has passed. */
export function daysUntil(isoDate: string, now: Date = new Date()): number {
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Lisbon" }).format(now);
  return Math.round((Date.parse(`${isoDate.slice(0, 10)}T00:00:00Z`) - Date.parse(`${today}T00:00:00Z`)) / 86_400_000);
}
