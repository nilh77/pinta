"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, resetAntiforgeryToken } from "@/lib/api/client";
import { ApiError, type Problem } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";

export type Me = components["schemas"]["MeResponse"];
type LoginRequest = components["schemas"]["LoginRequest"];
type RegisterRequest = components["schemas"]["RegisterRequest"];
type ConfirmEmailRequest = components["schemas"]["ConfirmEmailRequest"];

export const meQueryKey = ["me"] as const;

function orThrow<T>(result: { data?: T; error?: unknown }): T {
  if (result.error !== undefined) throw new ApiError(result.error as Problem);
  return result.data as T;
}

/** The signed-in user, or null without a session (GET /api/auth/me). */
export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async (): Promise<Me | null> => {
      const result = await api.GET("/api/auth/me");
      if (result.response.status === 401) return null;
      return orThrow(result);
    },
    staleTime: 60_000,
    retry: false,
  });
}

function useSessionChange() {
  const queryClient = useQueryClient();
  return (me: Me | null) => {
    resetAntiforgeryToken();
    queryClient.setQueryData(meQueryKey, me);
  };
}

export function useSignIn() {
  const onSessionChange = useSessionChange();
  return useMutation({
    mutationFn: async (body: LoginRequest) => orThrow(await api.POST("/api/auth/login", { body })),
    onSuccess: onSessionChange,
  });
}

export function useRegister() {
  const onSessionChange = useSessionChange();
  return useMutation({
    mutationFn: async (body: RegisterRequest) => orThrow(await api.POST("/api/auth/register", { body })),
    onSuccess: onSessionChange,
  });
}

export function useSignOut() {
  const onSessionChange = useSessionChange();
  return useMutation({
    mutationFn: async () => {
      orThrow(await api.POST("/api/auth/logout"));
    },
    onSuccess: () => onSessionChange(null),
  });
}

export function useConfirmEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: ConfirmEmailRequest) => {
      orThrow(await api.POST("/api/auth/confirm-email", { body }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meQueryKey }),
  });
}

export function useResendConfirmation() {
  return useMutation({
    mutationFn: async () => {
      orThrow(await api.POST("/api/auth/resend-confirmation"));
    },
  });
}
