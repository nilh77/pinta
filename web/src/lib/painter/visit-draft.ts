import type { components } from "@/lib/api/schema";
import { centsFromInput, formatDecimalInput } from "@/lib/numbers";
import { emptyDetails, newUuid, toEstimateRequest, validateElement, type CatalogInfo, type Draft, type DraftElement } from "@/lib/quote/draft";

type Schemas = components["schemas"];
type VisitElement = Schemas["PainterElement"];
export type VisitDetail = Schemas["PainterVisitDetail"];

/** A free extra item the painter found: what it is and what they would be paid (P3.1). */
export type VisitExtra = { id: string; description: string; pay: string };

/** What the painter has typed on site; kept on the device until the report is sent (screens.md, P3). */
export type VisitDraft = { draft: Draft; extras: VisitExtra[]; notes: string; photos: { id: string; thumbnail: string | null }[] };

/** The client's elements in the builder's model, all described, so the room editor and its checks apply as they are. */
export function fromVisitElements(elements: VisitElement[], locale: string): DraftElement[] {
  return elements.map((e) => {
    const exact = e.kind === "surface" || e.size == null;
    const text = (value: number | null | undefined) => (value == null ? "" : formatDecimalInput(locale, value));
    return {
      id: newUuid(),
      kind: e.kind,
      customName: e.name ?? null,
      roomTypeId: e.roomTypeId ?? "other",
      surfaceTypeId: e.surfaceTypeId ?? "interior_wall",
      mode: exact ? "exact" : "size",
      size: e.size ?? "M",
      length: exact ? text(e.length) : "",
      width: exact ? text(e.width) : "",
      height: exact && e.kind === "room" ? text(e.height) : "",
      walls: (e.walls ?? [false, false, false, false]) as DraftElement["walls"],
      ceiling: e.ceiling,
      paintId: e.paintId,
      paintChosen: true,
      damage: e.damage,
      described: true,
    };
  });
}

export function startVisitDraft(visit: VisitDetail, locale: string): VisitDraft {
  return {
    draft: {
      version: 1,
      type: "home",
      occupancy: visit.occupancy,
      elements: fromVisitElements(visit.elements, locale),
      step: "schema",
      details: emptyDetails(),
      savedAt: Date.now(),
    },
    extras: [],
    notes: "",
    photos: [],
  };
}

/** Index of the first element with wrong measures (it would be left out of the report), or -1. */
export const firstInvalid = (draft: Draft) => draft.elements.findIndex((e) => !e.described || Object.keys(validateElement(e)).length > 0);

/** POST /api/painter/visits/{id}/report body; null while an element or extra is not valid. */
export function toReport(visit: VisitDraft, info: CatalogInfo): Schemas["SiteVisitReportRequest"] | null {
  if (firstInvalid(visit.draft) >= 0) return null;
  const elements = toEstimateRequest(visit.draft, info)?.elements;
  const extras = visit.extras.filter((x) => x.description.trim() || x.pay.trim());
  if (!elements || elements.length !== visit.draft.elements.length || extras.some((x) => !x.description.trim() || (centsFromInput(x.pay) ?? -1) < 0)) {
    return null;
  }

  return {
    elements: elements.map((e) => ({ ...e, clientKey: null })),
    extras: extras.map((x) => ({ catalogKey: null, description: x.description.trim(), painterPayCents: centsFromInput(x.pay)! })),
    fileIds: visit.photos.map((p) => p.id),
    notes: visit.notes.trim() || null,
  };
}

const storageKey = (jobId: string) => `pinta.visit.${jobId}`;

/** The unsent report on this device, if any; storage may be unavailable (private mode). */
export function loadVisitDraft(jobId: string): VisitDraft | null {
  try {
    const raw = localStorage.getItem(storageKey(jobId));
    return raw ? (JSON.parse(raw) as VisitDraft) : null;
  } catch {
    return null;
  }
}

export function saveVisitDraft(jobId: string, visit: VisitDraft | null) {
  try {
    if (visit) localStorage.setItem(storageKey(jobId), JSON.stringify(visit));
    else localStorage.removeItem(storageKey(jobId));
  } catch {
    // Without storage the report still works; it just is not kept across reloads.
  }
}
