import { z } from "zod";
import { emptyDetails, type Draft } from "./draft";

export const DRAFT_KEY = "pinta.quote.draft.v1";
/** A draft older than this starts from scratch (C2.7). */
export const DRAFT_MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000;

const elementSchema = z.object({
  id: z.string(),
  kind: z.enum(["room", "surface"]),
  customName: z.string().nullable(),
  roomTypeId: z.string(),
  surfaceTypeId: z.string(),
  mode: z.enum(["size", "exact"]),
  size: z.enum(["S", "M", "L"]),
  length: z.string(),
  width: z.string(),
  height: z.string(),
  walls: z.tuple([z.boolean(), z.boolean(), z.boolean(), z.boolean()]),
  ceiling: z.boolean(),
  paintId: z.string(),
  paintChosen: z.boolean(),
  damage: z.boolean(),
  described: z.boolean(),
});

const fileSchema = z.object({
  id: z.string(),
  kind: z.enum(["image", "video"]),
  name: z.string(),
  thumbnail: z.string().nullable(),
  status: z.enum(["uploading", "done", "error"]),
  progress: z.number(),
});

// Drafts saved before H3 have no details: they get fresh ones.
const detailsSchema = z
  .object({
    draftId: z.string(),
    idempotencyKey: z.string(),
    description: z.string(),
    files: z.record(z.string(), z.array(fileSchema)),
    address: z.object({
      line1: z.string(),
      floor: z.string(),
      postalCode: z.string(),
      locality: z.string(),
      accessNotes: z.string(),
      municipality: z.string().nullable(),
    }),
    dates: z.object({ asap: z.boolean().nullable(), from: z.string(), to: z.string() }),
    billing: z.object({ wanted: z.boolean(), taxId: z.string(), companyName: z.string() }),
  })
  .optional()
  .transform((details) => details ?? emptyDetails());

const draftSchema = z.object({
  version: z.literal(1),
  type: z.enum(["surface", "room", "home"]),
  occupancy: z.enum(["empty", "furnished", "inhabited"]).nullable(),
  elements: z.array(elementSchema).max(30),
  step: z.enum(["type", "schema", "estimate", "photos", "address", "account"]),
  details: detailsSchema,
  savedAt: z.number(),
});

type Store = Pick<Storage, "getItem" | "setItem" | "removeItem">;

/** The saved draft, or null if there is none, it is unreadable or older than 30 days. Storage may be blocked. */
export function loadDraft(storage: Store | undefined, now: number): Draft | null {
  try {
    const raw = storage?.getItem(DRAFT_KEY);
    if (!raw) return null;
    const parsed = draftSchema.safeParse(JSON.parse(raw));
    if (!parsed.success || now - parsed.data.savedAt > DRAFT_MAX_AGE_MS) {
      storage?.removeItem(DRAFT_KEY);
      return null;
    }
    // An upload interrupted by the reload cannot resume: the file itself is gone.
    const files = Object.fromEntries(
      Object.entries(parsed.data.details.files).map(([key, list]) => [key, list.filter((f) => f.status === "done")]),
    );
    return { ...parsed.data, details: { ...parsed.data.details, files } };
  } catch {
    return null;
  }
}

export function saveDraft(storage: Store | undefined, draft: Draft | null, now: number): void {
  try {
    if (draft) storage?.setItem(DRAFT_KEY, JSON.stringify({ ...draft, savedAt: now }));
    else storage?.removeItem(DRAFT_KEY);
  } catch {
    // Private mode or full storage: the builder keeps working, it just won't survive a reload.
  }
}
