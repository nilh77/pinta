"use client";

import { CheckCircle2, Loader2, RotateCcw, UserX } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useErrorText } from "@/lib/api/use-error-text";
import { ApiError } from "@/lib/api/problem";
import { useDeletionRequests, useExecuteDeletion, useResetDemo, type DeletionRequest } from "@/lib/finance/use-finance";
import { formatCents, formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";

/** Ajustes: the account deletion queue (C5.8) and, in the local demo, «Restablecer datos de demo» (H8). */
export function SettingsScreen() {
  const t = useTranslations("admin.settings");
  const requests = useDeletionRequests();

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <header>
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
      </header>
      <section aria-labelledby="deletion-title" className="flex flex-col gap-3">
        <div>
          <h2 id="deletion-title" className="font-bold">
            {t("deletion.title")}
          </h2>
          <p className="text-sm text-muted-foreground">{t("deletion.body")}</p>
        </div>
        {requests.isPending ? (
          <p role="status" className="flex items-center gap-2 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
        ) : requests.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3">
            <p className="text-destructive">{t("error")}</p>
            <Button variant="outline" onClick={() => requests.refetch()}>
              {t("retry")}
            </Button>
          </div>
        ) : requests.data.length === 0 ? (
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <CheckCircle2 aria-hidden="true" className="size-4 text-primary" />
            {t("deletion.empty")}
          </p>
        ) : (
          <ul className="flex flex-col gap-3">
            {requests.data.map((r) => (
              <li key={r.userId}>
                <Request request={r} />
              </li>
            ))}
          </ul>
        )}
      </section>
      <DemoReset />
    </div>
  );
}

/** Development only: the API answers 404 elsewhere and the page says so. */
function DemoReset() {
  const t = useTranslations("admin.settings.demo");
  const errorText = useErrorText();
  const reset = useResetDemo();
  const [confirming, setConfirming] = useState(false);
  return (
    <section aria-labelledby="demo-title" className="flex flex-col items-start gap-3 rounded-[20px] border border-border bg-card p-4">
      <div>
        <h2 id="demo-title" className="font-bold">
          {t("title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("body")}</p>
      </div>
      <Button variant="outline" disabled={reset.isPending} onClick={() => setConfirming(true)}>
        {reset.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <RotateCcw aria-hidden="true" />}
        {reset.isPending ? t("running") : t("button")}
      </Button>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("confirmTitle")}
        description={t("confirmBody")}
        confirmLabel={t("confirm")}
        cancelLabel={t("cancel")}
        onConfirm={() =>
          reset.mutate(undefined, {
            onSuccess: () => toast.success(t("done")),
            onError: (e) => toast.error(e instanceof ApiError && e.code === "not_found" ? t("unavailable") : errorText(e)),
          })
        }
      />
    </section>
  );
}

function Request({ request }: { request: DeletionRequest }) {
  const t = useTranslations("admin.settings.deletion");
  const tStatus = useTranslations("jobs.status");
  const locale = useLocale();
  const errorText = useErrorText();
  const execute = useExecuteDeletion();
  const [confirming, setConfirming] = useState(false);
  const blocked = request.jobs.some((j) => j.action === "blocked");
  const toCancel = request.jobs.filter((j) => j.action === "cancel");

  return (
    <article className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-bold">{request.name}</p>
          <p className="text-sm text-muted-foreground">
            {request.email} · {t("requested", { date: formatDateTime(locale, request.requestedAt) })}
          </p>
        </div>
        <Button variant="outline" disabled={blocked || execute.isPending} onClick={() => setConfirming(true)}>
          <UserX aria-hidden="true" />
          {t("execute")}
        </Button>
      </div>
      {request.jobs.length > 0 && (
        <ul className="flex flex-col gap-1 text-sm">
          {request.jobs.map((j) => (
            <li key={j.jobId} className="flex flex-wrap justify-between gap-2">
              <span>
                {j.code} · {tStatus(j.status)}
              </span>
              <span className={cn("font-semibold", j.action === "blocked" ? "text-destructive" : j.action === "cancel" ? "text-accent-ink" : "text-muted-foreground")}>
                {t(`actions.${j.action}`, { refund: formatCents(locale, j.refundCents) })}
              </span>
            </li>
          ))}
        </ul>
      )}
      {blocked && <p className="text-sm text-destructive">{t("blocked")}</p>}
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("confirmTitle", { name: request.name })}
        description={
          toCancel.length > 0
            ? t("confirmCancels", { jobs: toCancel.map((j) => `${j.code} (${formatCents(locale, j.refundCents)})`).join(", ") })
            : t("confirmBody")
        }
        confirmLabel={t("execute")}
        cancelLabel={t("cancel")}
        onConfirm={() => execute.mutate(request.userId, { onSuccess: () => toast.success(t("done")), onError: (e) => toast.error(errorText(e)) })}
      />
    </article>
  );
}
