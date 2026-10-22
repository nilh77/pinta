"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { FieldNamesMarkedBoolean } from "react-hook-form";
import { api } from "@/lib/api/client";
import { ApiError, type Problem } from "@/lib/api/problem";
import {
  toExtraRequest,
  toModifierRequest,
  toPaintRequest,
  toRoomTypeRequest,
  toSettingsRequest,
  toSurfaceRequest,
  type PricingCatalog,
  type PricingFormValues,
} from "./pricing-form";
import type { QuoteBreakdown, SimulateRequest } from "./simulator";

export const pricingQueryKey = ["admin", "pricing"] as const;

function orThrow<T>(result: { data?: T; error?: unknown }): T {
  if (result.error !== undefined) throw new ApiError(result.error as Problem);
  return result.data as T;
}

/** Everything A1 edits (GET /api/admin/catalog/*, GET /api/admin/settings). Never refetched behind the admin's back. */
export function usePricingCatalog() {
  return useQuery({
    queryKey: pricingQueryKey,
    queryFn: async (): Promise<PricingCatalog> => {
      const [paints, surfaces, roomTypes, modifiers, extras, settings] = await Promise.all([
        api.GET("/api/admin/catalog/paints").then(orThrow),
        api.GET("/api/admin/catalog/surfaces").then(orThrow),
        api.GET("/api/admin/catalog/room-types").then(orThrow),
        api.GET("/api/admin/catalog/modifiers").then(orThrow),
        api.GET("/api/admin/catalog/extras").then(orThrow),
        api.GET("/api/admin/settings").then(orThrow),
      ]);
      return { paints, surfaces, roomTypes, modifiers, extras, settings };
    },
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: false,
  });
}

type Dirty = Partial<Readonly<FieldNamesMarkedBoolean<PricingFormValues>>>;

const isDirty = (flags: unknown): boolean =>
  flags === true || (typeof flags === "object" && flags !== null && Object.values(flags).some(isDirty));

/** Rows to send: new ones (no id) and edited ones. */
function changed<T extends { id?: string }>(rows: T[], dirty: unknown): T[] {
  const flags = (dirty ?? []) as unknown[];
  return rows.filter((row, i) => !row.id || isDirty(flags[i]));
}

/** Saving failed on one row; `label` names it for the error banner. */
export class SaveError extends ApiError {
  constructor(problem: Problem | undefined, public readonly label: string) {
    super(problem);
  }
}

/**
 * Saves only what changed. Order matters: new paints first (a room type may suggest them), then room types (so a paint
 * that stops being suggested can be deactivated), then the rest. Stops at the first error.
 */
export function useSavePricing() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ values, dirty, loaded }: { values: PricingFormValues; dirty: Dirty; loaded: PricingCatalog }) => {
      const run = async (label: string, call: () => Promise<{ data?: unknown; error?: unknown }>) => {
        const result = await call();
        if (result.error !== undefined) throw new SaveError(result.error as Problem, label);
      };
      const paints = changed(values.paints, dirty.paints);

      for (const p of paints.filter((p) => !p.id)) {
        await run(p.name.pt, () => api.POST("/api/admin/catalog/paints", { body: toPaintRequest(p) }));
      }
      for (const r of changed(values.roomTypes, dirty.roomTypes)) {
        await run(r.name.pt, () =>
          api.PUT("/api/admin/catalog/room-types/{id}", { params: { path: { id: r.id! } }, body: toRoomTypeRequest(r) }),
        );
      }
      for (const p of paints.filter((p) => p.id)) {
        await run(p.name.pt, () =>
          api.PUT("/api/admin/catalog/paints/{id}", { params: { path: { id: p.id! } }, body: toPaintRequest(p) }),
        );
      }
      for (const s of changed(values.surfaces, dirty.surfaces)) {
        await run(s.name.pt, () =>
          s.id
            ? api.PUT("/api/admin/catalog/surfaces/{id}", { params: { path: { id: s.id } }, body: toSurfaceRequest(s) })
            : api.POST("/api/admin/catalog/surfaces", { body: toSurfaceRequest(s) }),
        );
      }
      for (const m of changed(values.modifiers, dirty.modifiers)) {
        await run(m.name.pt, () =>
          api.PUT("/api/admin/catalog/modifiers/{id}", { params: { path: { id: m.id! } }, body: toModifierRequest(m) }),
        );
      }
      for (const x of changed(values.extras, dirty.extras)) {
        await run(x.name.pt, () =>
          x.id
            ? api.PUT("/api/admin/catalog/extras/{id}", { params: { path: { id: x.id } }, body: toExtraRequest(x) })
            : api.POST("/api/admin/catalog/extras", { body: toExtraRequest(x) }),
        );
      }
      if (isDirty(dirty.settings)) {
        await run("settings", () => api.PUT("/api/admin/settings", { body: toSettingsRequest(values.settings, loaded.settings) }));
      }
    },
    // After an error the admin's edits stay on screen; "Reload" fetches the latest data (and discards them).
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin"] }),
  });
}

export type SimulationResult = { breakdown: QuoteBreakdown } | { problem: Problem };

/** Live simulator (POST /api/admin/quotes/simulate). `catalogVersion` re-runs it after a save. */
export function useSimulation(request: SimulateRequest, catalogVersion: number) {
  return useQuery({
    queryKey: ["admin", "simulate", request, catalogVersion],
    queryFn: async (): Promise<SimulationResult> => {
      const result = await api.POST("/api/admin/quotes/simulate", { body: request });
      if (result.response.status === 422) return { problem: result.error as Problem };
      return { breakdown: orThrow(result) };
    },
    placeholderData: keepPreviousData,
    retry: false,
  });
}
