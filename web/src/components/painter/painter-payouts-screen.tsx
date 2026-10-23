"use client";

import { Loader2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { FormDialog } from "@/components/ui/form-dialog";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatCents, formatDate } from "@/lib/format";
import { usePainterPayouts, usePainterProfile, useUpdatePainterProfile, type PainterProfile } from "@/lib/painter/use-painter";
import { cn } from "@/lib/utils";

/** The current month in Lisbon as YYYY-MM. */
const thisMonth = () => new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Lisbon" }).slice(0, 7);

/** P4 · Mis pagos (board PintorPagos) [P4.1]: what Pinta still owes and what it paid in the month; the IBAN they go to. */
export function PainterPayoutsScreen() {
  const t = useTranslations("painter.payouts");
  const locale = useLocale();
  const [month, setMonth] = useState(thisMonth);
  const payouts = usePainterPayouts(month);
  const profile = usePainterProfile();
  const monthName = new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(`${month}-01T00:00:00Z`));

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
        <label className="flex flex-col gap-1 text-[13px] font-semibold">
          {t("month")}
          <input
            type="month"
            value={month}
            max={thisMonth()}
            onChange={(e) => e.target.value && setMonth(e.target.value)}
            className="h-11 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
          />
        </label>
      </header>

      {payouts.isPending ? (
        <p role="status" className="flex items-center gap-2 py-10 text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : payouts.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3 py-10">
          <p className="text-destructive">{t("error")}</p>
          <Button variant="outline" onClick={() => payouts.refetch()}>
            {t("retry")}
          </Button>
        </div>
      ) : (
        <>
          <dl className="grid grid-cols-2 gap-3">
            <div className="rounded-[20px] border border-accent bg-card p-4">
              <dt className="text-xs text-muted-foreground">{t("pending")}</dt>
              <dd className="text-2xl font-extrabold">{formatCents(locale, payouts.data.pendingCents)}</dd>
            </div>
            <div className="rounded-[20px] border border-border bg-card p-4">
              <dt className="text-xs text-muted-foreground">{t("paidIn", { month: monthName })}</dt>
              <dd className="text-2xl font-extrabold text-primary">{formatCents(locale, payouts.data.paidCents)}</dd>
            </div>
          </dl>

          {payouts.data.items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 rounded-[20px] border border-dashed border-border bg-card px-6 py-12 text-center">
              <p className="text-lg font-bold">{t("empty")}</p>
              <p className="text-sm text-muted-foreground">{t("emptyBody")}</p>
            </div>
          ) : (
            <ul className="flex flex-col gap-2">
              {payouts.data.items.map((p) => (
                <li key={p.id} className="flex items-center justify-between gap-3 rounded-2xl border border-border bg-card px-4 py-3">
                  <div>
                    <p className="font-semibold">{p.code}</p>
                    <p className="text-xs text-muted-foreground">
                      {p.status === "paid" && p.paidAt
                        ? t("paidOn", { date: formatDate(locale, p.paidAt, true), reference: p.reference ?? "—" })
                        : t("since", { date: formatDate(locale, p.createdAt, true) })}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="font-bold">{formatCents(locale, p.amountCents)}</p>
                    <span
                      className={cn(
                        "inline-flex rounded-full px-2 py-0.5 text-xs font-semibold",
                        p.status === "paid" ? "bg-primary-soft text-primary" : "bg-accent-soft text-accent-ink",
                      )}
                    >
                      {t(`status.${p.status}`)}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      {profile.data && <IbanCard profile={profile.data} />}
    </div>
  );
}

/** Masked IBAN; changing it asks for the password (A4.2). */
function IbanCard({ profile }: { profile: PainterProfile }) {
  const t = useTranslations("painter.payouts.iban");
  const errorText = useErrorText();
  const save = useUpdatePainterProfile();
  const [open, setOpen] = useState(false);
  const [iban, setIban] = useState("");
  const [password, setPassword] = useState("");
  const errors = save.error instanceof ApiError ? save.error.problem?.errors : undefined;

  return (
    <section className="flex flex-wrap items-center justify-between gap-3 rounded-[20px] border border-border bg-card p-4">
      <div>
        <h2 className="text-[13px] font-semibold text-muted-foreground uppercase">{t("title")}</h2>
        <p className="font-semibold tabular-nums">{profile.ibanMasked ?? t("none")}</p>
      </div>
      <Button variant="outline" onClick={() => setOpen(true)}>
        {t(profile.ibanMasked ? "change" : "add")}
      </Button>
      <FormDialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) save.reset();
        }}
        title={t(profile.ibanMasked ? "change" : "add")}
        description={t("body")}
        submitLabel={t("save")}
        cancelLabel={t("cancel")}
        pending={save.isPending}
        submitDisabled={!iban.trim() || (!!profile.ibanMasked && !password)}
        onSubmit={() =>
          save.mutate(
            {
              phone: profile.phone,
              taxId: profile.taxId,
              municipalities: [...profile.municipalities],
              iban: iban.replace(/\s/g, "").toUpperCase(),
              currentPassword: profile.ibanMasked ? password : null,
            },
            {
              onSuccess: () => {
                setOpen(false);
                setIban("");
                setPassword("");
                toast.success(t("saved"));
              },
              onError: (e) => !(e instanceof ApiError && e.problem?.errors) && toast.error(errorText(e)),
            },
          )
        }
      >
        <div className="flex flex-col gap-3">
          <FormField
            id="new-iban"
            label={t("iban")}
            autoComplete="off"
            spellCheck={false}
            value={iban}
            onChange={(e) => setIban(e.target.value)}
            error={errors?.iban ? { type: "server", message: errors.iban[0] } : undefined}
          />
          {profile.ibanMasked && (
            <FormField
              id="current-password"
              type="password"
              autoComplete="current-password"
              label={t("password")}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              error={errors?.currentPassword ? { type: "server", message: errors.currentPassword[0] } : undefined}
            />
          )}
        </div>
      </FormDialog>
    </section>
  );
}
