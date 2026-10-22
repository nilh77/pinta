import { describe, expect, it } from "vitest";
import {
  labourMarginPct,
  localName,
  pricingFormSchema,
  toFormValues,
  toPaintRequest,
  toSettingsRequest,
  type PricingCatalog,
} from "./pricing-form";
import { defaultSampleJob, newSurface, toSimulateRequest } from "./simulator";

const name = { pt: "Tinta plástica mate", es: "Plástica mate", en: null };
const settings = {
  wastePct: 10, vatPct: 23, minJobAmountCents: 15000, maxJobAmountCents: 1500000, premiumAreaM2: 250,
  rangeLowPct: 10, rangeHighPct: 15, rangeSizeWideningPct: 10, rangeDamageWideningPct: 10, depositPct: 30,
  minMarginPct: 20, defaultHeight: 2.6, quoteValidityDays: 15, siteVisitPriceCents: 0, siteVisitDeductible: true,
  reviewDeadlineBusinessDays: 2, fullRefundMinDays: 7, lateRefundPct: 50, maxPhotosPerElement: 10,
  maxVideosPerElement: 2, maxImageSizeMb: 10, maxVideoSizeMb: 100, clientPhoneVisibleToPainter: true,
  painterPhoneVisibleToClient: true, version: 7, updatedAt: "2026-10-22T10:00:00Z",
};
const catalog: PricingCatalog = {
  paints: [{ id: "p1", key: "matte", name, pricePerLitreCents: 800, coverageM2PerLitre: 10, coats: 2, isActive: true, version: 3, updatedAt: "" }],
  surfaces: [],
  roomTypes: [],
  modifiers: [],
  extras: [],
  settings,
};

describe("pricing form", () => {
  it("shows amounts in euros with the UI decimal separator and round-trips to cents", () => {
    const values = toFormValues(catalog, "pt");
    expect(values.paints[0].pricePerLitre).toBe("8,00");
    expect(values.settings.maxJobAmount).toBe("15000,00");

    const request = toPaintRequest({ ...values.paints[0], pricePerLitre: "8,50" });
    expect(request).toEqual({
      key: null,
      version: 3,
      name: { pt: "Tinta plástica mate", es: "Plástica mate", en: null },
      isActive: true,
      pricePerLitreCents: 850,
      coverageM2PerLitre: 10,
      coats: 2,
    });
    expect(toSettingsRequest(values.settings, settings)).toEqual(settings);
  });

  it("sends the key only for new rows", () => {
    const request = toPaintRequest({
      key: "enamel", name: { pt: "Esmalte", es: "", en: "" }, isActive: true, pricePerLitre: "12", coverage: "12,5", coats: "2",
    });
    expect(request.key).toBe("enamel");
    expect(request.version).toBeNull();
    expect(request.coverageM2PerLitre).toBe(12.5);
  });

  it("validates like the API", () => {
    const values = toFormValues(catalog, "pt");
    values.paints[0].pricePerLitre = "-1";
    values.paints.push({ key: "Bad key", name: { pt: "", es: "", en: "" }, isActive: true, pricePerLitre: "1", coverage: "0", coats: "9" });
    values.settings.maxJobAmount = "100";

    const result = pricingFormSchema.safeParse(values);

    expect(result.success).toBe(false);
    const issues = result.error!.issues.map((i) => `${i.path.join(".")}:${i.message}`);
    expect(issues).toEqual(
      expect.arrayContaining([
        "paints.0.pricePerLitre:admin.validation.amount",
        "paints.1.name.pt:validation.required",
        "paints.1.coverage:admin.validation.positive",
        "paints.1.coats:admin.validation.integer",
        "paints.1.key:admin.validation.key",
        "settings.maxJobAmount:admin.validation.minBelowMax",
      ]),
    );
  });

  it("computes the labour margin of a surface", () => {
    expect(labourMarginPct("6,00", "4,00")).toBe(33);
    expect(labourMarginPct("0", "4")).toBeNull();
  });

  it("falls back to Portuguese names", () => {
    expect(localName(name, "es")).toBe("Plástica mate");
    expect(localName(name, "en")).toBe("Tinta plástica mate");
  });
});

describe("simulator request", () => {
  it("starts as golden case TC01", () => {
    expect(toSimulateRequest(defaultSampleJob())).toEqual({
      occupancy: "empty",
      elements: [
        {
          kind: "room", name: null, roomTypeId: "bedroom", size: null, length: 4, width: 3, height: null,
          walls: [true, true, true, true], ceiling: true, surfaceTypeId: null, paintId: "matte", damage: false,
        },
      ],
      extras: [],
    });
  });

  it("sends sizes without measures and loose surfaces without walls", () => {
    const job = defaultSampleJob();
    job.elements[0].size = "M";
    job.elements.push({ ...newSurface(), surfaceTypeId: "facade", length: "12", width: "6" });
    job.extras.push({ description: "Anti-humidade", clientPrice: "121,95", painterPay: "75" });

    const request = toSimulateRequest(job);

    expect(request.elements[0]).toMatchObject({ size: "M", length: null, width: null });
    expect(request.elements[1]).toMatchObject({ kind: "surface", surfaceTypeId: "facade", length: 12, width: 6, walls: null });
    expect(request.extras).toEqual([{ description: "Anti-humidade", clientPriceCents: 12195, painterPayCents: 7500 }]);
  });
});
