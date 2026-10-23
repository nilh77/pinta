import type { components } from "@/lib/api/schema";

export type JobStatus = components["schemas"]["JobStatus"];

export type StatusTone = "waiting" | "action" | "progress" | "done" | "stopped";

/** One colour per status, shared by the client and admin screens (badges, board columns). */
export const STATUS_TONE: Record<JobStatus, StatusTone> = {
  pending_email: "waiting",
  in_review: "waiting",
  site_visit: "progress",
  quoted: "action",
  expired: "stopped",
  rejected: "stopped",
  deposit_paid: "progress",
  scheduled: "progress",
  in_progress: "progress",
  finished: "done",
  closed: "done",
  cancelled: "stopped",
};

export const TONE_CLASSES: Record<StatusTone, string> = {
  waiting: "bg-primary-soft text-[#0b5f59]",
  action: "bg-accent-soft text-accent-ink",
  progress: "bg-[#e0ecff] text-[#1d4ed8]",
  done: "bg-primary text-primary-foreground",
  stopped: "bg-[#eef1f0] text-muted-foreground",
};

/** Every status in the order of docs/states.md, for filters and the admin board. */
export const ALL_STATUSES: JobStatus[] = [
  "pending_email",
  "in_review",
  "site_visit",
  "quoted",
  "expired",
  "rejected",
  "deposit_paid",
  "scheduled",
  "in_progress",
  "finished",
  "closed",
  "cancelled",
];

/** Steps of the client's progress line (screen 10): Revisión → Precio → Señal → Agenda → Ejecución → Terminado. */
export const PROGRESS_STEPS = ["review", "price", "deposit", "schedule", "work", "done"] as const;
export type ProgressStep = (typeof PROGRESS_STEPS)[number];

/** Index of the current step, or null when the job stopped (expired, rejected, cancelled). */
export function progressIndex(status: JobStatus): number | null {
  switch (status) {
    case "pending_email":
    case "in_review":
    case "site_visit":
      return 0;
    case "quoted":
      return 1;
    case "deposit_paid":
      return 2;
    case "scheduled":
      return 3;
    case "in_progress":
      return 4;
    case "finished":
    case "closed":
      return 5;
    default:
      return null;
  }
}

/** States in which the client waits for the admin: the detail refreshes every 30 s (screen 10). */
export const WAITING_ON_ADMIN: JobStatus[] = ["pending_email", "in_review", "site_visit"];
