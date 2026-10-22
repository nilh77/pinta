import { describe, expect, it } from "vitest";
import { saveElement, startDraft, type CatalogInfo } from "./draft";
import { DRAFT_KEY, loadDraft } from "./storage";
import {
  addBusinessDays,
  isValidTaxId,
  lisbonToday,
  photoCoverage,
  toSubmitRequest,
  validateAddress,
  validateBilling,
  validateDates,
} from "./request";

const catalog: CatalogInfo = {
  defaultHeight: 2.6,
  defaultPaintId: "matte",
  defaultSurfaceTypeId: "interior_wall",
  roomTypes: { bedroom: { suggestedPaintId: "matte", sizes: { S: [3, 2.5], M: [3.5, 3], L: [4.5, 4] } } },
};

describe("dates (C4.2)", () => {
  it("uses the date in Lisbon", () => {
    expect(lisbonToday(new Date("2026-05-31T23:30:00Z"))).toBe("2026-06-01");
  });

  it("adds working days, skipping weekends", () => {
    expect(addBusinessDays("2026-10-22", 3)).toBe("2026-10-27");
    expect(addBusinessDays("2026-10-24", 1)).toBe("2026-10-26");
  });

  it("needs a choice, a start after the notice and at most 60 days", () => {
    const earliest = "2026-10-27";
    expect(validateDates({ asap: null, from: "", to: "" }, earliest)).toEqual({ dates: "quote.errors.datesChoose" });
    expect(validateDates({ asap: true, from: "", to: "" }, earliest)).toEqual({});
    expect(validateDates({ asap: false, from: "2026-10-26", to: "2026-10-30" }, earliest)).toEqual({ from: "quote.errors.datesTooSoon" });
    expect(validateDates({ asap: false, from: "2026-11-09", to: "2026-11-02" }, earliest)).toEqual({ to: "quote.errors.datesOrder" });
    expect(validateDates({ asap: false, from: "2026-11-01", to: "2027-01-01" }, earliest)).toEqual({ to: "quote.errors.datesTooLong" });
    expect(validateDates({ asap: false, from: "2026-11-01", to: "2026-12-31" }, earliest)).toEqual({});
  });
});

describe("address and invoice (C4.1, C4.3)", () => {
  it("requires address, postal code NNNN-NNN and locality", () => {
    expect(validateAddress({ line1: " ", floor: "", postalCode: "2710123", locality: "", accessNotes: "", municipality: null })).toEqual({
      line1: "quote.errors.required",
      postalCode: "quote.errors.postalCode",
      locality: "quote.errors.required",
    });
  });

  it("checks the NIF check digit like the API", () => {
    expect(["123456789", "999999990", "501442600", "451234561"].every(isValidTaxId)).toBe(true);
    expect(["123456780", "012345678", "412345678", "12345678"].some(isValidTaxId)).toBe(false);
    expect(validateBilling({ wanted: true, taxId: "123", companyName: "" })).toEqual({
      taxId: "quote.errors.taxId",
      companyName: "quote.errors.required",
    });
    expect(validateBilling({ wanted: false, taxId: "", companyName: "" })).toEqual({});
  });
});

describe("submission", () => {
  it("maps the draft to POST /api/jobs with the element keys of the photos", () => {
    let draft = startDraft("room", catalog, Date.now());
    draft = saveElement(draft, draft.elements[0]);
    draft.details.address = { line1: " Rua das Flores, 24 ", floor: "", postalCode: "2710-123", locality: "Sintra", accessNotes: "", municipality: "Sintra" };
    draft.details.dates = { asap: true, from: "", to: "" };
    draft.details.files[draft.elements[0].id] = [{ id: "f1", kind: "image", name: "a.jpg", thumbnail: null, status: "done", progress: 100 }];

    const request = toSubmitRequest(draft, catalog)!;

    expect(request.draftId).toBe(draft.details.draftId);
    expect(request.elements[0].clientKey).toBe(draft.elements[0].id);
    expect(request.address).toEqual({ line1: "Rua das Flores, 24", floor: null, postalCode: "2710-123", locality: "Sintra", accessNotes: null });
    expect(request.preferredDates).toEqual({ asap: true, from: null, to: null });
    expect(request.billing).toBeNull();
    expect(photoCoverage(draft, catalog)).toEqual({ withPhotos: 1, total: 1 });
  });

  it("keeps drafts saved before H3 and drops uploads cut by a reload", () => {
    const now = Date.now();
    const old = startDraft("room", catalog, now) as unknown as Record<string, unknown>;
    delete old.details;
    const storage = new Map([[DRAFT_KEY, JSON.stringify(old)]]);
    const store = { getItem: (k: string) => storage.get(k) ?? null, setItem: () => {}, removeItem: () => {} };
    const loaded = loadDraft(store, now)!;
    expect(loaded.details.draftId).toMatch(/^[0-9a-f-]{36}$/);

    loaded.details.files.x = [
      { id: "a", kind: "image", name: "a", thumbnail: null, status: "done", progress: 100 },
      { id: "b", kind: "video", name: "b", thumbnail: null, status: "uploading", progress: 40 },
    ];
    storage.set(DRAFT_KEY, JSON.stringify(loaded));
    expect(loadDraft(store, now)!.details.files.x.map((f) => f.id)).toEqual(["a"]);
  });
});
