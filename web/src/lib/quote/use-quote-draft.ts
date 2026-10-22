"use client";

import { useCallback, useSyncExternalStore } from "react";
import type { Draft } from "./draft";
import { loadDraft, saveDraft } from "./storage";

/**
 * The builder's draft, shared by every step and kept in localStorage (docs/screens.md). `hydrated` is false on the
 * server and on the first client render, so pages can show a skeleton instead of an empty schema.
 */
let current: Draft | null = null;
let hydrated = false;
const listeners = new Set<() => void>();

const storage = () => (typeof window === "undefined" ? undefined : window.localStorage);

function hydrate() {
  if (hydrated || typeof window === "undefined") return;
  current = loadDraft(storage(), Date.now());
  hydrated = true;
}

function subscribe(listener: () => void) {
  hydrate();
  listeners.add(listener);
  return () => listeners.delete(listener);
}

type Snapshot = { draft: Draft | null; hydrated: boolean };
let snapshot: Snapshot = { draft: null, hydrated: false };
const serverSnapshot: Snapshot = { draft: null, hydrated: false };

function getSnapshot(): Snapshot {
  hydrate();
  if (snapshot.draft !== current || snapshot.hydrated !== hydrated) snapshot = { draft: current, hydrated };
  return snapshot;
}

export function setDraft(update: Draft | null | ((draft: Draft | null) => Draft | null)) {
  hydrate();
  current = typeof update === "function" ? update(current) : update;
  saveDraft(storage(), current, Date.now());
  listeners.forEach((listener) => listener());
}

export function useQuoteDraft() {
  const state = useSyncExternalStore(subscribe, getSnapshot, () => serverSnapshot);
  const update = useCallback((next: Draft | ((draft: Draft) => Draft)) => {
    setDraft((draft) => (typeof next === "function" ? (draft ? next(draft) : draft) : next));
  }, []);
  return { ...state, update, reset: useCallback(() => setDraft(null), []) };
}
