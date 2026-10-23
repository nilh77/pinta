import { describe, expect, it } from "vitest";
import type { AdminQuote } from "./api";
import { parseEurosToCents, toDraftRequest, withMeasures } from "./draft";

const quote = {
  occupancy: "furnished",
  elements: [
    {
      id: "e1",
      kind: "room",
      name: "Sala",
      roomTypeId: "living",
      size: null,
      length: 5,
      width: 4,
      height: null,
      walls: [true, true, true, true],
      ceiling: true,
      surfaceTypeId: null,
      paintId: "matte",
      damage: false,
      clientKey: "room-1",
      areaM2: 58.8,
    },
  ],
  extras: [{ catalogKey: "anti_damp_treatment", description: "Tratamento anti-humidade", clientPriceCents: 12195, painterPayCents: 7500 }],
  premiumOverride: null,
  manualSubtotalCents: null,
  adjustmentReason: null,
} as unknown as AdminQuote;

describe("review draft (A2.3–A2.7)", () => {
  it("sends back the elements without ids or areas, keeping the client keys of the photos", () => {
    const request = toDraftRequest(quote);
    expect(request.elements[0]).not.toHaveProperty("id");
    expect(request.elements[0]).not.toHaveProperty("areaM2");
    expect(request.elements[0]?.clientKey).toBe("room-1");
    expect(request.extras?.[0]).toEqual({ catalogKey: "anti_damp_treatment", description: "Tratamento anti-humidade", clientPriceCents: 12195, painterPayCents: 7500 });
  });

  it("keeps a visit extra without client price unset until the admin prices it (P3.1)", () => {
    const fromVisit = { ...quote, extras: [{ catalogKey: null, description: "Fissura", clientPriceCents: 0, painterPayCents: 4000, clientPriceToSet: true }] } as AdminQuote;
    expect(toDraftRequest(fromVisit).extras?.[0]).toEqual({ catalogKey: null, description: "Fissura", clientPriceCents: null, painterPayCents: 4000 });
  });

  it("switches a room between size and measures", () => {
    const sized = withMeasures(toDraftRequest(quote).elements[0]!, undefined, 2.6);
    expect(sized).toMatchObject({ size: "M", length: null, width: null, height: null });
    expect(withMeasures(sized, { M: [4, 3.5] }, 2.6)).toMatchObject({ size: null, length: 4, width: 3.5, height: 2.6 });
  });

  it("reads euros typed with comma or dot", () => {
    expect(parseEurosToCents("2.500,00")).toBe(250000);
    expect(parseEurosToCents("2500.5")).toBe(250050);
    expect(parseEurosToCents("12,345")).toBeNull();
    expect(parseEurosToCents("")).toBeNull();
    expect(parseEurosToCents("abc")).toBeNull();
  });
});
