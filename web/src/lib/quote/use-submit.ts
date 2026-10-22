"use client";

import { useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { ApiError, type Problem } from "@/lib/api/problem";
import type { SubmitJobRequest } from "./request";

/** The request just sent, so screen 8 can load it after the draft is cleared. */
export const LAST_REQUEST_KEY = "pinta.quote.lastRequest";
export type LastRequest = { id: string; code: string };

export function rememberLastRequest(request: LastRequest) {
  try {
    sessionStorage.setItem(LAST_REQUEST_KEY, JSON.stringify(request));
  } catch {
    // Without storage the confirmation still shows the code from the URL.
  }
}

export function readLastRequest(code: string): LastRequest | null {
  try {
    const stored = JSON.parse(sessionStorage.getItem(LAST_REQUEST_KEY) ?? "null") as LastRequest | null;
    return stored?.code === code ? stored : null;
  } catch {
    return null;
  }
}

/** POST /api/jobs with the draft's Idempotency-Key: retrying after a network error never creates a second job (C4.4). */
export function useSubmitJob() {
  return useMutation({
    mutationFn: async ({ body, idempotencyKey }: { body: SubmitJobRequest; idempotencyKey: string }) => {
      const result = await api.POST("/api/jobs", { body, headers: { "Idempotency-Key": idempotencyKey } });
      if (result.error !== undefined) throw new ApiError(result.error as Problem);
      return result.data!;
    },
  });
}
