"use client";

import { Download } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { formatCents, formatDateTime } from "@/lib/format";
import { receiptUrl, type Payment } from "@/lib/payments/use-payments";
import { cn } from "@/lib/utils";

const STATUS_CLASSES: Record<Payment["status"], string> = {
  pending: "bg-[#eef1f0] text-muted-foreground",
  requires_action: "bg-accent-soft text-accent-ink",
  succeeded: "bg-primary-soft text-[#0b5f59]",
  failed: "bg-destructive/10 text-destructive",
  cancelled: "bg-[#eef1f0] text-muted-foreground",
  refunded: "bg-[#e0ecff] text-[#1d4ed8]",
  partially_refunded: "bg-[#e0ecff] text-[#1d4ed8]",
};

/** Payments of a job with status, method, refunds and the receipt (client screen 10 and admin Pagos tab). */
export function PaymentsList({ jobId, payments, emptyText }: { jobId: string; payments: Payment[]; emptyText: string }) {
  const t = useTranslations("payments");
  const locale = useLocale();
  if (payments.length === 0) return <p className="text-sm text-muted-foreground">{emptyText}</p>;

  return (
    <ul className="flex flex-col gap-2">
      {payments.map((p) => (
        <li key={p.id} className="flex flex-col gap-1.5 rounded-2xl border border-border bg-card p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="font-semibold">
              {t(`type.${p.type}`)} · {formatCents(locale, p.amountCents)}
            </span>
            <span className={cn("rounded-full px-2.5 py-0.5 text-xs font-semibold", STATUS_CLASSES[p.status])}>{t(`status.${p.status}`)}</span>
          </div>
          <p className="text-xs text-muted-foreground">
            {p.method && `${t(`method.${p.method}`)} · `}
            {p.paidAt ? t("paidAt", { date: formatDateTime(locale, p.paidAt) }) : t("createdAt", { date: formatDateTime(locale, p.createdAt) })}
            {p.receiptNumber && ` · ${p.receiptNumber}`}
          </p>
          {p.multibanco && p.status === "requires_action" && (
            <p className="text-xs">{t("multibanco", { entity: p.multibanco.entity, reference: p.multibanco.reference })}</p>
          )}
          {p.error && p.status === "failed" && <p className="text-xs text-destructive">{p.error}</p>}
          {p.refunds.map((r) => (
            <p key={r.id} className="text-xs">
              {t(`refund.${r.status}`, { amount: formatCents(locale, r.amountCents) })}
              {r.reason && ` · ${r.reason}`}
            </p>
          ))}
          {p.receiptNumber && (
            <a
              href={receiptUrl(jobId, p.id)}
              download
              className="inline-flex min-h-9 items-center gap-1.5 self-start text-sm font-semibold text-primary underline-offset-4 hover:underline"
            >
              <Download aria-hidden="true" className="size-4" />
              {t("receipt")}
            </a>
          )}
        </li>
      ))}
    </ul>
  );
}
