"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { orThrow } from "@/lib/jobs/api";
import { useMe } from "@/lib/auth/use-auth";

const KEY = ["notifications"] as const;

/** The bell's number (docs/screens.md, «Avisos»): every 30 s and when the tab comes back, only with a session. */
export function useUnreadCount() {
  const me = useMe();
  return useQuery({
    queryKey: [...KEY, "unread"],
    queryFn: async () => orThrow(await api.GET("/api/notifications/unread-count")).count,
    enabled: !!me.data,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

export function useNotifications(page = 1, pageSize = 20, enabled = true) {
  return useQuery({
    queryKey: [...KEY, "list", page, pageSize],
    queryFn: async () => orThrow(await api.GET("/api/notifications", { params: { query: { page, pageSize } } })),
    enabled,
    placeholderData: keepPreviousData,
  });
}

export function useMarkRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => orThrow(await api.POST("/api/notifications/{id}/read", { params: { path: { id } } })),
    onSettled: () => queryClient.invalidateQueries({ queryKey: KEY }),
  });
}

export function useMarkAllRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => orThrow(await api.POST("/api/notifications/read-all")),
    onSettled: () => queryClient.invalidateQueries({ queryKey: KEY }),
  });
}
