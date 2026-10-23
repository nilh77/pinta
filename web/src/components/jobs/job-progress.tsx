"use client";

import { useTranslations } from "next-intl";
import { progressIndex, PROGRESS_STEPS, type JobStatus } from "@/lib/jobs/status";
import { cn } from "@/lib/utils";

/** Revisión → Precio → Señal → Agenda → Ejecución → Terminado (screen 10). Hidden for stopped jobs. */
export function JobProgress({ status, compact = false }: { status: JobStatus; compact?: boolean }) {
  const t = useTranslations("jobs.progress");
  const current = progressIndex(status);
  if (current === null) return null;

  return (
    <div>
      <div
        role="progressbar"
        aria-label={t("label")}
        aria-valuemin={1}
        aria-valuemax={PROGRESS_STEPS.length}
        aria-valuenow={current + 1}
        aria-valuetext={t(PROGRESS_STEPS[current])}
        className="flex gap-1"
      >
        {PROGRESS_STEPS.map((step, i) => (
          <span key={step} className={cn("h-1.5 flex-1 rounded-full", i < current ? "bg-primary" : i === current ? "bg-accent" : "bg-border")} />
        ))}
      </div>
      {!compact && (
        <ol className="mt-2 hidden grid-cols-6 gap-1 text-[11px] text-muted-foreground sm:grid">
          {PROGRESS_STEPS.map((step, i) => (
            <li key={step} className={cn(i === current && "font-semibold text-foreground")}>
              {t(step)}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
