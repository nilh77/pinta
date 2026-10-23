"use client";

import { useTranslations } from "next-intl";
import { ApiError } from "./problem";

/** The message for an API error, by its stable `code` (never the title). */
export function useErrorText() {
  const t = useTranslations("errors");
  return (error: unknown) => t(error instanceof ApiError ? error.code : "internal_error");
}
