import { describe, expect, it } from "vitest";
import messages from "../../../messages/es.json";
import { badgeText, kindKey, notificationHref, timeAgo, type NotificationDto } from "./notices";
import type { components } from "@/lib/api/schema";

const notice = (over: Partial<NotificationDto> = {}): NotificationDto => ({
  id: "n1",
  kind: "job_scheduled",
  jobId: "job-1",
  jobCode: "PT-2026-0104",
  data: {},
  createdAt: "2026-11-17T10:00:00Z",
  readAt: null,
  ...over,
});

const ALL_KINDS: components["schemas"]["NotificationKind"][] = [
  "request_received", "client_question", "quote_confirmed", "quote_rejected", "quote_expiring", "quote_expired", "request_rejected",
  "site_visit_scheduled", "site_visit_assigned", "site_visit_reported", "multibanco_reference", "payment_received", "deposit_paid",
  "balance_paid", "offer_received", "offer_withdrawn", "offer_accepted", "offer_declined", "offer_unanswered", "job_scheduled",
  "job_started", "progress_photos", "incident_reported", "job_finished", "balance_due", "job_cancelled", "refund_issued",
  "refund_completed", "payment_dispute", "payout_paid", "account_deletion_requested", "reviews_overdue", "painter_documents_expiring",
];

describe("notices (docs/screens.md, «Avisos»)", () => {
  it("has a text for every kind of notice", () => {
    const kinds = messages.notifications.kinds as Record<string, string>;
    expect(ALL_KINDS.filter((k) => !kinds[kindKey(k)])).toEqual([]);
  });

  it("leads each role to the job in its own area", () => {
    expect(notificationHref(notice(), "client")).toBe("/mis-trabajos/job-1");
    expect(notificationHref(notice(), "admin")).toBe("/admin/trabajos/job-1");
    expect(notificationHref(notice({ kind: "site_visit_assigned" }), "painter")).toBe("/pintor/visitas/job-1");
    expect(notificationHref(notice({ kind: "progress_photos" }), "painter")).toBe("/pintor/trabajos/job-1");
    // An offer is not the painter's job yet: it opens the offers list.
    expect(notificationHref(notice({ kind: "offer_received" }), "painter")).toBe("/pintor");
    expect(notificationHref(notice({ kind: "job_cancelled" }), "painter")).toBe("/pintor");
    expect(notificationHref(notice({ kind: "reviews_overdue", jobId: null }), "admin")).toBe("/admin/revision");
    expect(notificationHref(notice({ kind: "account_deletion_requested", jobId: null }), "admin")).toBe("/admin/ajustes");
  });

  it("says how long ago and caps the badge", () => {
    const now = Date.parse("2026-11-17T12:00:00Z");
    expect(timeAgo("es", "2026-11-17T11:55:00Z", now)).toBe("hace 5 minutos");
    expect(timeAgo("es", "2026-11-16T12:00:00Z", now)).toBe("ayer");
    expect([badgeText(3), badgeText(12)]).toEqual(["3", "9+"]);
  });
});
