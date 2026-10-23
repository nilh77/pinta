"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { meQueryKey } from "@/lib/auth/use-auth";
import { orThrow, type Schemas } from "@/lib/jobs/api";

export type AccountProfile = Schemas["AccountProfile"];
export type UpdateAccountRequest = Schemas["UpdateAccountRequest"];

const accountKey = ["account"] as const;

/** Screen 12 (C5.8): GET /api/me. */
export function useAccount() {
  return useQuery({ queryKey: accountKey, queryFn: async () => orThrow(await api.GET("/api/me")) });
}

/** PUT /api/me; the session's name, email and language change with it. */
export function useUpdateAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: UpdateAccountRequest) => orThrow(await api.PUT("/api/me", { body })),
    onSuccess: (profile) => {
      queryClient.setQueryData(accountKey, profile);
      return queryClient.invalidateQueries({ queryKey: meQueryKey });
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (body: Schemas["ChangePasswordRequest"]) => orThrow(await api.POST("/api/me/password", { body })),
  });
}

export function useRequestDeletion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => orThrow(await api.POST("/api/me/delete-request")),
    onSettled: () => queryClient.invalidateQueries({ queryKey: accountKey }),
  });
}
