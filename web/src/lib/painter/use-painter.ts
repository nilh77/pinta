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

/**
 * A photo of the job: signed URL, then straight to storage; it is attached by the call that uses it (finish, progress
 * or incident, P2).
 */
export async function uploadResultPhoto(
  jobId: string,
  blob: Blob,
  contentType: string,
  onProgress: (percent: number) => void,
  context: "result" | "progress" | "incident" = "result",
): Promise<string> {
  const upload = orThrow(
    await api.POST("/api/painter/jobs/{id}/media/upload-url", { params: { path: { id: jobId } }, body: { contentType, sizeBytes: blob.size, context } }),
  );
  await postToStorage(upload, blob, onProgress);
  return upload.fileId;
}

/** P2: progress photos the client sees while the job runs. */
export const useAddProgress = (id: string) =>
  usePainterAction(
    async (fileIds: string[]) => orThrow(await api.POST("/api/painter/jobs/{id}/progress", { params: { path: { id } }, body: { fileIds } })),
    id,
  );

/** P2: an incident with text and photos; the admins get an email. */
export const useReportIncident = (id: string) =>
  usePainterAction(
    async (body: { text: string; fileIds: string[] }) => orThrow(await api.POST("/api/painter/jobs/{id}/incidents", { params: { path: { id } }, body })),
    id,
  );

/** Where each role lands after signing in without a `next` path. */
export const homeFor = (role: string | undefined) => (role === "painter" ? "/pintor" : role === "admin" ? "/admin/revision" : "/");

export type PainterVisitItem = Schemas["PainterVisitItem"];
export type PainterPayouts = Schemas["PainterPayouts"];
const visitKey = (id: string) => ["painter", "visit", id] as const;

/** P3: the visit and what the client declared. */
export function usePainterVisit(id: string) {
  const locale = useLocale();
  return useQuery({
    queryKey: [...visitKey(id), locale],
    queryFn: async () => orThrow(await api.GET("/api/painter/visits/{id}", { params: { path: { id } } })),
    retry: false,
  });
}

export function useReportVisit(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["SiteVisitReportRequest"]) =>
      orThrow(await api.POST("/api/painter/visits/{id}/report", { params: { path: { id } }, body })),
    onSettled: () => Promise.all([queryClient.invalidateQueries({ queryKey: jobsKey }), queryClient.invalidateQueries({ queryKey: visitKey(id) })]),
  });
}

/** A visit photo: signed URL, then straight to storage; attached when the report is sent (P3.1). */
export async function uploadVisitPhoto(jobId: string, blob: Blob, contentType: string, onProgress: (percent: number) => void): Promise<string> {
  const upload = orThrow(
    await api.POST("/api/painter/visits/{id}/media/upload-url", { params: { path: { id: jobId } }, body: { contentType, sizeBytes: blob.size } }),
  );
  await postToStorage(upload, blob, onProgress);
  return upload.fileId;
}

/** P4: what is still owed and what was paid in `month` (YYYY-MM). */
export function usePainterPayouts(month: string) {
  return useQuery({
    queryKey: ["painter", "payouts", month],
    queryFn: async () => orThrow(await api.GET("/api/painter/payouts", { params: { query: { month } } })),
  });
}
