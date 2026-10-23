"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale } from "next-intl";
import { api } from "@/lib/api/client";
import { orThrow, type DraftQuoteRequest, type Schemas } from "./api";
import type { JobStatus } from "./status";

export type AdminJobsFilters = {
  status: JobStatus[];
  q: string;
  municipality: string;
  premium: "" | "yes" | "no";
  from: string;
  to: string;
};

export const EMPTY_FILTERS: AdminJobsFilters = { status: [], q: "", municipality: "", premium: "", from: "", to: "" };

/** Query string of A3.1 (status repeats); empty filters are left out. */
export function filtersToQuery(filters: AdminJobsFilters, page?: number, pageSize?: number) {
  return {
    ...(filters.status.length > 0 ? { status: filters.status } : {}),
    ...(filters.q.trim() ? { q: filters.q.trim() } : {}),
    ...(filters.municipality ? { municipality: filters.municipality } : {}),
    ...(filters.premium ? { premium: filters.premium === "yes" } : {}),
    ...(filters.from ? { from: filters.from } : {}),
    ...(filters.to ? { to: filters.to } : {}),
    ...(page ? { page: page } : {}),
    ...(pageSize ? { pageSize: pageSize } : {}),
  };
}

/** The CSV export link with the same filters. */
export function exportUrl(filters: AdminJobsFilters): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filtersToQuery(filters))) {
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, String(item));
  }
  const query = params.toString();
  return `/api/admin/jobs/export${query ? `?${query}` : ""}`;
}

const adminJobKey = (id: string) => ["admin", "job", id] as const;

export function useReviewQueue() {
  return useQuery({
    queryKey: ["admin", "reviews"],
    queryFn: async () => orThrow(await api.GET("/api/admin/reviews")),
    refetchInterval: 60_000,
  });
}

export function useAdminJobs(filters: AdminJobsFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ["admin", "jobs", filters, page, pageSize],
    queryFn: async () => orThrow(await api.GET("/api/admin/jobs", { params: { query: filtersToQuery(filters, page, pageSize) } })),
    placeholderData: keepPreviousData,
  });
}

export function useAdminJob(id: string | null) {
  const locale = useLocale();
  return useQuery({
    queryKey: [...adminJobKey(id ?? ""), locale],
    queryFn: async () => orThrow(await api.GET("/api/admin/jobs/{id}", { params: { path: { id: id! } } })),
    enabled: !!id,
    retry: false,
  });
}

export function usePartners() {
  return useQuery({
    queryKey: ["admin", "partners"],
    queryFn: async () => orThrow(await api.GET("/api/admin/partners")),
    staleTime: 5 * 60_000,
  });
}

export function useCatalogExtras() {
  return useQuery({
    queryKey: ["admin", "catalog", "extras"],
    queryFn: async () => orThrow(await api.GET("/api/admin/catalog/extras")),
    staleTime: 5 * 60_000,
  });
}

export function useAdminCancellationPreview(id: string, enabled: boolean, fullRefund: boolean) {
  return useQuery({
    queryKey: [...adminJobKey(id), "cancellation-preview", fullRefund],
    queryFn: async () =>
      orThrow(await api.GET("/api/admin/jobs/{id}/cancellation-preview", { params: { path: { id }, query: { fullRefund } } })),
    enabled,
  });
}

/** After any action the job, the queue and the board are stale. */
function useAdminAction<TBody, TResult = unknown>(id: string, run: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: adminJobKey(id) }),
        queryClient.invalidateQueries({ queryKey: ["admin", "reviews"] }),
        queryClient.invalidateQueries({ queryKey: ["admin", "jobs"] }),
      ]),
  });
}

/** PUT draft-quote: does not refetch the job, the response is the new draft (kept by the editor). */
export function useSaveDraft(id: string) {
  return useMutation({
    mutationFn: async (body: DraftQuoteRequest) => orThrow(await api.PUT("/api/admin/jobs/{id}/draft-quote", { params: { path: { id } }, body })),
  });
}

const path = (id: string) => ({ params: { path: { id } } });

export const useConfirmQuote = (id: string) =>
  useAdminAction(id, async (body: Schemas["ConfirmQuoteRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/confirm-quote", { ...path(id), body })));

export const useRejectJob = (id: string) =>
  useAdminAction(id, async (body: Schemas["ReasonRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/reject", { ...path(id), body })));

export const useScheduleSiteVisit = (id: string) =>
  useAdminAction(id, async (body: Schemas["SiteVisitRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/site-visit", { ...path(id), body })));

export const useCompleteSiteVisit = (id: string) =>
  useAdminAction(id, async (body: Schemas["VersionRequest"]) =>
    orThrow(await api.POST("/api/admin/jobs/{id}/site-visit/complete", { ...path(id), body })),
  );

export const useTransitionJob = (id: string) =>
  useAdminAction(id, async (body: Schemas["TransitionRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/transition", { ...path(id), body })));

export const useAdminCancel = (id: string) =>
  useAdminAction(id, async (body: Schemas["AdminCancelRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/cancel", { ...path(id), body })));
