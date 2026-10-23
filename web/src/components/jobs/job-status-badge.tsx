"use client";

import { useTranslations } from "next-intl";
import { STATUS_TONE, TONE_CLASSES, type JobStatus } from "@/lib/jobs/status";
import { cn } from "@/lib/utils";

/** The status as a coloured pill; the same colours for the client and the admin. */
export function JobStatusBadge({ status, className }: { status: JobStatus; className?: string }) {
  const t = useTranslations("jobs.status");
  return (
    <span className={cn("inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap", TONE_CLASSES[STATUS_TONE[status]], className)}>
      {t(status)}
    </span>
  );
}
