import type { components } from "@/lib/api/schema";
import { toEstimateRequest, type CatalogInfo, type Draft, type DraftDetails } from "./draft";

export type SubmitJobRequest = components["schemas"]["SubmitJobRequest"];

/** Same rules as the API (Jobs SubmitJobRequestValidator, Catalog PostalCode). */
export const POSTAL_CODE = /^\d{4}-\d{3}$/;
export const MAX_DESCRIPTION = 2000;
export const MAX_RANGE_DAYS = 60;

/** Today's date in Lisbon as yyyy-mm-dd (dates are scheduled in Europe/Lisbon). */
export function lisbonToday(now = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Lisbon", year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}

const toDate = (iso: string) => new Date(`${iso}T12:00:00Z`);
const toIso = (date: Date) => date.toISOString().slice(0, 10);

/** Working days are Monday to Friday, no public holidays in the MVP (docs/screens.md, screen 6). */
export function addBusinessDays(iso: string, days: number): string {
  const date = toDate(iso);
  while (days > 0) {
    date.setUTCDate(date.getUTCDate() + 1);
    if (date.getUTCDay() !== 0 && date.getUTCDay() !== 6) days--;
  }
  return toIso(date);
}

export const addDays = (iso: string, days: number) => {
  const date = toDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return toIso(date);
};

const daysBetween = (from: string, to: string) => Math.round((toDate(to).getTime() - toDate(from).getTime()) / 86_400_000);

/** Field → translation key (quote.errors.*). */
export type DetailsErrors = Partial<Record<"line1" | "postalCode" | "locality" | "dates" | "from" | "to" | "taxId" | "companyName", string>>;

export function validateAddress(address: DraftDetails["address"]): DetailsErrors {
  const errors: DetailsErrors = {};
  if (!address.line1.trim()) errors.line1 = "quote.errors.required";
  if (!POSTAL_CODE.test(address.postalCode.trim())) errors.postalCode = "quote.errors.postalCode";
  if (!address.locality.trim()) errors.locality = "quote.errors.required";
  return errors;
}

export function validateDates(dates: DraftDetails["dates"], earliest: string): DetailsErrors {
  if (dates.asap === true) return {};
  if (dates.asap === null) return { dates: "quote.errors.datesChoose" };
  const errors: DetailsErrors = {};
  if (!dates.from) errors.from = "quote.errors.required";
  else if (dates.from < earliest) errors.from = "quote.errors.datesTooSoon";
  if (!dates.to) errors.to = "quote.errors.required";
  else if (dates.from && dates.to < dates.from) errors.to = "quote.errors.datesOrder";
  else if (dates.from && daysBetween(dates.from, dates.to) > MAX_RANGE_DAYS) errors.to = "quote.errors.datesTooLong";
  return errors;
}

/** Portuguese NIF: 9 digits and a mod-11 check digit (same as SharedKernel TaxId). */
export function isValidTaxId(value: string): boolean {
  if (!/^\d{9}$/.test(value) || value[0] === "0" || (value[0] === "4" && value[1] !== "5")) return false;
  const sum = [...value.slice(0, 8)].reduce((total, digit, i) => total + Number(digit) * (9 - i), 0);
  const remainder = sum % 11;
  return Number(value[8]) === (remainder < 2 ? 0 : 11 - remainder);
}

export function validateBilling(billing: DraftDetails["billing"]): DetailsErrors {
  if (!billing.wanted) return {};
  const errors: DetailsErrors = {};
  if (!isValidTaxId(billing.taxId.trim())) errors.taxId = "quote.errors.taxId";
  if (!billing.companyName.trim()) errors.companyName = "quote.errors.required";
  return errors;
}

/** POST /api/jobs body from the draft; null while nothing can be priced. */
export function toSubmitRequest(draft: Draft, info: CatalogInfo): SubmitJobRequest | null {
  const estimate = toEstimateRequest(draft, info);
  if (!estimate) return null;
  const { address, dates, billing, description } = draft.details;
  const blank = (text: string) => (text.trim() ? text.trim() : null);
  return {
    draftId: draft.details.draftId,
    occupancy: estimate.occupancy,
    elements: estimate.elements,
    description: blank(description),
    address: {
      line1: address.line1.trim(),
      floor: blank(address.floor),
      postalCode: address.postalCode.trim(),
      locality: address.locality.trim(),
      accessNotes: blank(address.accessNotes),
    },
    preferredDates: dates.asap ? { asap: true, from: null, to: null } : { asap: false, from: dates.from || null, to: dates.to || null },
    billing: billing.wanted ? { taxId: billing.taxId.trim(), companyName: billing.companyName.trim() } : null,
  };
}

/** Elements that will be sent, with how many have at least one photo or video (screen 5 footer). */
export function photoCoverage(draft: Draft, info: CatalogInfo): { withPhotos: number; total: number } {
  const keys = (toEstimateRequest(draft, info)?.elements ?? []).map((e) => e.clientKey!);
  const withPhotos = keys.filter((key) => (draft.details.files[key] ?? []).some((f) => f.status === "done")).length;
  return { withPhotos, total: keys.length };
}
