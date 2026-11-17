/**
 * Plain calendar arithmetic on ISO dates ("2026-11-17") for the date field (docs/screens.md, «Formularios»). Dates are
 * calendar days, not instants: everything runs in UTC so no time zone can shift a day.
 */

export type IsoDate = string;

const toDate = (iso: IsoDate) => new Date(`${iso}T00:00:00Z`);
const toIso = (date: Date): IsoDate => date.toISOString().slice(0, 10);

export const isIsoDate = (value: string): value is IsoDate => /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(toDate(value).getTime());

export function addDays(iso: IsoDate, days: number): IsoDate {
  const date = toDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return toIso(date);
}

/** The same day `months` later, kept inside the target month (31 Jan + 1 month → 28 Feb). */
export function addMonths(iso: IsoDate, months: number): IsoDate {
  const date = toDate(iso);
  const day = date.getUTCDate();
  date.setUTCDate(1);
  date.setUTCMonth(date.getUTCMonth() + months);
  const last = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
  date.setUTCDate(Math.min(day, last));
  return toIso(date);
}

/** The first day of the month that holds `iso`. */
export const startOfMonth = (iso: IsoDate): IsoDate => `${iso.slice(0, 7)}-01`;

/** Monday = 0 … Sunday = 6, as Portuguese and Spanish calendars start the week on Monday. */
export const weekday = (iso: IsoDate) => (toDate(iso).getUTCDay() + 6) % 7;

/** The month as rows of 7 days from Monday; days of the neighbouring months fill the first and last rows. */
export function monthGrid(monthStart: IsoDate): IsoDate[][] {
  const first = addDays(monthStart, -weekday(monthStart));
  const nextMonth = addMonths(monthStart, 1);
  const weeks: IsoDate[][] = [];
  for (let monday = first; monday < nextMonth; monday = addDays(monday, 7)) {
    weeks.push(Array.from({ length: 7 }, (_, i) => addDays(monday, i)));
  }
  return weeks;
}

/** Whether a day can be picked: within `min` and `max` when they are given. */
export const isSelectable = (iso: IsoDate, min?: IsoDate, max?: IsoDate) => (!min || iso >= min) && (!max || iso <= max);

/** Keeps a day within `min` and `max` (used when the keyboard moves past them). */
export function clamp(iso: IsoDate, min?: IsoDate, max?: IsoDate): IsoDate {
  if (min && iso < min) return min;
  if (max && iso > max) return max;
  return iso;
}

/** The weekday initials of a language, Monday first ("S", "T", "Q"… in pt). */
export function weekdayNames(locale: string, width: "narrow" | "short" | "long" = "narrow"): string[] {
  const format = new Intl.DateTimeFormat(locale, { weekday: width, timeZone: "UTC" });
  // 2024-01-01 was a Monday.
  return Array.from({ length: 7 }, (_, i) => format.format(toDate(addDays("2024-01-01", i))));
}

export const formatMonth = (locale: string, iso: IsoDate) =>
  new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(toDate(iso));

export const formatLongDate = (locale: string, iso: IsoDate) =>
  new Intl.DateTimeFormat(locale, { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }).format(toDate(iso));

export const formatFieldDate = (locale: string, iso: IsoDate) =>
  new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).format(toDate(iso));

export const dayOfMonth = (iso: IsoDate) => Number(iso.slice(8, 10));
