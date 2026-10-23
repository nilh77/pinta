"use client";

import { Elements, PaymentElement, useElements, useStripe } from "@stripe/react-stripe-js";
import { loadStripe, type Stripe } from "@stripe/stripe-js";
import { ArrowLeft, CircleAlert, CircleCheck, Copy, Download, Loader2, Smartphone } from "lucide-react";
import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatCents, formatDateTime } from "@/lib/format";
import { useMyJob } from "@/lib/jobs/use-client-jobs";
import { newUuid } from "@/lib/quote/draft";
import {
  formatCountdown,
  isPaymentType,
  MB_WAY_WINDOW_SECONDS,
  receiptUrl,
  useCreatePayment,
  useJobPayments,
  type CreatedPayment,
  type Payment,
  type PaymentType,
} from "@/lib/payments/use-payments";

const stripes = new Map<string, Promise<Stripe | null>>();
/** One Stripe.js instance per publishable key, loaded only on this screen. */
const stripeFor = (key: string) => {
  if (!stripes.has(key)) stripes.set(key, loadStripe(key));
  return stripes.get(key)!;
};

/** Screen 11 · Pago (board Pago) [C5.3, C5.5]: deposit, balance or site visit with Stripe's Payment Element. */
export function PaymentScreen({ id }: { id: string }) {
  const t = useTranslations("payment");
  const params = useSearchParams();
  const type = params.get("tipo");
  const returned = params.has("payment_intent");
  const job = useMyJob(id);
  const [idempotencyKey] = useState(() => newUuid());
  const create = useCreatePayment(id, idempotencyKey);
  const started = useRef(false);
  const errorText = useErrorText();
  const router = useRouter();

  // A Multibanco reference already issued is shown again instead of a new form.
  const existing = useJobPayments(id);
  const waiting = existing.data?.find((p) => p.type === type && p.status === "requires_action" && p.multibanco);

  useEffect(() => {
    // Strict Mode runs effects twice in development; one intent per visit.
    if (!isPaymentType(type) || returned || started.current || !existing.isSuccess || waiting) return;
    started.current = true;
    create.mutate(type);
  }, [create, existing.isSuccess, returned, type, waiting]);

  if (!isPaymentType(type)) {
    return <Problem title={t("invalid")} id={id} />;
  }

  return (
    <div className="flex flex-col gap-5">
      <Link href={`/mis-trabajos/${id}`} className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("back")}
      </Link>
      <header>
        <p className="text-[13px] font-semibold text-muted-foreground">{job.data?.code}</p>
        <h1 className="text-[26px] leading-tight font-extrabold tracking-tight">{t(`title.${type}`)}</h1>
        {job.data && <p className="mt-1 text-sm text-muted-foreground">{job.data.addressLine1}, {job.data.locality}</p>}
      </header>

      {returned || waiting ? (
        <PaymentProgress
          jobId={id}
          type={type}
          paymentId={waiting?.id ?? null}
          onRetry={() => {
            started.current = false;
            router.replace(`/mis-trabajos/${id}/pagar?tipo=${type}`);
          }}
        />
      ) : create.isPending || create.isIdle ? (
        <p role="status" className="flex items-center gap-2 py-8 text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
          {t("preparing")}
        </p>
      ) : create.isError ? (
        <Problem title={errorText(create.error)} id={id} />
      ) : (
        <Checkout jobId={id} payment={create.data} />
      )}
    </div>
  );
}

function Problem({ title, id }: { title: string; id: string }) {
  const t = useTranslations("payment");
  return (
    <div role="alert" className="flex flex-col items-start gap-4 rounded-[20px] border border-border bg-card p-5">
      <p className="flex items-start gap-2 font-semibold text-destructive">
        <CircleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
        {title}
      </p>
      <Button asChild variant="outline">
        <Link href={`/mis-trabajos/${id}`}>{t("back")}</Link>
      </Button>
    </div>
  );
}

function Checkout({ jobId, payment }: { jobId: string; payment: CreatedPayment }) {
  const t = useTranslations("payment");
  const locale = useLocale();
  const stripe = useMemo(() => stripeFor(payment.publishableKey), [payment.publishableKey]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between rounded-[20px] border border-border bg-card p-4">
        <span className="text-sm text-muted-foreground">{t("amount")}</span>
        <span className="text-2xl font-extrabold">{formatCents(locale, payment.amountCents)}</span>
      </div>
      {payment.mbWayLimitWarning && (
        <p className="rounded-xl bg-accent-soft px-3.5 py-3 text-sm text-accent-ink">{t("mbWayLimit")}</p>
      )}
      <Elements
        stripe={stripe}
        options={{
          clientSecret: payment.clientSecret,
          locale: locale as "pt" | "es" | "en",
          appearance: { theme: "stripe", variables: { colorPrimary: "#0f766e", borderRadius: "12px", fontFamily: "Inter, system-ui, sans-serif" } },
        }}
      >
        <PayForm jobId={jobId} payment={payment} />
      </Elements>
    </div>
  );
}

function PayForm({ jobId, payment }: { jobId: string; payment: CreatedPayment }) {
  const t = useTranslations("payment");
  const locale = useLocale();
  const stripe = useStripe();
  const elements = useElements();
  const [method, setMethod] = useState<string | null>(null);
  const [phase, setPhase] = useState<"form" | "submitting" | "waiting">("form");
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!stripe || !elements) return;
    setError(null);
    setPhase("submitting");
    const returnUrl = `${window.location.origin}/${locale}/mis-trabajos/${jobId}/pagar?tipo=${payment.type}`;
    const result = await stripe.confirmPayment({ elements, confirmParams: { return_url: returnUrl }, redirect: "if_required" });
    if (result.error) {
      // Stripe's message is already in the page language (Elements locale).
      setError(result.error.message ?? t("failed"));
      setPhase("form");
      return;
    }

    setPhase("waiting");
  };

  // The same PaymentIntent takes another attempt after a failure.
  if (phase === "waiting") return <PaymentProgress jobId={jobId} type={payment.type} paymentId={payment.paymentId} onRetry={() => setPhase("form")} />;

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" aria-busy={phase === "submitting"}>
      <div className="rounded-[20px] border border-border bg-card p-4">
        <PaymentElement options={{ layout: "tabs" }} onChange={(e) => setMethod(e.value.type)} />
      </div>
      {error && (
        <p role="alert" className="flex items-start gap-2 rounded-xl bg-destructive/10 px-3.5 py-3 text-sm font-medium text-destructive">
          <CircleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {error}
        </p>
      )}
      {phase === "submitting" && method === "mb_way" && <MbWayCountdown />}
      <Button type="submit" size="lg" disabled={!stripe || !elements || phase === "submitting"} className="w-full">
        {phase === "submitting" && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
        {t("pay", { amount: formatCents(locale, payment.amountCents) })}
      </Button>
      <p className="text-center text-xs text-muted-foreground">{t("secure")}</p>
    </form>
  );
}

/** MB WAY: "Acepta el pago en la app MB WAY" with Stripe's 5-minute window. */
function MbWayCountdown() {
  const t = useTranslations("payment");
  const [left, setLeft] = useState(MB_WAY_WINDOW_SECONDS);
  useEffect(() => {
    const timer = setInterval(() => setLeft((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(timer);
  }, []);
  return (
    <div role="status" className="flex items-center gap-3 rounded-xl bg-primary-soft px-3.5 py-3 text-sm text-[#0b5f59]">
      <Smartphone aria-hidden="true" className="size-6 shrink-0" />
      <span>
        <strong className="block">{t("mbWay.title")}</strong>
        {t("mbWay.body", { time: formatCountdown(left) })}
      </span>
    </div>
  );
}

/**
 * After confirming: paid only when the webhook arrives (S4). Polls the payments; Multibanco shows the reference to pay
 * later; a failure shows Stripe's message and lets the client try again.
 */
function PaymentProgress({ jobId, type, paymentId, onRetry }: { jobId: string; type: PaymentType; paymentId: string | null; onRetry: () => void }) {
  const t = useTranslations("payment");
  const locale = useLocale();
  const pick = (payments: Payment[]) =>
    payments.filter((p) => (paymentId ? p.id === paymentId : p.type === type)).sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];
  const payments = useJobPayments(jobId, (all) => !["succeeded", "failed", "cancelled", "refunded", "partially_refunded"].includes(pick(all)?.status ?? "pending"));
  const payment = payments.data ? pick(payments.data) : undefined;

  if (!payment || payment.status === "pending") {
    return (
      <p role="status" className="flex items-center gap-2 rounded-[20px] border border-border bg-card p-5 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("confirming")}
      </p>
    );
  }

  if (payment.status === "requires_action" && payment.multibanco) {
    const rows = [
      [t("multibanco.entity"), payment.multibanco.entity],
      [t("multibanco.reference"), payment.multibanco.reference.replace(/(\d{3})(?=\d)/g, "$1 ")],
      [t("amount"), formatCents(locale, payment.amountCents)],
    ] as const;
    return (
      <section aria-labelledby="mb-title" className="flex flex-col gap-3 rounded-[20px] border-2 border-accent bg-card p-5">
        <h2 id="mb-title" className="text-lg font-bold">
          {t("multibanco.title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("multibanco.body")}</p>
        <dl className="flex flex-col gap-2">
          {rows.map(([label, value]) => (
            <div key={label} className="flex items-center justify-between gap-3 rounded-xl bg-background px-3 py-2">
              <dt className="text-sm text-muted-foreground">{label}</dt>
              <dd className="flex items-center gap-2 font-mono text-base font-bold">
                {value}
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t("multibanco.copy", { what: label })}
                  onClick={() => navigator.clipboard.writeText(value.replace(/\s/g, "")).then(() => toast.success(t("multibanco.copied")))}
                >
                  <Copy aria-hidden="true" />
                </Button>
              </dd>
            </div>
          ))}
        </dl>
        {payment.multibanco.expiresAt && <p className="text-sm">{t("multibanco.deadline", { date: formatDateTime(locale, payment.multibanco.expiresAt) })}</p>}
        <p className="text-xs text-muted-foreground">{t("multibanco.waiting")}</p>
      </section>
    );
  }

  if (payment.status === "failed" || payment.status === "cancelled") {
    return (
      <div role="alert" className="flex flex-col items-start gap-3 rounded-[20px] border border-border bg-card p-5">
        <p className="flex items-start gap-2 font-semibold text-destructive">
          <CircleAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
          {payment.error ?? t("failed")}
        </p>
        <Button onClick={onRetry}>{t("retry")}</Button>
      </div>
    );
  }

  return (
    <div role="status" className="flex flex-col items-start gap-3 rounded-[20px] border border-border bg-card p-5">
      <p className="flex items-center gap-2 text-lg font-bold text-primary">
        <CircleCheck aria-hidden="true" className="size-6" />
        {t("paid")}
      </p>
      <p className="text-sm text-muted-foreground">{t(`paidBody.${type}`)}</p>
      <div className="flex flex-wrap gap-2">
        <Button asChild>
          <Link href={`/mis-trabajos/${jobId}`}>{t("seeJob")}</Link>
        </Button>
        {payment.receiptNumber && (
          <Button asChild variant="outline">
            <a href={receiptUrl(jobId, payment.id)} download>
              <Download aria-hidden="true" />
              {t("receipt")}
            </a>
          </Button>
        )}
      </div>
    </div>
  );
}
