"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { orThrow, type Schemas } from "@/lib/jobs/api";

export type MonthSummary = Schemas["MonthSummary"];
export type CostDto = Schemas["CostDto"];
export type CostRequest = Schemas["CostRequest"];
export type CostCategory = Schemas["OperatingCostCategory"];
export type DeletionRequest = Schemas["DeletionRequest"];

/** The closed list of the partners' agreement (docs/data-model.md). */
export const COST_CATEGORIES: CostCategory[] = ["infrastructure", "payment_fees", "accounting", "insurance", "marketing"];

const summaryKey = (month: string) => ["admin", "finance", month] as const;

/** The current month in Lisbon as YYYY-MM. */
export const currentMonth = () => new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Lisbon" }).slice(0, 7);

/** Today in Lisbon as YYYY-MM-DD. */
export const todayInLisbon = () => new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Lisbon" });

/** A5.1: the month's figures, frozen when it is closed. */
export function useFinanceSummary(month: string) {
  return useQuery({
    queryKey: summaryKey(month),
    queryFn: async () => orThrow(await api.GET("/api/admin/finance/summary", { params: { query: { month } } })),
    placeholderData: keepPreviousData,
  });
}

/** After any change every month may be stale (a cost can move between months, a payout belongs to all of them). */
function useFinanceMutation<TBody, TResult = unknown>(run: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () =>
      Promise.all([queryClient.invalidateQueries({ queryKey: ["admin", "finance"] }), queryClient.invalidateQueries({ queryKey: ["admin", "payouts"] })]),
  });
}

export const useAddCost = () =>
  useFinanceMutation(async (body: CostRequest) => orThrow(await api.POST("/api/admin/finance/costs", { body })));

export const useUpdateCost = () =>
  useFinanceMutation(async ({ id, body }: { id: string; body: CostRequest }) =>
    orThrow(await api.PUT("/api/admin/finance/costs/{id}", { params: { path: { id } }, body })),
  );

export const useDeleteCost = () =>
  useFinanceMutation(async (id: string) => orThrow(await api.DELETE("/api/admin/finance/costs/{id}", { params: { path: { id } } })));

export const useCloseMonth = () =>
  useFinanceMutation(async (month: string) => orThrow(await api.POST("/api/admin/finance/closings", { body: { month } })));

export const useMarkPayoutPaid = () =>
  useFinanceMutation(async ({ id, paidOn, reference }: { id: string; paidOn: string; reference: string }) =>
    orThrow(await api.POST("/api/admin/payouts/{id}/mark-paid", { params: { path: { id } }, body: { paidOn, reference } })),
  );

export const exportFinanceUrl = (month: string) => `/api/admin/finance/export?month=${encodeURIComponent(month)}`;

/** C5.8: clients waiting for the deletion of their account. */
export function useDeletionRequests() {
  return useQuery({
    queryKey: ["admin", "deletion-requests"],
    queryFn: async () => orThrow(await api.GET("/api/admin/deletion-requests")),
  });
}

export function useExecuteDeletion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (userId: string) =>
      orThrow(await api.POST("/api/admin/deletion-requests/{userId}/execute", { params: { path: { userId } } })),
    onSettled: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["admin", "deletion-requests"] }), queryClient.invalidateQueries({ queryKey: ["admin", "jobs"] })]),
  });
}

/** H8: «Restablecer datos de demo» (Development only; 404 elsewhere). Everything cached is stale afterwards. */
export function useResetDemo() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => orThrow(await api.POST("/api/admin/demo/reset")),
    onSuccess: () => queryClient.invalidateQueries(),
  });
}
