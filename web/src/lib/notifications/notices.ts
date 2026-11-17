import type { components } from "@/lib/api/schema";

export type NotificationDto = components["schemas"]["NotificationDto"];
export type NotificationKind = components["schemas"]["NotificationKind"];
export type Role = "client" | "painter" | "admin";

/** `job_scheduled` → `jobScheduled`, the key of its text in `notifications.kinds` (messages/{pt,es,en}.json). */
export const kindKey = (kind: NotificationKind) => kind.replace(/_(\w)/g, (_, c: string) => c.toUpperCase());

/** The values a notice's text uses: its job code plus its data (name, count…). */
export const textValues = (n: NotificationDto) => ({ code: n.jobCode ?? "", name: "", count: "", ...n.data });

/** Where a notice leads (docs/screens.md, «Avisos»): its job in the user's area, or the screen its kind belongs to. */
export function notificationHref(n: NotificationDto, role: Role): string {
  if (n.jobId) {
    if (role === "admin") return `/admin/trabajos/${n.jobId}`;
    if (role === "painter") return n.kind === "site_visit_assigned" ? `/pintor/visitas/${n.jobId}` : `/pintor/trabajos/${n.jobId}`;
    return `/mis-trabajos/${n.jobId}`;
  }
  if (role === "admin") {
    if (n.kind === "account_deletion_requested") return "/admin/ajustes";
    if (n.kind === "painter_documents_expiring") return "/admin/pintores";
    return "/admin/revision";
  }
  return role === "painter" ? "/pintor/pagos" : "/avisos";
}

/** «hace 5 min», «ayer»… in the interface's language. */
export function timeAgo(locale: string, iso: string, now: number = Date.now()): string {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
  const format = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [
    ["second", 60],
    ["minute", 60],
    ["hour", 24],
    ["day", 7],
    ["week", 4.35],
    ["month", 12],
  ];
  let value = seconds;
  for (const [unit, size] of steps) {
    if (Math.abs(value) < size) return format.format(Math.round(value), unit === "second" && Math.abs(value) < 45 ? "second" : unit);
    value /= size;
  }
  return format.format(Math.round(value), "year");
}

/** The bell's badge: the number, or «9+» from ten on. */
export const badgeText = (count: number) => (count > 9 ? "9+" : String(count));
