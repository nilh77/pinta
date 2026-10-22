import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "@/lib/api/problem";

/** Puts the API's 422 field errors under their fields and focuses the first one. Returns true if any applied. */
export function applyServerFieldErrors<T extends FieldValues>(
  error: unknown,
  fields: readonly Path<T>[],
  setError: UseFormSetError<T>,
): boolean {
  if (!(error instanceof ApiError) || error.code !== "validation_failed") return false;
  const errors = error.problem?.errors ?? {};
  const matching = fields.filter((field) => errors[field]?.length);
  matching.forEach((field, index) =>
    setError(field, { type: "server", message: errors[field]![0] }, { shouldFocus: index === 0 }),
  );
  return matching.length > 0;
}
