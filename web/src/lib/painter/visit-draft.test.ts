import { describe, expect, it } from "vitest";
import type { CatalogInfo } from "@/lib/quote/draft";
import { firstInvalid, startVisitDraft, toReport, type VisitDetail } from "./visit-draft";

const info: CatalogInfo = {
  defaultHeight: 2.6,
  defaultPaintId: "matte",
  defaultSurfaceTypeId: "interior_wall",
  roomTypes: { bedroom: { suggestedPaintId: "matte", sizes: { S: [3, 2.5], M: [3.5, 3], L: [4.5, 4] } } },
};

const visit = {
  occupancy: "empty",
  elements: [
    {
      id: "e1",
      kind: "room",
      name: null,
      roomTypeId: "bedroom",
      size: null,
      length: 4,
      width: 3,
      height: null,
      walls: [true, true, true, true],
      ceiling: true,
      surfaceTypeId: null,
      paintId: "matte",
      damage: false,
      areaM2: 48.4,
      litres: 10.7,
    },
  ],
} as unknown as VisitDetail;

describe("visit report (P3.1)", () => {
  it("starts from the client's elements with their exact measures", () => {
    const draft = startVisitDraft(visit, "pt");
    expect(draft.draft.elements[0]).toMatchObject({ kind: "room", mode: "exact", length: "4,00", width: "3,00", described: true });
    expect(firstInvalid(draft.draft)).toBe(-1);
  });

  it("sends the corrected measures, free extras with the painter's pay, photos and notes", () => {
    const draft = startVisitDraft(visit, "en");
    draft.draft.elements[0].length = "5";
    draft.extras = [{ id: "x", description: " Fix crack ", pay: "40" }];
    draft.photos = [{ id: "p1", thumbnail: null }];
    draft.notes = " Damp on wall B ";

    expect(toReport(draft, info)).toEqual({
      elements: [expect.objectContaining({ kind: "room", length: 5, width: 3, clientKey: null })],
      extras: [{ catalogKey: null, description: "Fix crack", painterPayCents: 4000 }],
      fileIds: ["p1"],
      notes: "Damp on wall B",
    });
  });

  it("refuses wrong measures or an extra without pay instead of dropping them", () => {
    const wrong = startVisitDraft(visit, "pt");
    wrong.draft.elements[0].length = "abc";
    expect(firstInvalid(wrong.draft)).toBe(0);
    expect(toReport(wrong, info)).toBeNull();

    const noPay = startVisitDraft(visit, "pt");
    noPay.extras = [{ id: "x", description: "Rodapés", pay: "" }];
    expect(toReport(noPay, info)).toBeNull();
  });
});
