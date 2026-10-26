"use client";

import { CheckCircle2, Loader2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useState, type ComponentProps } from "react";
import { Button } from "@/components/ui/button";
import { useRouter } from "@/i18n/navigation";
import { api } from "@/lib/api/client";
import { toEstimateRequest, type DraftDetails } from "@/lib/quote/draft";
import { addBusinessDays, addDays, lisbonToday, MAX_RANGE_DAYS, POSTAL_CODE, validateAddress, validateDates, type DetailsErrors } from "@/lib/quote/request";
import { cn } from "@/lib/utils";
import { QuoteShell, type QuoteContext } from "./quote-shell";
import { Waitlist } from "./waitlist";

type Coverage = { status: "idle" | "checking" | "covered" | "outside" | "error"; municipality?: string | null };

/** Screen 6 · Dirección y fecha (board Direccion) [C4.1, C4.2]. */
export function AddressStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={3} back={{ href: "/cotizar/fotos", label: t("address.back") }}>
      {(context) => <AddressForm {...context} />}
    </QuoteShell>
  );
}

/** Postal codes are typed as 2710123 or 2710-123; store them as NNNN-NNN. */
const formatPostalCode = (value: string) => {
  const digits = value.replace(/\D/g, "").slice(0, 7);
  return digits.length > 4 ? `${digits.slice(0, 4)}-${digits.slice(4)}` : digits;
};

function AddressForm({ draft, catalog, info, update }: QuoteContext) {
  const t = useTranslations("quote.address");
  const tRoot = useTranslations();
  const locale = useLocale();
  const router = useRouter();
  const [coverage, setCoverage] = useState<Coverage>(() =>
    draft?.details.address.municipality ? { status: "covered", municipality: draft.details.address.municipality } : { status: "idle" },
  );
  const [showErrors, setShowErrors] = useState(false);
  const request = useMemo(() => (draft ? toEstimateRequest(draft, info) : null), [draft, info]);

  useEffect(() => {
    if (!draft) router.replace("/cotizar");
    else if (!request) router.replace("/cotizar/esquema");
  }, [draft, request, router]);
  if (!draft || !request) return null;

  const { address, dates } = draft.details;
  const today = lisbonToday();
  const earliest = addBusinessDays(today, catalog.minLeadBusinessDays);
  const errors: DetailsErrors = { ...validateAddress(address), ...validateDates(dates, earliest) };
  const setAddress = (patch: Partial<DraftDetails["address"]>) => update((d) => ({ ...d, details: { ...d.details, address: { ...d.details.address, ...patch } } }));
  const setDates = (patch: Partial<DraftDetails["dates"]>) => update((d) => ({ ...d, details: { ...d.details, dates: { ...d.details.dates, ...patch } } }));

  async function checkCoverage(postalCode: string) {
    if (!POSTAL_CODE.test(postalCode)) return setCoverage({ status: "idle" });
    setCoverage({ status: "checking" });
    const result = await api.GET("/api/coverage/check", { params: { query: { postalCode } } }).catch(() => null);
    if (!result?.data) return setCoverage({ status: "error" });
    const { covered, municipality } = result.data;
    setCoverage({ status: covered ? "covered" : "outside", municipality });
    setAddress({ municipality: covered ? municipality : null, ...(covered && !address.locality.trim() && municipality ? { locality: municipality } : {}) });
  }

  const onContinue = () => {
    setShowErrors(true);
    if (Object.keys(errors).length > 0 || coverage.status !== "covered") {
      if (coverage.status === "idle" || coverage.status === "error") void checkCoverage(address.postalCode);
      return;
    }

    update((d) => ({ ...d, step: "account" }));
    router.push("/cotizar/cuenta");
  };
  const earliestText = new Intl.DateTimeFormat(locale === "en" ? "en-GB" : locale === "es" ? "es-ES" : "pt-PT", {
    day: "numeric",
    month: "long",
    timeZone: "UTC",
  }).format(new Date(`${earliest}T00:00:00Z`));
  const error = (field: keyof DetailsErrors) => (showErrors && errors[field] ? tRoot(errors[field] as never, { date: earliestText } as never) : undefined);

  return (
    <div className="flex flex-1 flex-col pb-36 lg:pb-12">
      <h1 className="text-[26px] leading-tight font-bold tracking-tight">{t("title")}</h1>

      <div className="mt-4 flex flex-col gap-3">
        <TextField label={t("line1")} autoComplete="street-address" value={address.line1} error={error("line1")} onChange={(line1) => setAddress({ line1 })} />
        <TextField label={t("floor")} value={address.floor} optional={t("optional")} onChange={(floor) => setAddress({ floor })} />
        <div className="grid grid-cols-2 gap-2.5">
          <TextField
            label={t("postalCode")}
            inputMode="numeric"
            autoComplete="postal-code"
            placeholder="2710-123"
            value={address.postalCode}
            error={error("postalCode")}
            onChange={(value) => {
              const postalCode = formatPostalCode(value);
              setAddress({ postalCode, municipality: null });
              setCoverage({ status: "idle" });
              if (POSTAL_CODE.test(postalCode)) void checkCoverage(postalCode);
            }}
            onBlur={() => void checkCoverage(address.postalCode)}
          />
          <TextField label={t("locality")} autoComplete="address-level2" value={address.locality} error={error("locality")} onChange={(locality) => setAddress({ locality })} />
        </div>

        <div aria-live="polite">
          {coverage.status === "checking" && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
              {t("checking")}
            </p>
          )}
          {coverage.status === "covered" && (
            <p className="flex items-center gap-2.5 rounded-xl bg-primary-soft px-3.5 py-3 text-[13px] font-medium text-[#0b5f59]">
              <CheckCircle2 aria-hidden="true" className="size-[18px] shrink-0 text-primary" />
              {t("covered", { municipality: coverage.municipality ?? "" })}
            </p>
          )}
          {coverage.status === "error" && <p className="text-sm font-medium text-destructive">{t("coverageError")}</p>}
          {coverage.status === "outside" && <Waitlist postalCode={address.postalCode} locale={locale} />}
        </div>

        <TextField label={t("accessNotes")} value={address.accessNotes} optional={t("optional")} placeholder={t("accessNotesPlaceholder")} onChange={(accessNotes) => setAddress({ accessNotes })} />
      </div>

      <fieldset className="mt-6" aria-describedby={error("dates") ? "dates-error" : undefined}>
        <legend className="mb-2 text-[13px] font-semibold">{t("when")}</legend>
        <div className="grid grid-cols-2 gap-1 rounded-2xl border border-border bg-card p-1">
          {([true, false] as const).map((asap) => (
            <button
              key={String(asap)}
              type="button"
              aria-pressed={dates.asap === asap}
              onClick={() => setDates({ asap })}
              className="h-11 rounded-xl text-[13px] font-medium text-muted-foreground transition-colors aria-pressed:bg-primary aria-pressed:font-semibold aria-pressed:text-primary-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none motion-reduce:transition-none"
            >
              {t(asap ? "asap" : "chooseDates")}
            </button>
          ))}
        </div>
        {error("dates") && (
          <p id="dates-error" role="alert" className="mt-2 text-sm font-medium text-destructive">
            {error("dates")}
          </p>
        )}
        {dates.asap === false && (
          <div className="mt-3 grid grid-cols-2 gap-2.5">
            <TextField type="date" label={t("from")} min={earliest} value={dates.from} error={error("from")} onChange={(from) => setDates({ from, ...(dates.to && dates.to < from ? { to: from } : {}) })} />
            <TextField type="date" label={t("to")} min={dates.from || earliest} max={dates.from ? addDays(dates.from, MAX_RANGE_DAYS) : undefined} value={dates.to} error={error("to")} onChange={(to) => setDates({ to })} />
          </div>
        )}
        <p className="mt-2.5 text-xs leading-relaxed text-muted-foreground">
          {dates.asap === false ? t("datesHint", { days: catalog.minLeadBusinessDays }) : t("asapHint")} {t("agreeLater")}
        </p>
      </fieldset>

      <div className="fixed inset-x-0 bottom-0 z-30 rounded-t-[20px] border-t border-border bg-card px-5 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))] lg:static lg:mt-6 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0">
        <div className="mx-auto max-w-2xl">
          <Button size="lg" className="w-full" disabled={coverage.status === "outside"} onClick={onContinue}>
            {t("continue")}
          </Button>
        </div>
      </div>
    </div>
  );
}

type TextFieldProps = Omit<ComponentProps<"input">, "onChange" | "value"> & {
  label: string;
  value: string;
  error?: string;
  optional?: string;
  onChange: (value: string) => void;
};

function TextField({ label, value, error, optional, onChange, className, ...input }: TextFieldProps) {
  const id = useId();
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] font-semibold">
        {label}
        {optional && <span className="ml-1 font-normal text-muted-foreground">({optional})</span>}
      </label>
      <input
        id={id}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        onChange={(e) => onChange(e.target.value)}
        className={cn(
          "h-12 w-full min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3.5 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive",
          className,
        )}
        {...input}
      />
      {error && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
