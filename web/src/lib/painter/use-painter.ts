"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale } from "next-intl";
import { api, resetAntiforgeryToken } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { meQueryKey } from "@/lib/auth/use-auth";
import { orThrow } from "@/lib/jobs/api";
import { postToStorage } from "@/lib/quote/uploads";

type Schemas = components["schemas"];
export type PainterProfile = Schemas["PainterProfile"];
export type UpdateProfileRequest = Schemas["UpdateProfileRequest"];
export type PainterJobs = Schemas["PainterJobs"];
export type PainterJobItem = Schemas["PainterJobItem"];
export type PainterJobDetail = Schemas["PainterJobDetail"];
export type PainterElement = Schemas["PainterElement"];

const profileKey = ["painter", "me"] as const;
const jobsKey = ["painter", "jobs"] as const;
const jobKey = (id: string) => ["painter", "job", id] as const;

/** POST /api/auth/accept-invite: sets the password and signs in (A4.2). */
export function useAcceptInvite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["AcceptInviteRequest"]) => orThrow(await api.POST("/api/auth/accept-invite", { body })),
    onSuccess: (me) => {
      resetAntiforgeryToken();
      queryClient.setQueryData(meQueryKey, me);
    },
  });
}

/** GET /api/painter/me: the profile the admin prefilled (A4.2). */
export function usePainterProfile(enabled = true) {
  return useQuery({
    queryKey: profileKey,
    queryFn: async () => orThrow(await api.GET("/api/painter/me")),
    enabled,
  });
}

export function useUpdatePainterProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: UpdateProfileRequest) => orThrow(await api.PUT("/api/painter/me", { body })),
    onSuccess: (profile) => queryClient.setQueryData(profileKey, profile),
  });
}

/** P1: offers, upcoming, in progress and done. */
export function usePainterJobs() {
  const locale = useLocale();
  return useQuery({
    queryKey: [...jobsKey, locale],
    queryFn: async () => orThrow(await api.GET("/api/painter/jobs")),
  });
}

/** P2: the job to carry out. */
export function usePainterJob(id: string) {
  const locale = useLocale();
  return useQuery({
    queryKey: [...jobKey(id), locale],
    queryFn: async () => orThrow(await api.GET("/api/painter/jobs/{id}", { params: { path: { id } } })),
    retry: false,
  });
}

function usePainterAction<TBody = void>(run: (body: TBody) => Promise<unknown>, jobId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: jobsKey }),
        jobId ? queryClient.invalidateQueries({ queryKey: jobKey(jobId) }) : undefined,
      ]),
  });
}

export const useAcceptOffer = () =>
  usePainterAction(async (assignmentId: string) =>
    orThrow(await api.POST("/api/painter/assignments/{id}/accept", { params: { path: { id: assignmentId } } })),
  );

export const useDeclineOffer = () =>
  usePainterAction(async ({ assignmentId, reason }: { assignmentId: string; reason: string }) =>
    orThrow(await api.POST("/api/painter/assignments/{id}/decline", { params: { path: { id: assignmentId } }, body: { reason } })),
  );

export const useStartJob = (id: string) =>
  usePainterAction(async () => orThrow(await api.POST("/api/painter/jobs/{id}/start", { params: { path: { id } } })), id);

export const useFinishJob = (id: string) =>
  usePainterAction(
    async (fileIds: string[]) => orThrow(await api.POST("/api/painter/jobs/{id}/finish", { params: { path: { id } }, body: { fileIds } })),
    id,
  );

/** A result photo: signed URL for the job, then straight to storage; confirmed when the job is finished (P2.2). */
export async function uploadResultPhoto(jobId: string, blob: Blob, contentType: string, onProgress: (percent: number) => void): Promise<string> {
  const upload = orThrow(
    await api.POST("/api/painter/jobs/{id}/media/upload-url", { params: { path: { id: jobId } }, body: { contentType, sizeBytes: blob.size } }),
  );
  await postToStorage(upload, blob, onProgress);
  return upload.fileId;
}

/** Where each role lands after signing in without a `next` path. */
export const homeFor = (role: string | undefined) => (role === "painter" ? "/pintor" : role === "admin" ? "/admin/revision" : "/");
