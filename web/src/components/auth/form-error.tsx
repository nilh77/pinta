"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { ApiError } from "@/lib/api/problem";

/** Form-level error from a ProblemDetails `code` (field errors go under each field). */
export function FormError({ error, children }: { error: unknown; children?: ReactNode }) {
  const t = useTranslations("errors");
  if (!error) return null;
  const code = error instanceof ApiError ? error.code : "internal_error";

  return (
    <div role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 px-3.5 py-3 text-sm text-destructive">
      {t(code)} {children}
    </div>
  );
}
