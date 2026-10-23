"use client";

import { ArrowLeft, FileText, Loader2, Mail, Trash2, Upload } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatCents, formatDate } from "@/lib/format";
import {
  DOCUMENT_KINDS,
  useAddDocument,
  useAdminPainter,
  useDeleteDocument,
  useResendInvitation,
  useSetPainterActive,
  usePainterPayoutsForAdmin,
  useUpdatePainter,
  type PainterDetail,
  type PainterDocumentKind,
} from "@/lib/painter/use-admin-painters";
import { PainterForm } from "./painter-form";
import { PainterStatusBadge } from "./painter-status-badge";

/** Ficha del pintor (A4.1–A4.4): data, invitation, activation, documents with expiry and jobs. */
export function PainterScreen({ id }: { id: string }) {
  const t = useTranslations("admin.painters");
  const painter = useAdminPainter(id);

  if (painter.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-12 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (painter.isError) {
    const notFound = painter.error instanceof ApiError && painter.error.code === "not_found";
    return (
      <div role="alert" className="flex flex-col items-start gap-3 py-12">
        <p className="font-semibold">{t(notFound ? "notFound" : "loadError")}</p>
        {!notFound && <Button onClick={() => painter.refetch()}>{t("retry")}</Button>}
        <Button asChild variant="outline">
          <Link href="/admin/pintores">{t("back")}</Link>
        </Button>
      </div>
    );
  }

  const data = painter.data;
  return (
    <div className="flex flex-col gap-5">
      <Link href="/admin/pintores" className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("back")}
      </Link>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-[28px] font-extrabold tracking-tight">{data.name}</h1>
          <PainterStatusBadge status={data.status} />
        </div>
        <StatusActions painter={data} />
      </header>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
        <section aria-labelledby="data-title" className="rounded-[20px] border border-border bg-card p-4">
          <h2 id="data-title" className="mb-3 font-bold">
            {t("detail.data")}
          </h2>
          <EditForm key={data.version} painter={data} />
        </section>
        <div className="flex flex-col gap-5">
          <Documents painter={data} />
          <Jobs painter={data} />
          <Payouts painterId={data.id} />
        </div>
      </div>
    </div>
  );
}

function EditForm({ painter }: { painter: PainterDetail }) {
  const t = useTranslations("admin.painters.detail");
  const update = useUpdatePainter(painter.id);
  return (
    <PainterForm
      painter={painter}
      pending={update.isPending}
      error={update.error}
      submitLabel={t("save")}
      onSubmit={(values) =>
        update.mutate(
          {
            name: values.name,
            phone: values.phone,
            taxId: values.taxId,
            specialties: values.specialties,
            municipalities: values.municipalities,
            iban: values.iban,
            notes: values.notes,
            rating: values.rating,
            version: painter.version,
          },
          { onSuccess: () => toast.success(t("saved")) },
        )
      }
    />
  );
}

/** Invited → resend the link; active ↔ inactive (a deactivated painter cannot sign in or get offers). */
function StatusActions({ painter }: { painter: PainterDetail }) {
  const t = useTranslations("admin.painters.detail");
  const errorText = useErrorText();
  const resend = useResendInvitation(painter.id);
  const setActive = useSetPainterActive(painter.id);
  const [confirming, setConfirming] = useState(false);

  return (
    <div className="flex flex-wrap gap-2">
      {!painter.signedUp && (
        <Button
          variant="outline"
          disabled={resend.isPending}
          onClick={() => resend.mutate(undefined, { onSuccess: () => toast.success(t("resent")), onError: (e) => toast.error(errorText(e)) })}
        >
          {resend.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Mail aria-hidden="true" />}
          {t("resend")}
        </Button>
      )}
      {painter.status === "active" ? (
        <Button variant="outline" disabled={setActive.isPending} onClick={() => setConfirming(true)}>
          {t("deactivate")}
        </Button>
      ) : (
        painter.signedUp && (
          <Button
            disabled={setActive.isPending}
            onClick={() => setActive.mutate(true, { onSuccess: () => toast.success(t("activated")), onError: (e) => toast.error(errorText(e)) })}
          >
            {t("activate")}
          </Button>
        )
      )}
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("deactivateTitle", { name: painter.name })}
        description={t("deactivateBody")}
        confirmLabel={t("deactivate")}
        cancelLabel={t("cancel")}
        onConfirm={() => setActive.mutate(false, { onSuccess: () => toast.success(t("deactivated")), onError: (e) => toast.error(errorText(e)) })}
      />
    </div>
  );
}

/** A4.3: insurance, activity and contract with expiry; expired ones stand out. */
function Documents({ painter }: { painter: PainterDetail }) {
  const t = useTranslations("admin.painters.documents");
  const locale = useLocale();
  const errorText = useErrorText();
  const add = useAddDocument(painter.id);
  const remove = useDeleteDocument(painter.id);
  const input = useRef<HTMLInputElement>(null);
  const [kind, setKind] = useState<PainterDocumentKind>("insurance");
  const [expiresOn, setExpiresOn] = useState("");
  const fieldClass =
    "h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

  return (
    <section aria-labelledby="documents-title" className="rounded-[20px] border border-border bg-card p-4">
      <h2 id="documents-title" className="mb-3 font-bold">
        {t("title")}
      </h2>
      {painter.documents.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {painter.documents.map((d) => (
            <li key={d.id} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2 text-sm">
              <a
                href={d.file?.url}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex min-w-0 items-center gap-2 font-semibold text-primary"
              >
                <FileText aria-hidden="true" className="size-4 shrink-0" />
                <span className="truncate">{t(`kinds.${d.kind}`)}</span>
              </a>
              <span className={d.expired ? "font-semibold text-destructive" : "text-muted-foreground"}>
                {d.expiresOn ? t(d.expired ? "expired" : "expires", { date: formatDate(locale, d.expiresOn, true) }) : t("noExpiry")}
              </span>
              <button
                type="button"
                aria-label={t("remove", { kind: t(`kinds.${d.kind}`) })}
                disabled={remove.isPending}
                onClick={() => remove.mutate(d.id, { onError: (e) => toast.error(errorText(e)) })}
                className="grid size-9 shrink-0 place-items-center rounded-lg text-muted-foreground hover:text-destructive"
              >
                <Trash2 aria-hidden="true" className="size-4" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="mt-4 grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("kind")}
          <select value={kind} onChange={(e) => setKind(e.target.value as PainterDocumentKind)} className={fieldClass}>
            {DOCUMENT_KINDS.map((k) => (
              <option key={k} value={k}>
                {t(`kinds.${k}`)}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("expiresOn")}
          <input type="date" value={expiresOn} onChange={(e) => setExpiresOn(e.target.value)} className={fieldClass} />
        </label>
      </div>
      <Button variant="outline" className="mt-2 w-full" disabled={add.isPending} onClick={() => input.current?.click()}>
        {add.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Upload aria-hidden="true" />}
        {t("upload")}
      </Button>
      <input
        ref={input}
        type="file"
        accept="application/pdf,image/jpeg,image/png,image/webp"
        className="sr-only"
        tabIndex={-1}
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = "";
          if (!file) return;
          add.mutate(
            { file, kind, expiresOn: expiresOn || null },
            { onSuccess: () => toast.success(t("uploaded")), onError: (e) => toast.error(errorText(e)) },
          );
        }}
      />
    </section>
  );
}

function Jobs({ painter }: { painter: PainterDetail }) {
  const t = useTranslations("admin.painters.jobs");
  const locale = useLocale();
  return (
    <section aria-labelledby="jobs-title" className="rounded-[20px] border border-border bg-card p-4">
      <h2 id="jobs-title" className="mb-3 font-bold">
        {t("title")}
      </h2>
      {painter.jobs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("empty")}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {painter.jobs.map((j) => (
            <li key={j.jobId}>
              <Link href={`/admin/trabajos/${j.jobId}`} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2 text-sm hover:border-primary">
                <span>
                  <span className="font-semibold">{j.code}</span>
                  <span className="block text-xs text-muted-foreground">
                    {formatDate(locale, j.scheduledOn, true)} · {formatCents(locale, j.painterPayCents)}
                  </span>
                </span>
                <JobStatusBadge status={j.status} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** What Pinta owes the painter per closed job (A5.4); marking it paid arrives with Finanzas in H7. */
function Payouts({ painterId }: { painterId: string }) {
  const t = useTranslations("admin.painters.payouts");
  const locale = useLocale();
  const payouts = usePainterPayoutsForAdmin(painterId);
  const pending = (payouts.data ?? []).filter((p) => p.status === "pending").reduce((sum, p) => sum + p.amountCents, 0);
  return (
    <section aria-labelledby="payouts-title" className="rounded-[20px] border border-border bg-card p-4">
      <h2 id="payouts-title" className="mb-1 font-bold">
        {t("title")}
      </h2>
      {payouts.isPending ? (
        <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : payouts.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {t("error")}
        </p>
      ) : payouts.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("empty")}</p>
      ) : (
        <>
          <p className="mb-3 text-sm">{t("pending", { amount: formatCents(locale, pending) })}</p>
          <ul className="flex flex-col gap-2">
            {payouts.data.map((p) => (
              <li key={p.id} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2 text-sm">
                <Link href={`/admin/trabajos/${p.jobId}`} className="font-semibold text-primary">
                  {p.code}
                </Link>
                <span className="tabular-nums">{formatCents(locale, p.amountCents)}</span>
                <span className={p.status === "paid" ? "text-xs font-semibold text-primary" : "text-xs font-semibold text-accent-ink"}>
                  {t(`status.${p.status}`)}
                </span>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-xs text-muted-foreground">{t("markLater")}</p>
        </>
      )}
    </section>
  );
}
