"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { PASSWORD_MIN_LENGTH, registerSchema, signInSchema, type RegisterInput, type RegisterValues, type SignInValues } from "@/lib/auth/schemas";
import { applyServerFieldErrors } from "@/lib/auth/server-errors";
import { useMe, useRegister, useSignIn } from "@/lib/auth/use-auth";
import { toEstimateRequest, type DraftDetails } from "@/lib/quote/draft";
import { toSubmitRequest, validateBilling } from "@/lib/quote/request";
import { rememberLastRequest, useSubmitJob } from "@/lib/quote/use-submit";
import { cn } from "@/lib/utils";
import { QuoteShell, type QuoteContext } from "./quote-shell";

/** Screen 7 · Cuenta (board Cuenta) [C4.3, C4.4]: create an account or sign in, then send the request. */
export function AccountStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={4} back={{ href: "/cotizar/direccion", label: t("account.back") }}>
      {(context) => <AccountForm {...context} />}
    </QuoteShell>
  );
}

const REGISTER_FIELDS = ["name", "email", "phone", "password", "acceptTerms"] as const;

function AccountForm({ draft, info, update }: QuoteContext) {
  const t = useTranslations("quote.account");
  const tRoot = useTranslations();
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const me = useMe();
  const register = useRegister();
  const signIn = useSignIn();
  const submit = useSubmitJob();
  const providers = useQuery({
    queryKey: ["auth-providers"],
    queryFn: async () => (await api.GET("/api/auth/providers")).data ?? { google: false },
    staleTime: Infinity,
  });
  const [tab, setTab] = useState<"register" | "login">("register");
  const [showBillingErrors, setShowBillingErrors] = useState(false);
  const registerForm = useForm<RegisterInput, unknown, RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: "", email: "", phone: "+351 ", password: "", acceptTerms: false, marketingConsent: false },
  });
  const loginForm = useForm<SignInValues>({ resolver: zodResolver(signInSchema), defaultValues: { email: "", password: "" } });
  const body = useMemo(() => (draft ? toSubmitRequest(draft, info) : null), [draft, info]);
  const hasPriced = useMemo(() => (draft ? toEstimateRequest(draft, info) !== null : false), [draft, info]);

  useEffect(() => {
    if (!draft) router.replace("/cotizar");
    else if (!hasPriced) router.replace("/cotizar/esquema");
  }, [draft, hasPriced, router]);
  if (!draft || !body) return null;

  const billingErrors = validateBilling(draft.details.billing);
  const setBilling = (patch: Partial<DraftDetails["billing"]>) => update((d) => ({ ...d, details: { ...d.details, billing: { ...d.details.billing, ...patch } } }));
  const busy = register.isPending || signIn.isPending || submit.isPending;
  const signedIn = me.data ?? null;

  function send() {
    submit.mutate(
      { body: body!, idempotencyKey: draft!.details.idempotencyKey },
      {
        onSuccess: (job) => {
          rememberLastRequest({ id: job.id, code: job.code });
          // The draft is cleared on screen 8, so this screen never re-renders without it.
          router.push(`/cotizar/enviado/${job.code}`);
        },
      },
    );
  }

  const billingValid = () => {
    setShowBillingErrors(true);
    return Object.keys(billingErrors).length === 0;
  };

  const onRegister = registerForm.handleSubmit((values) => {
    if (!billingValid()) return;
    register.mutate(
      { ...values, locale: locale as "pt" | "es" | "en", sendConfirmationEmail: false },
      {
        onSuccess: send,
        onError: (error) => {
          if (error instanceof ApiError && error.code === "email_taken") {
            loginForm.setValue("email", values.email);
            setTab("login");
            return;
          }
          applyServerFieldErrors(error, REGISTER_FIELDS, registerForm.setError);
        },
      },
    );
  });
  const onLogin = loginForm.handleSubmit((values) => {
    if (!billingValid()) return;
    signIn.mutate(values, { onSuccess: send });
  });

  const billing = (
    <Billing billing={draft.details.billing} errors={showBillingErrors ? billingErrors : {}} onChange={setBilling} />
  );

  return (
    <div className="flex flex-1 flex-col pb-12">
      <h1 className="text-[26px] leading-tight font-bold tracking-tight">{t("title")}</h1>
      <p className="mt-1.5 text-sm text-muted-foreground">{signedIn ? t("subtitleSignedIn") : t("subtitle")}</p>

      <SubmitError error={submit.error} />

      {me.isPending ? (
        <div role="status" className="mt-5 h-40 animate-pulse rounded-2xl bg-primary-soft motion-reduce:animate-none">
          <span className="sr-only">{tRoot("common.loading")}</span>
        </div>
      ) : signedIn ? (
        signedIn.role !== "client" ? (
          <p role="alert" className="mt-5 rounded-2xl bg-accent-soft p-4 text-sm text-accent-ink">
            {t("notClient")}
          </p>
        ) : (
          <div className="mt-5 flex flex-col gap-4">
            <div className="rounded-2xl border border-border bg-card p-4">
              <p className="text-sm text-muted-foreground">{t("sendingAs")}</p>
              <p className="mt-0.5 font-semibold">{signedIn.name}</p>
              <p className="text-sm text-muted-foreground">{signedIn.email}</p>
            </div>
            {billing}
            <Button size="lg" className="w-full" disabled={busy} onClick={() => billingValid() && send()}>
              {busy && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
              {t("send")}
            </Button>
          </div>
        )
      ) : (
        <div className="mt-5 flex flex-col gap-4">
          {providers.data?.google && (
            <>
              <Button asChild variant="outline" size="lg" className="w-full">
                {/* Full page navigation: Google signs in on the API and comes back here with the draft intact. */}
                <a href={`/api/auth/google?returnUrl=${encodeURIComponent(`/${locale}${pathname}`)}`}>{t("google")}</a>
              </Button>
              <p className="text-center text-xs text-muted-foreground">{t("googleTerms")}</p>
              <div className="flex items-center gap-3 text-xs text-muted-foreground">
                <span aria-hidden="true" className="h-px flex-1 bg-border" />
                {t("orEmail")}
                <span aria-hidden="true" className="h-px flex-1 bg-border" />
              </div>
            </>
          )}

          <div role="tablist" aria-label={t("tabsLabel")} className="grid grid-cols-2 gap-1 rounded-2xl border border-border bg-card p-1">
            {(["register", "login"] as const).map((name) => (
              <button
                key={name}
                type="button"
                role="tab"
                id={`tab-${name}`}
                aria-selected={tab === name}
                aria-controls={`panel-${name}`}
                onClick={() => setTab(name)}
                className="h-11 rounded-xl text-[13px] font-medium text-muted-foreground transition-colors aria-selected:bg-primary aria-selected:font-semibold aria-selected:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
              >
                {t(`tabs.${name}`)}
              </button>
            ))}
          </div>

          {tab === "register" ? (
            <form id="panel-register" role="tabpanel" aria-labelledby="tab-register" noValidate onSubmit={onRegister} className="flex flex-col gap-4">
              <AuthError error={register.error} />
              <FormField id="name" autoComplete="name" label={tRoot("register.name")} error={registerForm.formState.errors.name} {...registerForm.register("name")} />
              <FormField id="email" type="email" spellCheck={false} autoComplete="email" label={tRoot("register.email")} error={registerForm.formState.errors.email} {...registerForm.register("email")} />
              <FormField id="phone" type="tel" autoComplete="tel" label={tRoot("register.phone")} hint={tRoot("register.phoneHint")} error={registerForm.formState.errors.phone} {...registerForm.register("phone")} />
              <FormField
                id="password"
                type="password"
                autoComplete="new-password"
                label={tRoot("register.password")}
                hint={tRoot("register.passwordHint", { min: PASSWORD_MIN_LENGTH })}
                error={registerForm.formState.errors.password}
                errorValues={{ min: PASSWORD_MIN_LENGTH }}
                {...registerForm.register("password")}
              />
              {billing}
              {(["acceptTerms", "marketingConsent"] as const).map((name) => (
                <Controller
                  key={name}
                  name={name}
                  control={registerForm.control}
                  render={({ field, fieldState }) => (
                    <div className="flex flex-col gap-1">
                      <label htmlFor={name} className="flex min-h-11 items-start gap-3 text-sm text-muted-foreground">
                        <Checkbox
                          id={name}
                          ref={field.ref}
                          checked={field.value}
                          onCheckedChange={(checked) => field.onChange(checked === true)}
                          aria-invalid={fieldState.error ? true : undefined}
                          className="mt-0.5 size-5"
                        />
                        {tRoot(name === "acceptTerms" ? "register.acceptTerms" : "register.marketing")}
                      </label>
                      {fieldState.error?.message && <p className="pl-8 text-[13px] font-medium text-destructive">{tRoot(fieldState.error.message as never)}</p>}
                    </div>
                  )}
                />
              ))}
              <Button type="submit" size="lg" className="w-full" disabled={busy}>
                {busy && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
                {t("send")}
              </Button>
            </form>
          ) : (
            <form id="panel-login" role="tabpanel" aria-labelledby="tab-login" noValidate onSubmit={onLogin} className="flex flex-col gap-4">
              {register.error instanceof ApiError && register.error.code === "email_taken" && (
                <p role="status" className="rounded-xl bg-primary-soft px-4 py-3 text-sm text-primary">
                  {t("emailTaken")}
                </p>
              )}
              <AuthError error={signIn.error} />
              <FormField id="login-email" type="email" spellCheck={false} autoComplete="email" label={tRoot("signIn.email")} error={loginForm.formState.errors.email} {...loginForm.register("email")} />
              <FormField id="login-password" type="password" autoComplete="current-password" label={tRoot("signIn.password")} error={loginForm.formState.errors.password} {...loginForm.register("password")} />
              {billing}
              <Button type="submit" size="lg" className="w-full" disabled={busy}>
                {busy && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
                {t("signInAndSend")}
              </Button>
            </form>
          )}
        </div>
      )}
    </div>
  );
}

/** "Factura a nombre de una empresa": NIF with check digit and company name (C4.3). */
function Billing({ billing, errors, onChange }: { billing: DraftDetails["billing"]; errors: ReturnType<typeof validateBilling>; onChange: (patch: Partial<DraftDetails["billing"]>) => void }) {
  const t = useTranslations("quote.account.billing");
  const tRoot = useTranslations();
  const id = useId();
  return (
    <div className="flex flex-col gap-3">
      <label htmlFor={id} className="flex min-h-12 items-center gap-3 rounded-xl border border-border bg-card px-3.5 text-sm font-medium">
        <Checkbox id={id} checked={billing.wanted} onCheckedChange={(checked) => onChange({ wanted: checked === true })} className="size-5" />
        {t("wanted")}
      </label>
      {billing.wanted && (
        <div className="grid grid-cols-2 gap-2.5">
          <Field label={t("taxId")} value={billing.taxId} inputMode="numeric" error={errors.taxId && tRoot(errors.taxId as never)} onChange={(taxId) => onChange({ taxId: taxId.replace(/\D/g, "").slice(0, 9) })} />
          <Field label={t("companyName")} value={billing.companyName} error={errors.companyName && tRoot(errors.companyName as never)} onChange={(companyName) => onChange({ companyName })} />
        </div>
      )}
    </div>
  );
}

function Field({ label, value, error, inputMode, onChange }: { label: string; value: string; error?: string; inputMode?: "numeric"; onChange: (value: string) => void }) {
  const id = useId();
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] font-semibold">
        {label}
      </label>
      <input
        id={id}
        value={value}
        inputMode={inputMode}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        onChange={(e) => onChange(e.target.value)}
        className="h-12 w-full min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3.5 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive"
      />
      {error && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

function AuthError({ error }: { error: Error | null }) {
  const t = useTranslations("errors");
  if (!(error instanceof ApiError) || error.code === "validation_failed" || error.code === "email_taken") return null;
  return (
    <p role="alert" className="rounded-xl bg-destructive/10 px-4 py-3 text-sm font-medium text-destructive">
      {t(error.code)}
    </p>
  );
}

/** Errors of POST /api/jobs: the draft stays, so the client can fix it and send again with the same key. */
function SubmitError({ error }: { error: Error | null }) {
  const t = useTranslations("quote.account.submitErrors");
  if (!error) return null;
  const code = error instanceof ApiError ? error.problem?.code : undefined;
  const messages = error instanceof ApiError ? Object.values(error.problem?.errors ?? {}).flat() : [];
  return (
    <div role="alert" className={cn("mt-4 rounded-2xl px-4 py-3 text-sm", code === "out_of_coverage" ? "bg-accent-soft text-accent-ink" : "bg-destructive/10 text-destructive")}>
      <p className="font-semibold">
        {code === "out_of_coverage" ? t("outOfCoverage") : code === "unauthenticated" ? t("sessionExpired") : code === "validation_failed" ? t("invalid") : t("generic")}
      </p>
      {messages.length > 0 && (
        <ul className="mt-1 list-disc pl-5">
          {messages.map((message, i) => (
            <li key={i}>{message}</li>
          ))}
        </ul>
      )}
      {code === "out_of_coverage" && (
        <Link href="/cotizar/direccion" className="mt-2 inline-block font-semibold underline underline-offset-4">
          {t("editAddress")}
        </Link>
      )}
    </div>
  );
}
