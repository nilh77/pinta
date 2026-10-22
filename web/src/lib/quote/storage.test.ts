import { describe, expect, it } from "vitest";
import { startDraft, type CatalogInfo } from "./draft";
import { DRAFT_KEY, DRAFT_MAX_AGE_MS, loadDraft, saveDraft } from "./storage";

const catalog: CatalogInfo = {
  defaultHeight: 2.6,
  defaultPaintId: "matte",
  defaultSurfaceTypeId: "interior_wall",
  roomTypes: { bedroom: { suggestedPaintId: "matte", sizes: { S: [3, 2.5], M: [3.5, 3], L: [4.5, 4] } } },
};

function memoryStorage() {
  const items = new Map<string, string>();
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    removeItem: (key: string) => void items.delete(key),
    items,
  };
}

describe("draft storage (C2.7)", () => {
  const now = Date.UTC(2026, 9, 22);

  it("round-trips a draft", () => {
    const storage = memoryStorage();
    const draft = startDraft("room", catalog, now);
    saveDraft(storage, draft, now);
    expect(loadDraft(storage, now + 1000)).toEqual(draft);
  });

  it("starts from scratch after 30 days", () => {
    const storage = memoryStorage();
    saveDraft(storage, startDraft("room", catalog, now), now);
    expect(loadDraft(storage, now + DRAFT_MAX_AGE_MS + 1)).toBeNull();
    expect(storage.items.has(DRAFT_KEY)).toBe(false);
  });

  it("ignores unreadable data and blocked storage", () => {
    const storage = memoryStorage();
    storage.setItem(DRAFT_KEY, "{not json");
    expect(loadDraft(storage, now)).toBeNull();
    storage.setItem(DRAFT_KEY, JSON.stringify({ version: 2 }));
    expect(loadDraft(storage, now)).toBeNull();

    const blocked = {
      getItem: () => {
        throw new Error("SecurityError");
      },
      setItem: () => {
        throw new Error("QuotaExceeded");
      },
      removeItem: () => {},
    };
    expect(loadDraft(blocked, now)).toBeNull();
    expect(() => saveDraft(blocked, startDraft("room", catalog, now), now)).not.toThrow();
  });
});
