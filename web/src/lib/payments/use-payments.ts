"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { orThrow } from "@/lib/jobs/api";

export type PaymentType = components["schemas"]["PaymentType"];
export type Payment = components["schemas"]["PaymentDto"];
export type CreatedPayment = components["schemas"]["CreatePaymentResponse"];

export const PAYMENT_TYPES: PaymentType[] = ["deposit", "balance", "site_visit"];

export const isPaymentType = (value: string | null | undefined): value is PaymentType => PAYMENT_TYPES.includes(value as PaymentType);

const paymentsKey = (jobId: string) => ["job", jobId, "payments"] as const;

/** GET /api/jobs/{id}/payments; `pollWhile` keeps asking every 3 s (waiting for Stripe's webhook). */
export function useJobPayments(jobId: string, pollWhile?: (payments: Payment[]) => boolean) {
  return useQuery({
    queryKey: paymentsKey(jobId),
    queryFn: async () => orThrow(await api.GET("/api/jobs/{id}/payments", { params: { path: { id: jobId } } })),
    refetchInterval: (query) => (pollWhile && query.state.data && pollWhile(query.state.data) ? 3000 : false),
  });
}

export function useAdminJobPayments(jobId: string) {
  return useQuery({
    queryKey: ["admin", "job", jobId, "payments"],
    queryFn: async () => orThrow(await api.GET("/api/admin/jobs/{id}/payments", { params: { path: { id: jobId } } })),
  });
}

/** POST /api/jobs/{id}/payments with one Idempotency-Key per visit to the screen: going back never charges twice. */
export function useCreatePayment(jobId: string, idempotencyKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (type: PaymentType) =>
      orThrow(
        await api.POST("/api/jobs/{id}/payments", {
          params: { path: { id: jobId } },
          body: { type },
          headers: { "Idempotency-Key": idempotencyKey },
        }),
      ),
    onSettled: () => queryClient.invalidateQueries({ queryKey: paymentsKey(jobId) }),
  });
}

export const receiptUrl = (jobId: string, paymentId: string) => `/api/jobs/${jobId}/payments/${paymentId}/receipt`;

/** Seconds left of MB WAY's 5-minute window to accept in the app (screen 11). */
export const MB_WAY_WINDOW_SECONDS = 5 * 60;

export function formatCountdown(seconds: number): string {
  const s = Math.max(0, seconds);
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}
