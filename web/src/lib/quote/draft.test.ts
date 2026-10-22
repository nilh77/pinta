import { describe, expect, it } from "vitest";
import {
  addRoom,
  addSurface,
  displayNames,
  duplicateElement,
  elementAreaOf,
  MAX_ELEMENTS,
  removeElement,
  renameElement,
  restoreElement,
  saveElement,
  setMeasureMode,
  setRoomType,
  startDraft,
  toEstimateRequest,
  validateElement,
  type CatalogInfo,
  type Draft,
} from "./draft";
import { toM2 } from "./area";

const catalog: CatalogInfo = {
  defaultHeight: 2.6,
  defaultPaintId: "matte",
  defaultSurfaceTypeId: "interior_wall",
  roomTypes: {
    living: { suggestedPaintId: "matte", sizes: { S: [4, 3], M: [5, 4], L: [6, 5] } },
    bedroom: { suggestedPaintId: "matte", sizes: { S: [3, 2.5], M: [3.5, 3], L: [4.5, 4] } },
    kitchen: { suggestedPaintId: "satin", sizes: { S: [3, 2], M: [3.5, 3], L: [4.5, 3.5] } },
    bathroom: { suggestedPaintId: "anti_damp", sizes: { S: [2, 1.5], M: [2.5, 2], L: [3, 2.5] } },
    hallway: { suggestedPaintId: "matte", sizes: { S: [3, 1], M: [5, 1.2], L: [7, 1.2] } },
  },
};
const NOW = Date.UTC(2026, 9, 22);

describe("startDraft", () => {
  it("full home starts with living room, kitchen, bathroom and bedroom in size M, not described (C2.4)", () => {
    const draft = startDraft("home", catalog, NOW);
    expect(draft.elements.map((e) => [e.roomTypeId, e.size, e.mode, e.paintId, e.described])).toEqual([
      ["living", "M", "size", "matte", false],
      ["kitchen", "M", "size", "satin", false],
      ["bathroom", "M", "size", "anti_damp", false],
      ["bedroom", "M", "size", "matte", false],
    ]);
    expect(draft.occupancy).toBeNull();
  });

  it("a room starts with 4 walls, ceiling, size M and the suggested paint (C2.3)", () => {
    const [room] = startDraft("room", catalog, NOW).elements;
    expect(room).toMatchObject({ kind: "room", walls: [true, true, true, true], ceiling: true, size: "M", mode: "size" });
  });

  it("a loose surface defaults to an empty home (6.3)", () => {
    expect(startDraft("surface", catalog, NOW).occupancy).toBe("empty");
  });
});

describe("editing", () => {
  it("names repeated room types with numbers (Dormitorio 1, Dormitorio 2)", () => {
    let draft = startDraft("home", catalog, NOW);
    draft = addRoom(draft, "bedroom", catalog);
    const names = displayNames(draft.elements, { living: "Sala", kitchen: "Cocina", bathroom: "Baño", bedroom: "Dormitorio" }, "Superficie");
    expect(names).toEqual(["Sala", "Cocina", "Baño", "Dormitorio 1", "Dormitorio 2"]);
  });

  it("custom names win, and surfaces are numbered too", () => {
    let draft = addSurface(addSurface(startDraft("room", catalog, NOW), catalog), catalog);
    draft = { ...draft, elements: draft.elements.map((e, i) => (i === 0 ? { ...e, customName: "Quarto da Ana" } : e)) };
    expect(displayNames(draft.elements, { bedroom: "Dormitorio" }, "Superficie")).toEqual(["Quarto da Ana", "Superficie 1", "Superficie 2"]);
  });

  it("stops at 30 elements", () => {
    let draft = startDraft("room", catalog, NOW);
    for (let i = 0; i < 40; i++) draft = addRoom(draft, "bedroom", catalog);
    expect(draft.elements).toHaveLength(MAX_ELEMENTS);
  });

  it("duplicates after the original with a new id", () => {
    const draft = startDraft("home", catalog, NOW);
    const copy = duplicateElement(draft, draft.elements[1].id);
    expect(copy.elements).toHaveLength(5);
    expect(copy.elements[2]).toMatchObject({ roomTypeId: "kitchen" });
    expect(copy.elements[2].id).not.toBe(draft.elements[1].id);
  });

  it("removes and restores at the same position (undo)", () => {
    const draft = startDraft("home", catalog, NOW);
    const { draft: without, removed } = removeElement(draft, draft.elements[1].id);
    expect(without.elements).toHaveLength(3);
    expect(restoreElement(without, removed!).elements).toEqual(draft.elements);
  });

  it("exact measures start from the chosen size (C2.3)", () => {
    const [room] = startDraft("room", catalog, NOW).elements;
    const exact = setMeasureMode(room, "exact", catalog, "pt");
    expect([exact.length, exact.width, exact.height]).toEqual(["3,50", "3,00", "2,60"]);
  });

  it("changing the room type follows the suggested paint unless the client chose one (C2.5)", () => {
    const [room] = startDraft("room", catalog, NOW).elements;
    expect(setRoomType(room, "bathroom", catalog).paintId).toBe("anti_damp");
    expect(setRoomType({ ...room, paintId: "satin", paintChosen: true }, "bathroom", catalog).paintId).toBe("satin");
  });

  it("saving marks the element as described", () => {
    const draft = startDraft("home", catalog, NOW);
    const saved = saveElement(draft, { ...draft.elements[0], damage: true });
    expect(saved.elements[0]).toMatchObject({ described: true, damage: true });
  });

  it("saving an editor copy keeps a rename made meanwhile", () => {
    const draft = startDraft("home", catalog, NOW);
    const copy = draft.elements[0];
    const renamed = renameElement(draft, copy.id, "Sala grande");
    expect(saveElement(renamed, { ...copy, size: "L" }).elements[0]).toMatchObject({ customName: "Sala grande", size: "L" });
  });
});

describe("validation (6.3 limits)", () => {
  const [room] = startDraft("room", catalog, NOW).elements;
  const exact = { ...room, mode: "exact" as const, length: "4", width: "3", height: "2,6" };

  it("accepts a valid room and needs a wall or the ceiling", () => {
    expect(validateElement(exact)).toEqual({});
    expect(validateElement({ ...exact, walls: [false, false, false, false], ceiling: false })).toEqual({ walls: "quote.errors.wallsOrCeiling" });
  });

  it("checks room sides, height and decimals", () => {
    expect(validateElement({ ...exact, length: "0,4", width: "31", height: "6,5" })).toEqual({
      length: "quote.errors.side",
      width: "quote.errors.side",
      height: "quote.errors.height",
    });
    expect(validateElement({ ...exact, length: "4,005" })).toEqual({ length: "quote.errors.decimals" });
    expect(validateElement({ ...exact, length: "" })).toEqual({ length: "quote.errors.required" });
  });

  it("lets loose surfaces go up to 40 × 20", () => {
    const [surface] = startDraft("surface", catalog, NOW).elements;
    expect(validateElement({ ...surface, length: "40", width: "20" })).toEqual({});
    expect(validateElement({ ...surface, length: "40,5", width: "20,5" })).toEqual({
      length: "quote.errors.surfaceLength",
      width: "quote.errors.surfaceHeight",
    });
  });
});

describe("area and estimate request", () => {
  function tc09(): Draft {
    let draft = startDraft("home", catalog, NOW);
    const exact = (i: number, length: string, width: string, patch = {}) =>
      (draft = saveElement(draft, { ...draft.elements[i], mode: "exact", length, width, height: "2,6", ...patch }));
    exact(0, "5", "4");
    exact(1, "3,5", "3");
    exact(2, "2,5", "2", { damage: true });
    exact(3, "4", "3,5");
    draft = addRoom(draft, "bedroom", catalog);
    draft = saveElement(draft, { ...draft.elements[4], walls: [true, true, true, false] });
    draft = addRoom(draft, "hallway", catalog);
    exact(5, "6", "1,2", { ceiling: false });
    return { ...draft, occupancy: "furnished" };
  }

  it("computes TC09's 266,44 m² from the draft", () => {
    const draft = tc09();
    const total = draft.elements.reduce((sum, e) => sum + (elementAreaOf(e, catalog)?.total ?? 0), 0);
    expect(toM2(total)).toBe(266.44);
  });

  it("maps TC09 to the API request: sizes without measures, decimals as numbers", () => {
    const request = toEstimateRequest(tc09(), catalog)!;
    expect(request.occupancy).toBe("furnished");
    expect(request.elements).toHaveLength(6);
    expect(request.elements[1]).toMatchObject({ kind: "room", roomTypeId: "kitchen", size: null, length: 3.5, width: 3, height: 2.6, paintId: "satin" });
    expect(request.elements[2]).toMatchObject({ roomTypeId: "bathroom", damage: true, paintId: "anti_damp" });
    expect(request.elements[4]).toMatchObject({ size: "M", length: null, width: null, height: null, walls: [true, true, true, false] });
  });

  it("prices only described, valid elements and uses empty until occupancy is chosen", () => {
    const draft = startDraft("home", catalog, NOW);
    expect(toEstimateRequest(draft, catalog)).toBeNull();
    const one = saveElement(draft, draft.elements[0]);
    expect(toEstimateRequest(one, catalog)).toMatchObject({ occupancy: "empty", elements: [{ roomTypeId: "living", size: "M" }] });
  });
});
