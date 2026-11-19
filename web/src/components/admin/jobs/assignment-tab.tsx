"use client";

import { Hourglass, Loader2, Send, Undo2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { toast } from "@/lib/toast";
import { DateInput } from "@/components/ui/date-input";
import { Button } from "@/components/ui/button";
import { JobProtocol } from "@/components/jobs/job-protocol";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import type { AdminJobDetail } from "@/lib/jobs/api";
import { formatCents, formatDate, formatDateTime } from "@/lib/format";
import { useAssignablePainters, useOfferJob, useWithdrawOffer } from "@/lib/painter/use-admin-painters";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { cn } from "@/lib/utils";

const fieldClass =
  "h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive";

const TONES = {
  offered: "bg-accent-soft text-accent-ink",
  accepted: "bg-primary-soft text-primary",
  completed: "bg-primary-soft text-primary",
  declined: "bg-destructive/10 text-destructive",
  withdrawn: "bg-muted text-muted-foreground",
} as const;

/** Asignación (A3.2): offers to painters covering the concelho, with date, and their answers. */
export function AssignmentTab({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.assignment");
  const locale = useLocale();
  // One open offer at a time (A3.2): while it waits, the form gives way to «Retirar oferta».
  const openOffer = job.assignments.find((a) => a.status === "offered");

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
      <section aria-labelledby="offers-title">
        <h3 id="offers-title" className="mb-3 text-sm font-bold">
          {t("offers")}
        </h3>
        {job.assignments.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("none")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {job.assignments.map((a) => (
              <li key={a.id} className="rounded-xl border border-border bg-card px-3 py-2 text-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-semibold">{a.painterName}</p>
                  <span className={cn("rounded-full px-2.5 py-1 text-xs font-semibold", TONES[a.status])}>{t(`status.${a.status}`)}</span>
                </div>
                <p className="text-xs text-muted-foreground">
                  {t("line", {
                    date: formatDate(locale, a.scheduledOn, true),
                    pay: formatCents(locale, a.painterPayCents),
                    offered: formatDateTime(locale, a.offeredAt),
                  })}
                  {a.respondedAt && ` · ${t("answered", { date: formatDateTime(locale, a.respondedAt) })}`}
                </p>
                {a.declineReason && <p className="mt-1">{t("reason", { reason: a.declineReason })}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>
      {job.status !== "deposit_paid" ? (
        <p className="text-sm text-muted-foreground">{t("onlyPaid")}</p>
      ) : openOffer ? (
        <OpenOffer job={job} offer={openOffer} />
      ) : (
        <OfferForm job={job} />
      )}
      {job.assignments.some((offer) => offer.status === "accepted" || offer.status === "completed") && (
        <div className="lg:col-span-2">
          <JobProtocol id={job.id} audience="admin" finished={job.status === "finished" || job.status === "closed"} />
        </div>
      )}
    </div>
  );
}

function OfferForm({ job }: { job: AdminJobDetail }) {
  const t = useTranslations("admin.job.assignment");
  const errorText = useErrorText();
  const painters = useAssignablePainters(job.id, true);
  const offer = useOfferJob(job.id);
  const today = new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Lisbon" });
  const [painterId, setPainterId] = useState("");
  const [scheduledOn, setScheduledOn] = useState(job.preferredDates.from && job.preferredDates.from >= today ? job.preferredDates.from : today);
  const server = offer.error instanceof ApiError ? offer.error.problem?.errors : undefined;

  return (
    <form
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (!painterId || !scheduledOn) return;
        offer.mutate(
          { painterId, scheduledOn, version: job.version },
          {
            onSuccess: (a) => toast.success(t("sent", { name: a.painterName })),
            onError: (e) => !(e instanceof ApiError && e.problem?.errors) && toast.error(errorText(e)),
          },
        );
      }}
      className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4"
    >
      <h3 className="text-sm font-bold">{t("new")}</h3>
      {painters.isPending ? (
        <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : (painters.data ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("noPainters", { municipality: job.address.municipality })}</p>
      ) : (
        <>
          <label className="flex flex-col gap-1 text-[13px] font-semibold">
            {t("painter")}
            <select value={painterId} onChange={(e) => setPainterId(e.target.value)} className={fieldClass} aria-invalid={!!server?.painterId}>
              <option value="">{t("choose")}</option>
              {painters.data!.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.rating != null ? ` · ★ ${p.rating}` : ""}
                </option>
              ))}
            </select>
            {server?.painterId && <span className="text-sm font-normal text-destructive">{server.painterId[0]}</span>}
          </label>
          <label className="flex flex-col gap-1 text-[13px] font-semibold">
            {t("date")}
            <DateInput min={today} value={scheduledOn} onChange={setScheduledOn} className={fieldClass} invalid={!!server?.scheduledOn} />
            {server?.scheduledOn && <span className="text-sm font-normal text-destructive">{server.scheduledOn[0]}</span>}
          </label>
          <Button type="submit" disabled={!painterId || !scheduledOn || offer.isPending}>
            {offer.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Send aria-hidden="true" />}
            {t("submit")}
          </Button>
        </>
      )}
    </form>
  );
}

/** The offer waiting for the painter (A3.2): who, which date, since when, and «Retirar oferta». */
function OpenOffer({ job, offer }: { job: AdminJobDetail; offer: AdminJobDetail["assignments"][number] }) {
  const t = useTranslations("admin.job.assignment");
  const locale = useLocale();
  const errorText = useErrorText();
  const withdraw = useWithdrawOffer(job.id);
  const [confirming, setConfirming] = useState(false);

  return (
    <section aria-labelledby="open-offer-title" className="flex flex-col gap-3 rounded-[20px] border-2 border-accent bg-card p-4">
      <h3 id="open-offer-title" className="flex items-center gap-2 text-sm font-bold">
        <Hourglass aria-hidden="true" className="size-4 text-accent-ink" />
        {t("waiting")}
      </h3>
      <p className="text-sm">
        {t("waitingBody", {
          name: offer.painterName,
          date: formatDate(locale, offer.scheduledOn, true),
          offered: formatDateTime(locale, offer.offeredAt),
        })}
      </p>
      <p className="text-xs text-muted-foreground">{t("oneAtATime")}</p>
      <Button variant="outline" className="self-start text-destructive" pending={withdraw.isPending} onClick={() => setConfirming(true)}>
        <Undo2 aria-hidden="true" />
        {t("withdraw")}
      </Button>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("withdrawTitle", { name: offer.painterName })}
        description={t("withdrawBody")}
        confirmLabel={t("withdraw")}
        cancelLabel={t("keep")}
        onConfirm={() =>
          withdraw.mutate(offer.id, {
            onSuccess: () => toast.success(t("withdrawn", { name: offer.painterName })),
            onError: (e) => toast.error(errorText(e)),
          })
        }
      />
    </section>
  );
}
