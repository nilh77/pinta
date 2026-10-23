import type { components } from "./schema";

export type Problem = components["schemas"]["HttpValidationProblemDetails"];

/** Error codes the UI has a message for (messages: errors.*). Anything else shows the generic error. */
export const KNOWN_ERROR_CODES = [
  "unauthenticated",
  "invalid_credentials",
  "forbidden",
  "antiforgery_failed",
  "not_found",
  "email_taken",
  "concurrency_conflict",
  "invalid_transition",
  "validation_failed",
  "rate_limited",
  "internal_error",
] as const;

export type KnownErrorCode = (typeof KNOWN_ERROR_CODES)[number];

/** The frontend translates by `code`, never by `title` (docs/api.md). */
export function errorCode(problem: unknown): KnownErrorCode {
  const code = (problem as { code?: unknown } | null)?.code;
  return KNOWN_ERROR_CODES.includes(code as KnownErrorCode) ? (code as KnownErrorCode) : "internal_error";
}

/** Thrown by mutations so TanStack Query exposes the ProblemDetails body. */
export class ApiError extends Error {
  constructor(public readonly problem: Problem | undefined) {
    super(problem?.code ?? "internal_error");
  }

  get code(): KnownErrorCode {
    return errorCode(this.problem);
  }
}
