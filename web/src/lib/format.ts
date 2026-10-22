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
