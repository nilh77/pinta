"use client";

import { Ban, CalendarClock, Check, CircleCheck, Loader2, Shuffle, X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { FormDialog, TextAreaField } from "@/components/ui/form-dialog";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import type { AdminJobDetail } from "@/lib/jobs/api";
import { parseEurosToCents } from "@/lib/jobs/draft";
import type { JobStatus } from "@/lib/jobs/status";
import {
  useAdminCancel,
  useAdminCancellationPreview,
  useCompleteSiteVisit,
  useConfirmQuote,
  usePartners,
  useRejectJob,
  useScheduleSiteVisit,
  useTransitionJob,
} from "@/lib/jobs/use-admin-jobs";
import { formatCents } from "@/lib/format";

const inputClass =
  "h-11 w-full min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive";

/** Transitions with their own action; the rest go through "Cambiar estado" (A3.3). */
const OWN_ACTION: JobStatus[] = ["quoted", "site_visit", "rejected", "cancelled"];

/** What the admin can do with the job now: only actions the state machine allows are offered. */
export function JobActions({ job }: { job: AdminJobDetail }) {
  const allowed = job.allowedTransitions;
  const others = allowed.filter((s) => !OWN_ACTION.includes(s) && !(job.status === "site_visit" && s === "in_review"));
  if (allowed.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-2">
      {allowed.includes("quoted") && <ConfirmButton job={job} />}
      {job.status === "site_visit" && allowed.includes("in_review") && <CompleteVisitButton job={job} />}
      {allowed.includes("site_visit") && <SiteVisitButton job={job} />}
      {allowed.includes("rejected") && <RejectButton job={job} />}
      {others.length > 0 && <TransitionButton job={job} targets={others} />}
      {allowed.includes("cancelled") && <CancelButton job={job} />}
    </div>
  );
}

function useDone() {
  const errorText = useErrorText();
  return {
    onError: (error: unknown) => {
      if (!(error instanceof ApiError && error.code === "validation_failed")) toast.error(errorText(error));
    },
    fieldErrors: (error: unknown) => (error instanceof ApiError ? (error.problem?.errors ?? {}) : {}),
  };
}

function ConfirmButton({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.actions.confirm");
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState("");
  const [acknowledge, setAcknowledge] = useState(false);
  const confirm = useConfirmQuote(job.id);
  const { onError, fieldErrors } = useDone();
  const lowMargin = (job.draft ?? job.currentQuote).breakdown.belowMinMargin || "acknowledgeLowMargin" in fieldErrors(confirm.error);

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Check aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title", { code: job.code })}
        description={t("body")}
        submitLabel={t("submit")}
        cancelLabel={t("cancel")}
        pending={confirm.isPending}
        submitDisabled={lowMargin && !acknowledge}
        onSubmit={() =>
          confirm.mutate(
            { note: note.trim() || null, acknowledgeLowMargin: acknowledge, version: job.version },
            {
              onSuccess: () => {
                setOpen(false);
                toast.success(t("done"));
              },
              onError,
            },
          )
        }
      >
        <TextAreaField id={fieldId} label={t("note")} hint={t("noteHint")} value={note} onChange={setNote} max={1000} />
        {lowMargin && (
          <label className="flex items-start gap-3 rounded-xl bg-destructive/10 p-3 text-sm font-medium text-destructive">
            <input type="checkbox" className="mt-0.5 size-5 accent-[var(--destructive)]" checked={acknowledge} onChange={(e) => setAcknowledge(e.target.checked)} />
            {t("acknowledge")}
          </label>
        )}
      </FormDialog>
    </>
  );
}

function RejectButton({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.actions.reject");
  const fieldId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [touched, setTouched] = useState(false);
  const reject = useRejectJob(job.id);
  const { onError } = useDone();

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <X aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title", { code: job.code })}
        description={t("body")}
        submitLabel={t("submit")}
        cancelLabel={t("cancel")}
        destructive
        pending={reject.isPending}
        onSubmit={() => {
          setTouched(true);
          if (!reason.trim()) return;
          reject.mutate({ reason: reason.trim(), version: job.version }, { onSuccess: () => (setOpen(false), toast.success(t("done"))), onError });
        }}
      >
        <TextAreaField id={fieldId} label={t("reason")} value={reason} onChange={setReason} max={500} required error={touched && !reason.trim() ? t("required") : undefined} />
      </FormDialog>
    </>
  );
}

/** A2.6: a partner, a date and time and the visit price (0 € by default, from the settings). */
function SiteVisitButton({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.actions.visit");
  const id = useId();
  const [open, setOpen] = useState(false);
  const [assigneeId, setAssigneeId] = useState("");
  const [at, setAt] = useState("");
  const [price, setPrice] = useState("0");
  const partners = usePartners();
  const visit = useScheduleSiteVisit(job.id);
  const { onError, fieldErrors } = useDone();
  const errors = fieldErrors(visit.error);
  const priceCents = parseEurosToCents(price);
  const ready = assigneeId && at && priceCents !== null;

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <CalendarClock aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title", { code: job.code })}
        description={t("body")}
        submitLabel={t("submit")}
        cancelLabel={t("cancel")}
        pending={visit.isPending}
        submitDisabled={!ready}
        onSubmit={() =>
          visit.mutate(
            { assigneeId, at: new Date(at).toISOString(), priceCents, version: job.version },
            { onSuccess: () => (setOpen(false), toast.success(t("done"))), onError },
          )
        }
      >
        <label htmlFor={`${id}-who`} className="text-[13px] font-semibold">
          {t("who")}
        </label>
        <select id={`${id}-who`} className={inputClass} value={assigneeId} aria-invalid={errors.assigneeId ? true : undefined} onChange={(e) => setAssigneeId(e.target.value)}>
          <option value="">{t("choose")}</option>
          {(partners.data ?? []).map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        <label htmlFor={`${id}-at`} className="text-[13px] font-semibold">
          {t("when")}
        </label>
        <input id={`${id}-at`} type="datetime-local" className={inputClass} value={at} aria-invalid={errors.at ? true : undefined} onChange={(e) => setAt(e.target.value)} />
        {errors.at && <p className="text-xs font-medium text-destructive">{errors.at.join(" ")}</p>}
        <label htmlFor={`${id}-price`} className="text-[13px] font-semibold">
          {t("price")}
        </label>
        <input id={`${id}-price`} inputMode="decimal" className={inputClass} value={price} aria-invalid={priceCents === null ? true : undefined} onChange={(e) => setPrice(e.target.value)} />
      </FormDialog>
    </>
  );
}

function CompleteVisitButton({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.actions.completeVisit");
  const complete = useCompleteSiteVisit(job.id);
  const { onError } = useDone();
  return (
    <Button disabled={complete.isPending} onClick={() => complete.mutate({ version: job.version }, { onSuccess: () => toast.success(t("done")), onError })}>
      {complete.isPending ? <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <CircleCheck aria-hidden="true" />}
      {t("open")}
    </Button>
  );
}

function TransitionButton({ job, targets }: { job: AdminJobDetail; targets: JobStatus[] }) {
  const t = useTranslations("admin.job.actions.transition");
  const tStatus = useTranslations("jobs.status");
  const id = useId();
  const [open, setOpen] = useState(false);
  const [to, setTo] = useState<JobStatus>(targets[0]!);
  const [reason, setReason] = useState("");
  const [scheduledOn, setScheduledOn] = useState("");
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Lisbon" }).format(new Date());
  const needsDate = to === "scheduled";
  const transition = useTransitionJob(job.id);
  const { onError } = useDone();

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <Shuffle aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title", { code: job.code })}
        description={t("body")}
        submitLabel={t("submit")}
        cancelLabel={t("cancel")}
        pending={transition.isPending}
        submitDisabled={!reason.trim() || (needsDate && !scheduledOn)}
        onSubmit={() =>
          transition.mutate(
            { to, reason: reason.trim(), version: job.version, scheduledOn: needsDate ? scheduledOn : null },
            { onSuccess: () => (setOpen(false), toast.success(t("done"))), onError },
          )
        }
      >
        <label htmlFor={`${id}-to`} className="text-[13px] font-semibold">
          {t("to")}
        </label>
        <select id={`${id}-to`} className={inputClass} value={to} onChange={(e) => setTo(e.target.value as JobStatus)}>
          {targets.map((status) => (
            <option key={status} value={status}>
              {tStatus(status)}
            </option>
          ))}
        </select>
        {needsDate && (
          <>
            <label htmlFor={`${id}-date`} className="text-[13px] font-semibold">
              {t("scheduledOn")}
            </label>
            <input id={`${id}-date`} type="date" min={today} className={inputClass} value={scheduledOn} onChange={(e) => setScheduledOn(e.target.value)} />
          </>
        )}
        <TextAreaField id={`${id}-reason`} label={t("reason")} value={reason} onChange={setReason} max={500} required />
      </FormDialog>
    </>
  );
}

/** A3.4: the refund under the policy first; full refund as an exception, always with a reason. */
function CancelButton({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.actions.cancelJob");
  const locale = useLocale();
  const id = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [fullRefund, setFullRefund] = useState(false);
  const preview = useAdminCancellationPreview(job.id, open, fullRefund);
  const cancel = useAdminCancel(job.id);
  const { onError } = useDone();

  return (
    <>
      <Button variant="ghost" className="text-destructive" onClick={() => setOpen(true)}>
        <Ban aria-hidden="true" />
        {t("open")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("title", { code: job.code })}
        description={
          preview.data
            ? preview.data.depositPaidCents > 0
              ? t("refund", { amount: formatCents(locale, preview.data.refundCents), paid: formatCents(locale, preview.data.depositPaidCents) })
              : t("noPayment")
            : t("calculating")
        }
        submitLabel={t("submit")}
        cancelLabel={t("keep")}
        destructive
        pending={cancel.isPending}
        submitDisabled={!reason.trim()}
        onSubmit={() =>
          cancel.mutate({ reason: reason.trim(), fullRefund, version: job.version }, { onSuccess: () => (setOpen(false), toast.success(t("done"))), onError })
        }
      >
        <TextAreaField id={`${id}-reason`} label={t("reason")} value={reason} onChange={setReason} max={500} required />
        <label className="flex min-h-11 items-center gap-3 text-sm font-medium">
          <input type="checkbox" className="size-5 accent-[var(--primary)]" checked={fullRefund} onChange={(e) => setFullRefund(e.target.checked)} />
          {t("fullRefund")}
        </label>
      </FormDialog>
    </>
  );
}
