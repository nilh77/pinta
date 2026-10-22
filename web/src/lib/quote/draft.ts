import type { components } from "@/lib/api/schema";
import { decimalPlaces, formatDecimalInput, parseDecimal } from "@/lib/numbers";
import { elementArea, type ElementArea, type Walls } from "./area";

type Schemas = components["schemas"];
export type Occupancy = Schemas["Occupancy"];
export type RoomSize = Schemas["RoomSize"];
export type EstimateRequest = Schemas["EstimateRequest"];

/** What the client chose in step 1 (board Tipo). */
export type JobType = "surface" | "room" | "home";
export type MeasureMode = "size" | "exact";
export type QuoteStep = "type" | "schema" | "estimate";

/** One room or loose surface as the client describes it. Measures stay as typed ("3,5") until sent. */
export type DraftElement = {
  id: string;
  kind: "room" | "surface";
  /** Null: the default name ("Dormitorio 2") is computed in the UI language. */
  customName: string | null;
  roomTypeId: string;
  surfaceTypeId: string;
  mode: MeasureMode;
  size: RoomSize;
  length: string;
  width: string;
  height: string;
  walls: [boolean, boolean, boolean, boolean];
  ceiling: boolean;
  paintId: string;
  /** The client picked the paint; otherwise it follows the room type's suggestion. */
  paintChosen: boolean;
  damage: boolean;
  /** Opened and saved by the client: only described elements are priced (C2.4). */
  described: boolean;
};

export type Draft = {
  version: 1;
  type: JobType;
  occupancy: Occupancy | null;
  elements: DraftElement[];
  step: QuoteStep;
  savedAt: number;
};

/** What the builder needs from GET /api/catalog: sizes, suggestions and defaults. */
export type CatalogInfo = {
  defaultHeight: number;
  defaultPaintId: string;
  defaultSurfaceTypeId: string;
  roomTypes: Record<string, { suggestedPaintId: string; sizes: Record<RoomSize, [number, number]> }>;
};

// Limits of docs/pricing-engine.md ("Validaciones"); the API checks them again.
export const MAX_ELEMENTS = 30;
const MIN_SIDE = 0.5;
const MAX_ROOM_SIDE = 30;
const MIN_HEIGHT = 2;
const MAX_HEIGHT = 6;
const MAX_SURFACE_LENGTH = 40;
const MAX_SURFACE_WIDTH = 20;

/** Rooms preloaded for a full home (docs/screens.md, screen 1). */
const HOME_PRESET = ["living", "kitchen", "bathroom", "bedroom"];

let sequence = 0;
/** Ids only need to be unique in this draft; crypto.randomUUID is missing on plain-http LAN addresses. */
const newId = () => `${Date.now().toString(36)}-${(sequence++).toString(36)}-${Math.random().toString(36).slice(2, 8)}`;

function newRoom(roomTypeId: string, catalog: CatalogInfo): DraftElement {
  return {
    id: newId(),
    kind: "room",
    customName: null,
    roomTypeId,
    surfaceTypeId: catalog.defaultSurfaceTypeId,
    mode: "size",
    size: "M",
    length: "",
    width: "",
    height: "",
    walls: [true, true, true, true],
    ceiling: true,
    paintId: catalog.roomTypes[roomTypeId]?.suggestedPaintId ?? catalog.defaultPaintId,
    paintChosen: false,
    damage: false,
    described: false,
  };
}

function newSurface(catalog: CatalogInfo): DraftElement {
  return {
    ...newRoom(Object.keys(catalog.roomTypes)[0] ?? "other", catalog),
    kind: "surface",
    mode: "exact",
    walls: [false, false, false, false],
    ceiling: false,
    paintId: catalog.defaultPaintId,
  };
}

export function startDraft(type: JobType, catalog: CatalogInfo, now: number): Draft {
  const elements =
    type === "home"
      ? HOME_PRESET.filter((key) => key in catalog.roomTypes).map((key) => newRoom(key, catalog))
      : type === "room"
        ? [newRoom("bedroom" in catalog.roomTypes ? "bedroom" : Object.keys(catalog.roomTypes)[0], catalog)]
        : [newSurface(catalog)];
  // Loose surfaces are priced as an empty home by default (docs/pricing-engine.md).
  return { version: 1, type, occupancy: type === "surface" ? "empty" : null, elements, step: "schema", savedAt: now };
}

const withElements = (draft: Draft, elements: DraftElement[]): Draft => ({ ...draft, elements });

export const addRoom = (draft: Draft, roomTypeId: string, catalog: CatalogInfo): Draft =>
  draft.elements.length >= MAX_ELEMENTS ? draft : withElements(draft, [...draft.elements, newRoom(roomTypeId, catalog)]);

export const addSurface = (draft: Draft, catalog: CatalogInfo): Draft =>
  draft.elements.length >= MAX_ELEMENTS ? draft : withElements(draft, [...draft.elements, newSurface(catalog)]);

export function duplicateElement(draft: Draft, id: string): Draft {
  const index = draft.elements.findIndex((e) => e.id === id);
  if (index < 0 || draft.elements.length >= MAX_ELEMENTS) return draft;
  const copy = { ...draft.elements[index], id: newId(), customName: null };
  return withElements(draft, [...draft.elements.slice(0, index + 1), copy, ...draft.elements.slice(index + 1)]);
}

export const renameElement = (draft: Draft, id: string, name: string): Draft =>
  withElements(draft, draft.elements.map((e) => (e.id === id ? { ...e, customName: name.trim() || null } : e)));

export type RemovedElement = { element: DraftElement; index: number };

export function removeElement(draft: Draft, id: string): { draft: Draft; removed: RemovedElement | null } {
  const index = draft.elements.findIndex((e) => e.id === id);
  if (index < 0) return { draft, removed: null };
  return { draft: withElements(draft, draft.elements.filter((e) => e.id !== id)), removed: { element: draft.elements[index], index } };
}

/** Undo of a removal: back at its position (5 s toast, docs/screens.md). */
export function restoreElement(draft: Draft, { element, index }: RemovedElement): Draft {
  if (draft.elements.some((e) => e.id === element.id) || draft.elements.length >= MAX_ELEMENTS) return draft;
  const elements = [...draft.elements];
  elements.splice(Math.min(index, elements.length), 0, element);
  return withElements(draft, elements);
}

/**
 * The editor works on a copy; saving it back marks the element as described. The name is kept from the draft: it is
 * renamed from the schema, never in the editor, so a stale copy cannot undo a rename.
 */
export const saveElement = (draft: Draft, element: DraftElement): Draft =>
  withElements(draft, draft.elements.map((e) => (e.id === element.id ? { ...element, customName: e.customName, described: true } : e)));

/** Exact measures start from the size's metres (C2.3). */
export function setMeasureMode(element: DraftElement, mode: MeasureMode, catalog: CatalogInfo, locale: string): DraftElement {
  if (mode === element.mode) return element;
  if (mode === "size") return { ...element, mode };
  const [length, width] = catalog.roomTypes[element.roomTypeId]?.sizes[element.size] ?? [0, 0];
  const keep = (text: string, value: number) => (text.trim() ? text : formatDecimalInput(locale, value));
  return {
    ...element,
    mode,
    length: keep(element.length, length),
    width: keep(element.width, width),
    height: keep(element.height, catalog.defaultHeight),
  };
}

export function setRoomType(element: DraftElement, roomTypeId: string, catalog: CatalogInfo): DraftElement {
  const suggested = catalog.roomTypes[roomTypeId]?.suggestedPaintId;
  return { ...element, roomTypeId, paintId: element.paintChosen || !suggested ? element.paintId : suggested };
}

/**
 * Default names in the UI language: "Dormitorio" when there is one, "Dormitorio 1" and "Dormitorio 2" when there are
 * several; custom names win (C2.4).
 */
export function displayNames(elements: DraftElement[], roomTypeNames: Record<string, string>, surfaceName: string): string[] {
  const group = (e: DraftElement) => (e.kind === "surface" ? "#surface" : e.roomTypeId);
  const totals = new Map<string, number>();
  for (const e of elements) totals.set(group(e), (totals.get(group(e)) ?? 0) + 1);
  const seen = new Map<string, number>();
  return elements.map((e) => {
    const key = group(e);
    const n = (seen.get(key) ?? 0) + 1;
    seen.set(key, n);
    if (e.customName) return e.customName;
    const base = e.kind === "surface" ? surfaceName : (roomTypeNames[e.roomTypeId] ?? e.roomTypeId);
    return (totals.get(key) ?? 0) > 1 ? `${base} ${n}` : base;
  });
}

/** Field → translation key of the error (quote.errors.*). Empty when valid. */
export type ElementErrors = Partial<Record<"walls" | "length" | "width" | "height", string>>;

function checkMeasure(text: string, min: number, max: number, rangeKey: string): string | undefined {
  if (!text.trim()) return "quote.errors.required";
  const value = parseDecimal(text);
  if (value === null) return "quote.errors.number";
  if (decimalPlaces(text) > 2) return "quote.errors.decimals";
  return value < min || value > max ? rangeKey : undefined;
}

export function validateElement(element: DraftElement): ElementErrors {
  const errors: ElementErrors = {};
  const set = (field: keyof ElementErrors, error: string | undefined) => {
    if (error) errors[field] = error;
  };
  if (element.kind === "surface") {
    set("length", checkMeasure(element.length, MIN_SIDE, MAX_SURFACE_LENGTH, "quote.errors.surfaceLength"));
    set("width", checkMeasure(element.width, MIN_SIDE, MAX_SURFACE_WIDTH, "quote.errors.surfaceHeight"));
    return errors;
  }

  if (!element.walls.some(Boolean) && !element.ceiling) errors.walls = "quote.errors.wallsOrCeiling";
  if (element.mode === "exact") {
    set("length", checkMeasure(element.length, MIN_SIDE, MAX_ROOM_SIDE, "quote.errors.side"));
    set("width", checkMeasure(element.width, MIN_SIDE, MAX_ROOM_SIDE, "quote.errors.side"));
    if (element.height.trim()) set("height", checkMeasure(element.height, MIN_HEIGHT, MAX_HEIGHT, "quote.errors.height"));
  }
  return errors;
}

/** Length × width × height in metres, from the size table or the typed measures; null while they are invalid. */
function measures(element: DraftElement, catalog: CatalogInfo): { length: number; width: number; height: number } | null {
  if (element.kind === "room" && element.mode === "size") {
    const size = catalog.roomTypes[element.roomTypeId]?.sizes[element.size];
    return size ? { length: size[0], width: size[1], height: catalog.defaultHeight } : null;
  }
  const errors = validateElement(element);
  if (errors.length || errors.width || errors.height) return null;
  const height = element.height.trim() ? parseDecimal(element.height)! : catalog.defaultHeight;
  return { length: parseDecimal(element.length)!, width: parseDecimal(element.width)!, height };
}

/** Area shown instantly on the cards and in the editor (never a price). */
export function elementAreaOf(element: DraftElement, catalog: CatalogInfo): ElementArea | null {
  const m = measures(element, catalog);
  if (!m) return null;
  return element.kind === "surface"
    ? elementArea({ kind: "surface", length: m.length, width: m.width })
    : elementArea({ kind: "room", ...m, walls: element.walls as Walls, ceiling: element.ceiling });
}

/** POST /api/quotes/estimate body with the described, valid elements; null when there is nothing to price yet. */
export function toEstimateRequest(draft: Draft, catalog: CatalogInfo): EstimateRequest | null {
  const priced = draft.elements.filter(
    (e) => e.described && Object.keys(validateElement(e)).length === 0 && (elementAreaOf(e, catalog)?.total ?? 0) > 0,
  );
  if (priced.length === 0) return null;
  return {
    // The live estimate uses "empty" until the client chooses (docs/screens.md, screen 2).
    occupancy: draft.occupancy ?? "empty",
    elements: priced.map((e) => {
      const exact = e.kind === "surface" || e.mode === "exact";
      const m = measures(e, catalog)!;
      return {
        kind: e.kind,
        name: e.customName,
        roomTypeId: e.kind === "room" ? e.roomTypeId : null,
        size: exact ? null : e.size,
        length: exact ? m.length : null,
        width: exact ? m.width : null,
        height: e.kind === "room" && exact ? m.height : null,
        walls: e.kind === "room" ? e.walls : null,
        ceiling: e.kind === "room" && e.ceiling,
        surfaceTypeId: e.kind === "surface" ? e.surfaceTypeId : null,
        paintId: e.paintId,
        damage: e.damage,
      };
    }),
  };
}
