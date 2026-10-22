"use client";

import { useQuery } from "@tanstack/react-query";
import { MailCheck } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { LocaleSwitcher } from "@/components/locale-switcher";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { useResendConfirmation } from "@/lib/auth/use-auth";
import { formatArea } from "@/lib/format";
import { setDraft } from "@/lib/quote/use-quote-draft";
import { readLastRequest } from "@/lib/quote/use-submit";
import { PriceRange } from "./price-range";

/** Screen 8 · Confirmación (board Confirmacion) [C4.4, C4.5]. The draft is cleared on arrival. */
export function SentStep({ code }: { code: string }) {
  const t = useTranslations("quote.sent");
  const locale = useLocale();
  // Read once on the client; the first render shows no job data either way, so hydration matches.
  const [request] = useState(() => (typeof window === "undefined" ? null : readLastRequest(code)));

  useEffect(() => setDraft(null), []);

  const job = useQuery({
    queryKey: ["job", request?.id],
    queryFn: async () => {
      const result = await api.GET("/api/jobs/{id}", { params: { path: { id: request!.id } } });
      if (!result.data) throw new ApiError(undefined);
      return result.data;
    },
    enabled: !!request,
  });
  const data = job.data;
  const dateFormat = new Intl.DateTimeFormat(locale === "en" ? "en-GB" : locale === "es" ? "es-ES" : "pt-PT", { day: "numeric", month: "long", timeZone: "UTC" });
  const formatDate = (iso: string) => dateFormat.format(new Date(`${iso}T00:00:00Z`));

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="mx-auto flex w-full max-w-2xl items-center justify-between px-4 pt-3 sm:px-6">
        <Logo className="text-[22px]" />
        <LocaleSwitcher />
      </header>
      <main id="main" className="mx-auto flex w-full max-w-2xl flex-1 flex-col px-4 pt-6 pb-36 sm:px-6 lg:pb-12">
        <div className="flex flex-col items-center text-center">
          <svg width="112" height="112" viewBox="0 0 112 112" aria-hidden="true">
            <circle cx="56" cy="56" r="54" fill="var(--accent-soft)" />
            <circle cx="56" cy="56" r="36" fill="var(--primary)" />
            <path d="M40 57l11 11 22-24" fill="none" stroke="#fff" strokeWidth="6" strokeLinecap="round" strokeLinejoin="round" />
            <circle cx="98" cy="22" r="6" fill="var(--accent)" />
          </svg>
          <h1 className="mt-5 text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
          <p className="mt-2 text-[15px] text-muted-foreground">{t("emailSent")}</p>
          <span className="mt-3.5 rounded-[10px] border border-border bg-card px-3 py-1.5 text-[13px] font-semibold">{t("code", { code })}</span>
        </div>

        {data?.status === "pending_email" && <ConfirmEmailNotice />}

        <section aria-labelledby="next-title" className="mt-7">
          <h2 id="next-title" className="mb-3 text-[13px] font-semibold">
            {t("next.title")}
          </h2>
          <ol className="rounded-[18px] border border-border bg-card p-4">
            {(["review", "price", "book"] as const).map((step, i) => (
              <li key={step} className="flex gap-3">
                <div className="flex flex-col items-center">
                  <span
                    aria-hidden="true"
                    className={
                      i === 0
                        ? "flex size-7 items-center justify-center rounded-full bg-primary text-[13px] font-bold text-primary-foreground"
                        : i === 1
                          ? "flex size-7 items-center justify-center rounded-full bg-primary-soft text-[13px] font-bold text-[#0b5f59]"
                          : "flex size-7 items-center justify-center rounded-full bg-accent-soft text-[13px] font-bold text-accent-ink"
                    }
                  >
                    {i + 1}
                  </span>
                  {i < 2 && <span aria-hidden="true" className="my-1 w-0.5 flex-1 bg-border" />}
                </div>
                <div className={i < 2 ? "pb-4" : undefined}>
                  <p className="text-sm font-semibold">{t(`next.${step}.title`)}</p>
                  <p className="mt-0.5 text-[13px] leading-snug text-muted-foreground">{t(`next.${step}.body`)}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>

        {data && (
          <dl className="mt-4 rounded-[18px] border border-border bg-card px-4 text-[13px]">
            <Row label={t("summary.area")} value={formatArea(locale, Math.round(data.areaM2 * 100))} />
            <Row
              label={t("summary.estimate")}
              value={
                data.overMax || data.rangeMinCents == null || data.rangeMaxCents == null ? (
                  t("summary.custom")
                ) : (
                  <PriceRange minCents={data.rangeMinCents} maxCents={data.rangeMaxCents} />
                )
              }
            />
            <Row label={t("summary.address")} value={`${data.addressLine1}, ${data.locality}`} />
            <Row
              label={t("summary.dates")}
              value={data.preferredDates.asap || !data.preferredDates.from || !data.preferredDates.to ? t("summary.asap") : `${formatDate(data.preferredDates.from)} – ${formatDate(data.preferredDates.to)}`}
              last
            />
          </dl>
        )}

        <div className="fixed inset-x-0 bottom-0 bg-background px-5 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))] lg:static lg:mt-6 lg:p-0">
          <div className="mx-auto max-w-2xl">
            {/* "Ver mi trabajo" (screen 10) arrives with the client area. */}
            <Button asChild size="lg" className="w-full">
              <Link href="/">{t("home")}</Link>
            </Button>
          </div>
        </div>
      </main>
    </div>
  );
}

function Row({ label, value, last }: { label: string; value: React.ReactNode; last?: boolean }) {
  return (
    <div className={last ? "flex justify-between gap-3 py-3" : "flex justify-between gap-3 border-b border-border/70 py-3"}>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-semibold">{value}</dd>
    </div>
  );
}

/** pending_email: the request is reviewed once the email is confirmed; resend once a minute (C4.5). */
function ConfirmEmailNotice() {
  const t = useTranslations("quote.sent.confirm");
  const resend = useResendConfirmation();
  const limited = resend.error instanceof ApiError && resend.error.code === "rate_limited";
  return (
    <div role="status" className="mt-6 flex flex-col gap-3 rounded-2xl border border-accent bg-accent-soft p-4 text-accent-ink">
      <p className="flex items-start gap-2.5 text-sm font-semibold">
        <MailCheck aria-hidden="true" className="mt-0.5 size-[18px] shrink-0" />
        {t("title")}
      </p>
      <p className="text-sm">{t("body")}</p>
      <div className="flex flex-wrap items-center gap-3">
        <Button size="sm" variant="outline" disabled={resend.isPending || resend.isSuccess} onClick={() => resend.mutate()}>
          {t("resend")}
        </Button>
        {resend.isSuccess && <span className="text-sm">{t("resent")}</span>}
        {limited && <span className="text-sm">{t("wait")}</span>}
      </div>
    </div>
  );
}
