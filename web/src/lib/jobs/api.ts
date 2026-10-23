import { ApiError, type Problem } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";

export type Schemas = components["schemas"];
export type ClientJobDetail = Schemas["ClientJobDetail"];
export type ClientJobListItem = Schemas["ClientJobListItem"];
export type AdminJobDetail = Schemas["AdminJobDetail"];
export type AdminJobListItem = Schemas["AdminJobListItem"];
export type AdminQuote = Schemas["AdminQuote"];
export type ReviewItem = Schemas["ReviewItem"];
export type DraftQuoteRequest = Schemas["DraftQuoteRequest"];
export type CancellationPreview = Schemas["CancellationPreview"];

/** openapi-fetch result → data, or an ApiError with the ProblemDetails. */
export function orThrow<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.error !== undefined || !result.response.ok) throw new ApiError(result.error as Problem | undefined);
  return result.data as T;
}
