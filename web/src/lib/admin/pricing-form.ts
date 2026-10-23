import { z } from "zod";
import { isValidTaxId } from "@/lib/quote/request";
import type { components } from "@/lib/api/schema";
import { centsFromInput, centsToInput, decimalPlaces, formatDecimalInput, parseDecimal } from "@/lib/numbers";

type Schemas = components["schemas"];
export type PaintDto = Schemas["PaintDto"];
export type SurfaceTypeDto = Schemas["SurfaceTypeDto"];
export type RoomTypeDto = Schemas["RoomTypeDto"];
export type ModifierDto = Schemas["ModifierDto"];
export type ExtraItemDto = Schemas["ExtraItemDto"];
export type SettingsDto = Schemas["SettingsDto"];

/** Everything the A1 screen edits, as loaded from the API. */
export type PricingCatalog = {
  paints: PaintDto[];
  surfaces: SurfaceTypeDto[];
  roomTypes: RoomTypeDto[];
  modifiers: ModifierDto[];
  extras: ExtraItemDto[];
  settings: SettingsDto;
};

// Same bounds as the API validators (Catalog Features/Admin, Settings). Messages are translation keys.
const KEY_PATTERN = /^[a-z][a-z0-9_]{1,39}$/;
const MIN_FACTOR = 0.5;
const MAX_FACTOR = 3;
const MIN_SIDE = 0.5;
const MAX_ROOM_SIDE = 30;

const number = (check: (value: number) => boolean, message: string) =>
  z.string().refine((text) => {
    const value = parseDecimal(text);
    return value !== null && decimalPlaces(text) <= 2 && check(value);
  }, message);

const amount = z.string().refine((text) => (centsFromInput(text) ?? -1) >= 0, "admin.validation.amount");
const factor = number((v) => v >= MIN_FACTOR && v <= MAX_FACTOR, "admin.validation.factor");
const percent = number((v) => v >= 0 && v <= 100, "admin.validation.percent");
const positive = number((v) => v > 0, "admin.validation.positive");
const side = number((v) => v >= MIN_SIDE && v <= MAX_ROOM_SIDE, "admin.validation.side");
const integer = (min: number, max: number) =>
  z.string().refine((text) => /^\d+$/.test(text.trim()) && +text >= min && +text <= max, "admin.validation.integer");

const name = z.object({
  pt: z.string().trim().min(1, "validation.required").max(120, "validation.tooLong"),
  es: z.string().max(120, "validation.tooLong"),
  en: z.string().max(120, "validation.tooLong"),
});

/** A row the API already has (id and version) or a new one (key typed by the admin). */
const row = {
  id: z.string().optional(),
  version: z.number().optional(),
  key: z.string(),
  name,
  isActive: z.boolean(),
};

/** New rows need a valid key; saved rows keep theirs. */
const keyRule: [(value: { id?: string; key: string }) => boolean, { message: string; path: string[] }] = [
  (value) => value.id !== undefined || KEY_PATTERN.test(value.key),
  { message: "admin.validation.key", path: ["key"] },
];

export const pricingFormSchema = z.object({
  paints: z.array(z.object({ ...row, pricePerLitre: amount, coverage: positive, coats: integer(1, 5) }).refine(...keyRule)),
  surfaces: z.array(z.object({ ...row, clientRate: amount, painterRate: amount, factor }).refine(...keyRule)),
  roomTypes: z.array(
    z.object({
      ...row,
      sizes: z.object({
        s: z.object({ length: side, width: side }),
        m: z.object({ length: side, width: side }),
        l: z.object({ length: side, width: side }),
      }),
      suggestedPaintId: z.string().min(1, "validation.required"),
    }),
  ),
  modifiers: z.array(z.object({ ...row, factor })),
  extras: z.array(z.object({ ...row, clientPrice: amount, painterPay: amount }).refine(...keyRule)),
  settings: z
    .object({
      version: z.number(),
      wastePct: percent,
      vatPct: percent,
      minJobAmount: amount,
      maxJobAmount: amount,
      premiumAreaM2: positive,
      rangeLowPct: percent,
      rangeHighPct: percent,
      rangeSizeWideningPct: percent,
      rangeDamageWideningPct: percent,
      depositPct: percent,
      minMarginPct: percent,
      defaultHeight: number((v) => v >= 2 && v <= 6, "admin.validation.height"),
      quoteValidityDays: integer(1, 365),
      minLeadBusinessDays: integer(0, 30),
      siteVisitPrice: amount,
      siteVisitDeductible: z.boolean(),
      reviewDeadlineBusinessDays: integer(1, 30),
      fullRefundMinDays: integer(0, 365),
      lateRefundPct: percent,
      companyName: z.string().trim().min(1, "validation.required").max(200, "validation.tooLong"),
      companyTaxId: z.string().trim().refine(isValidTaxId, "quote.errors.taxId"),
      companyAddress: z.string().trim().min(1, "validation.required").max(300, "validation.tooLong"),
    })
    .refine((s) => (centsFromInput(s.minJobAmount) ?? 0) < (centsFromInput(s.maxJobAmount) ?? 0), {
      message: "admin.validation.minBelowMax",
      path: ["maxJobAmount"],
    }),
});

export type PricingFormValues = z.infer<typeof pricingFormSchema>;
export type SettingsFormValues = PricingFormValues["settings"];

const nameValues = (n: Schemas["LocalizedTextDto"]) => ({ pt: n.pt, es: n.es ?? "", en: n.en ?? "" });
const nameDto = (n: { pt: string; es: string; en: string }) => ({ pt: n.pt.trim(), es: n.es.trim() || null, en: n.en.trim() || null });
const dec = (locale: string, value: number) => formatDecimalInput(locale, value);
const num = (text: string) => parseDecimal(text) ?? 0;
const cents = (text: string) => centsFromInput(text) ?? 0;
const base = (dto: { id: string; version: number; key: string; name: Schemas["LocalizedTextDto"]; isActive: boolean }) => ({
  id: dto.id,
  version: dto.version,
  key: dto.key,
  name: nameValues(dto.name),
  isActive: dto.isActive,
});

/** API data → form values; numbers are shown with the decimal separator of the UI language. */
export function toFormValues(catalog: PricingCatalog, locale: string): PricingFormValues {
  const s = catalog.settings;
  return {
    paints: catalog.paints.map((p) => ({
      ...base(p),
      pricePerLitre: centsToInput(locale, p.pricePerLitreCents),
      coverage: dec(locale, p.coverageM2PerLitre),
      coats: String(p.coats),
    })),
    surfaces: catalog.surfaces.map((x) => ({
      ...base(x),
      clientRate: centsToInput(locale, x.clientRateCents),
      painterRate: centsToInput(locale, x.painterRateCents),
      factor: dec(locale, x.difficultyFactor),
    })),
    roomTypes: catalog.roomTypes.map((r) => ({
      ...base(r),
      sizes: {
        s: { length: dec(locale, r.sizes.s.length), width: dec(locale, r.sizes.s.width) },
        m: { length: dec(locale, r.sizes.m.length), width: dec(locale, r.sizes.m.width) },
        l: { length: dec(locale, r.sizes.l.length), width: dec(locale, r.sizes.l.width) },
      },
      suggestedPaintId: r.suggestedPaintId,
    })),
    modifiers: catalog.modifiers.map((m) => ({ ...base(m), factor: dec(locale, m.factor) })),
    extras: catalog.extras.map((x) => ({
      ...base(x),
      clientPrice: centsToInput(locale, x.clientPriceCents),
      painterPay: centsToInput(locale, x.painterPayCents),
    })),
    settings: {
      version: s.version,
      wastePct: dec(locale, s.wastePct),
      vatPct: dec(locale, s.vatPct),
      minJobAmount: centsToInput(locale, s.minJobAmountCents),
      maxJobAmount: centsToInput(locale, s.maxJobAmountCents),
      premiumAreaM2: dec(locale, s.premiumAreaM2),
      rangeLowPct: dec(locale, s.rangeLowPct),
      rangeHighPct: dec(locale, s.rangeHighPct),
      rangeSizeWideningPct: dec(locale, s.rangeSizeWideningPct),
      rangeDamageWideningPct: dec(locale, s.rangeDamageWideningPct),
      depositPct: dec(locale, s.depositPct),
      minMarginPct: dec(locale, s.minMarginPct),
      defaultHeight: dec(locale, s.defaultHeight),
      quoteValidityDays: String(s.quoteValidityDays),
      minLeadBusinessDays: String(s.minLeadBusinessDays),
      siteVisitPrice: centsToInput(locale, s.siteVisitPriceCents),
      siteVisitDeductible: s.siteVisitDeductible,
      reviewDeadlineBusinessDays: String(s.reviewDeadlineBusinessDays),
      fullRefundMinDays: String(s.fullRefundMinDays),
      lateRefundPct: dec(locale, s.lateRefundPct),
      companyName: s.companyName,
      companyTaxId: s.companyTaxId,
      companyAddress: s.companyAddress,
    },
  };
}

type Row = PricingFormValues["paints"][number];

/** Fields shared by every catalog request: the key only on create, the version only on edit. */
const requestBase = (row: Omit<Row, "pricePerLitre" | "coverage" | "coats">) => ({
  key: row.id ? null : row.key,
  version: row.id ? (row.version ?? null) : null,
  name: nameDto(row.name),
  isActive: row.isActive,
});

export const toPaintRequest = (p: PricingFormValues["paints"][number]): Schemas["PaintRequest"] => ({
  ...requestBase(p),
  pricePerLitreCents: cents(p.pricePerLitre),
  coverageM2PerLitre: num(p.coverage),
  coats: Number(p.coats),
});

export const toSurfaceRequest = (x: PricingFormValues["surfaces"][number]): Schemas["SurfaceTypeRequest"] => ({
  ...requestBase(x),
  clientRateCents: cents(x.clientRate),
  painterRateCents: cents(x.painterRate),
  difficultyFactor: num(x.factor),
});

export const toRoomTypeRequest = (r: PricingFormValues["roomTypes"][number]): Schemas["RoomTypeRequest"] => ({
  ...requestBase(r),
  sizes: {
    s: { length: num(r.sizes.s.length), width: num(r.sizes.s.width) },
    m: { length: num(r.sizes.m.length), width: num(r.sizes.m.width) },
    l: { length: num(r.sizes.l.length), width: num(r.sizes.l.width) },
  },
  suggestedPaintId: r.suggestedPaintId,
});

export const toModifierRequest = (m: PricingFormValues["modifiers"][number]): Schemas["ModifierRequest"] => ({
  ...requestBase(m),
  factor: num(m.factor),
});

export const toExtraRequest = (x: PricingFormValues["extras"][number]): Schemas["ExtraItemRequest"] => ({
  ...requestBase(x),
  clientPriceCents: cents(x.clientPrice),
  painterPayCents: cents(x.painterPay),
});

/** The settings PUT sends every field; the ones A1 does not edit keep their loaded values. */
export const toSettingsRequest = (s: SettingsFormValues, loaded: SettingsDto): SettingsDto => ({
  ...loaded,
  version: s.version,
  wastePct: num(s.wastePct),
  vatPct: num(s.vatPct),
  minJobAmountCents: cents(s.minJobAmount),
  maxJobAmountCents: cents(s.maxJobAmount),
  premiumAreaM2: num(s.premiumAreaM2),
  rangeLowPct: num(s.rangeLowPct),
  rangeHighPct: num(s.rangeHighPct),
  rangeSizeWideningPct: num(s.rangeSizeWideningPct),
  rangeDamageWideningPct: num(s.rangeDamageWideningPct),
  depositPct: num(s.depositPct),
  minMarginPct: num(s.minMarginPct),
  defaultHeight: num(s.defaultHeight),
  quoteValidityDays: Number(s.quoteValidityDays),
  minLeadBusinessDays: Number(s.minLeadBusinessDays),
  siteVisitPriceCents: cents(s.siteVisitPrice),
  siteVisitDeductible: s.siteVisitDeductible,
  reviewDeadlineBusinessDays: Number(s.reviewDeadlineBusinessDays),
  fullRefundMinDays: Number(s.fullRefundMinDays),
  lateRefundPct: num(s.lateRefundPct),
  companyName: s.companyName.trim(),
  companyTaxId: s.companyTaxId.trim(),
  companyAddress: s.companyAddress.trim(),
});

/** Labour margin of a surface type: share of the client rate that is not painter pay (board "Margen MO"). */
export function labourMarginPct(clientRate: string, painterRate: string): number | null {
  const client = centsFromInput(clientRate);
  const painter = centsFromInput(painterRate);
  if (client === null || painter === null || client === 0) return null;
  return Math.round(((client - painter) / client) * 100);
}

/** The text of a translatable name in the UI language, falling back to Portuguese like the API. */
export function localName(name: { pt: string; es?: string | null; en?: string | null }, locale: string): string {
  const text = locale === "es" ? name.es : locale === "en" ? name.en : null;
  return text?.trim() ? text : name.pt;
}
