"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { orThrow, type Schemas } from "@/lib/jobs/api";
import { postToStorage } from "@/lib/quote/uploads";

export type PainterListItem = Schemas["PainterListItem"];
export type PainterDetail = Schemas["PainterDetail"];
export type CreatePainterRequest = Schemas["CreatePainterRequest"];
export type UpdatePainterRequest = Schemas["UpdatePainterRequest"];
export type PainterDocumentKind = Schemas["PainterDocumentKind"];
export type AssignmentDto = Schemas["AssignmentDto"];

export const SPECIALTIES = ["interior", "exterior", "wood", "metal"] as const;
export const DOCUMENT_KINDS: PainterDocumentKind[] = ["insurance", "activity", "contract", "other"];

const paintersKey = ["admin", "painters"] as const;
const painterKey = (id: string) => ["admin", "painter", id] as const;
const path = (id: string) => ({ params: { path: { id } } });

/** A4.1: every painter with status, zones, jobs done and rating. */
export function useAdminPainters() {
  return useQuery({ queryKey: paintersKey, queryFn: async () => orThrow(await api.GET("/api/admin/painters")) });
}

export function useAdminPainter(id: string) {
  return useQuery({
    queryKey: painterKey(id),
    queryFn: async () => orThrow(await api.GET("/api/admin/painters/{id}", path(id))),
    retry: false,
  });
}

/** Concelhos Pinta covers (A1.7): the zones a painter can work in. */
export function useCoveredMunicipalities() {
  return useQuery({
    queryKey: ["admin", "coverage"],
    queryFn: async () => orThrow(await api.GET("/api/admin/coverage")),
    select: (areas) => [...new Set(areas.map((a) => a.municipality))].sort((a, b) => a.localeCompare(b)),
    staleTime: 5 * 60_000,
  });
}

/** After any change the painter and the list are stale. */
function usePainterMutation<TBody, TResult = unknown>(id: string | null, run: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: paintersKey }),
        id ? queryClient.invalidateQueries({ queryKey: painterKey(id) }) : undefined,
      ]),
  });
}

/** A4.2: creates the painter and sends the invitation. */
export const useCreatePainter = () =>
  usePainterMutation(null, async (body: CreatePainterRequest) => orThrow(await api.POST("/api/admin/painters", { body })));

export const useUpdatePainter = (id: string) =>
  usePainterMutation(id, async (body: UpdatePainterRequest) => orThrow(await api.PUT("/api/admin/painters/{id}", { ...path(id), body })));

export const useResendInvitation = (id: string) =>
  usePainterMutation(id, async () => orThrow(await api.POST("/api/admin/painters/{id}/invite", path(id))));

export const useSetPainterActive = (id: string) =>
  usePainterMutation(id, async (active: boolean) =>
    orThrow(await api.POST(active ? "/api/admin/painters/{id}/activate" : "/api/admin/painters/{id}/deactivate", path(id))),
  );

/** A4.3: the file straight to storage, then the document with its kind and expiry. */
export const useAddDocument = (id: string) =>
  usePainterMutation(id, async ({ file, kind, expiresOn }: { file: File; kind: PainterDocumentKind; expiresOn: string | null }) => {
    const upload = orThrow(
      await api.POST("/api/admin/painters/{id}/documents/upload-url", { ...path(id), body: { contentType: file.type, sizeBytes: file.size } }),
    );
    await postToStorage(upload, file, () => {});
    return orThrow(await api.POST("/api/admin/painters/{id}/documents", { ...path(id), body: { fileId: upload.fileId, kind, expiresOn } }));
  });

export const useDeleteDocument = (id: string) =>
  usePainterMutation(id, async (documentId: string) =>
    orThrow(await api.DELETE("/api/admin/painters/{id}/documents/{documentId}", { params: { path: { id, documentId } } })),
  );

/** A5.4 (read-only until H7): the painter's payouts, newest first. */
export function usePainterPayoutsForAdmin(painterId: string) {
  return useQuery({
    queryKey: ["admin", "payouts", painterId],
    queryFn: async () => orThrow(await api.GET("/api/admin/payouts", { params: { query: { painterId } } })),
  });
}

/** A3.2: active painters covering the job's concelho. */
export function useAssignablePainters(jobId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["admin", "job", jobId, "assignable"],
    queryFn: async () => orThrow(await api.GET("/api/admin/jobs/{id}/assignable-painters", path(jobId))),
    enabled,
  });
}

export function useOfferJob(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["OfferJobRequest"]) => orThrow(await api.POST("/api/admin/jobs/{id}/assignments", { ...path(jobId), body })),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["admin", "job", jobId] }),
  });
}
