"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale } from "next-intl";
import { api } from "@/lib/api/client";
import { orThrow } from "./api";
import { WAITING_ON_ADMIN } from "./status";

const jobKey = (id: string) => ["job", id] as const;

/** Screen 9: GET /api/jobs, active or history (C5.1). */
export function useMyJobs(filter: "active" | "history", page = 1) {
  const locale = useLocale();
  return useQuery({
    queryKey: ["jobs", filter, page, locale],
    queryFn: async () => orThrow(await api.GET("/api/jobs", { params: { query: { filter, page, pageSize: 20 } } })),
    placeholderData: keepPreviousData,
  });
}

/** Screen 10: GET /api/jobs/{id}; every 30 s while the job waits on the admin. */
export function useMyJob(id: string) {
  const locale = useLocale();
  return useQuery({
    queryKey: [...jobKey(id), locale],
    queryFn: async () => orThrow(await api.GET("/api/jobs/{id}", { params: { path: { id } } })),
    refetchInterval: (query) => (query.state.data && WAITING_ON_ADMIN.includes(query.state.data.status) ? 30_000 : false),
    retry: false,
  });
}

export function useCancellationPreview(id: string, enabled: boolean) {
  return useQuery({
    queryKey: [...jobKey(id), "cancellation-preview"],
    queryFn: async () => orThrow(await api.GET("/api/jobs/{id}/cancellation-preview", { params: { path: { id } } })),
    enabled,
  });
}

function useJobAction<TBody = void>(id: string, run: (body: TBody) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () => Promise.all([queryClient.invalidateQueries({ queryKey: jobKey(id) }), queryClient.invalidateQueries({ queryKey: ["jobs"] })]),
  });
}

export const useRejectQuote = (id: string) =>
  useJobAction(id, async (reason: string | null) =>
    orThrow(await api.POST("/api/jobs/{id}/reject-quote", { params: { path: { id } }, body: { reason } })),
  );

export const useAskQuestion = (id: string) =>
  useJobAction(id, async (message: string) => orThrow(await api.POST("/api/jobs/{id}/questions", { params: { path: { id } }, body: { message } })));

export const useCancelJob = (id: string) =>
  useJobAction(id, async (reason: string | null) => orThrow(await api.POST("/api/jobs/{id}/cancel", { params: { path: { id } }, body: { reason } })));

export const useRequote = (id: string) =>
  useJobAction(id, async () => orThrow(await api.POST("/api/jobs/{id}/requote", { params: { path: { id } } })));
