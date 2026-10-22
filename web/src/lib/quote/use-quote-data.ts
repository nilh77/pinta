"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useLocale } from "next-intl";
import { useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api/client";
import { ApiError, type Problem } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";
import type { CatalogInfo, EstimateRequest } from "./draft";

export type PublicCatalog = components["schemas"]["CatalogResponse"];
export type Estimate = components["schemas"]["EstimateResponse"];

/** GET /api/catalog in the UI language, plus the lookups the draft reducers need. */
export function useCatalog() {
  const locale = useLocale();
  const query = useQuery({
    queryKey: ["catalog", locale],
    queryFn: async () => {
      const result = await api.GET("/api/catalog");
      if (!result.response.ok || !result.data) throw new ApiError(undefined);
      return result.data;
    },
    staleTime: 5 * 60_000,
  });
  const info = useMemo(() => (query.data ? toCatalogInfo(query.data) : undefined), [query.data]);
  return { ...query, info };
}

export function toCatalogInfo(catalog: PublicCatalog): CatalogInfo {
  return {
    defaultHeight: catalog.defaultHeight,
    defaultPaintId: catalog.paints[0]?.id ?? "matte",
    defaultSurfaceTypeId: catalog.surfaceTypes[0]?.id ?? "interior_wall",
    roomTypes: Object.fromEntries(
      catalog.roomTypes.map((room) => [
        room.id,
        {
          suggestedPaintId: room.suggestedPaintId,
          sizes: Object.fromEntries(room.sizes.map((s) => [s.size, [s.length, s.width]])) as CatalogInfo["roomTypes"][string]["sizes"],
        },
      ]),
    ),
  };
}

export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

/**
 * Live estimate (POST /api/quotes/estimate) 400 ms after the last change (docs/screens.md, screen 2). While a new
 * request runs the previous range stays visible but `isStale` lets the UI dim it: it is never shown as current.
 */
export function useEstimate(request: EstimateRequest | null, { debounceMs = 400 } = {}) {
  const body = useMemo(() => (request ? JSON.stringify(request) : null), [request]);
  const debounced = useDebouncedValue(body, debounceMs);
  const query = useQuery({
    queryKey: ["estimate", debounced],
    queryFn: async (): Promise<Estimate> => {
      const result = await api.POST("/api/quotes/estimate", { body: JSON.parse(debounced!) as EstimateRequest });
      if (result.error !== undefined) throw new ApiError(result.error as Problem);
      return result.data!;
    },
    enabled: debounced !== null,
    placeholderData: keepPreviousData,
    retry: 1,
    staleTime: 60_000,
  });
  return {
    estimate: body === null ? undefined : query.data,
    isStale: body !== debounced || query.isPlaceholderData || query.isFetching,
    isError: query.isError && !query.isFetching,
    isPaused: query.fetchStatus === "paused",
    retry: () => query.refetch(),
  };
}

/** True from 1024 px (desktop builder D2). False on the server and before the first effect. */
export function useIsDesktop() {
  const [desktop, setDesktop] = useState(false);
  useEffect(() => {
    const media = window.matchMedia("(min-width: 1024px)");
    const update = () => setDesktop(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);
  return desktop;
}
