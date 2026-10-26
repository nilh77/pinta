"use client";

import { CheckCircle2, Loader2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";
import { Waitlist } from "@/components/quote/waitlist";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api/client";
import { POSTAL_CODE } from "@/lib/quote/request";

type Result = { status: "idle" | "invalid" | "checking" | "error" } | { status: "covered" | "outside"; postalCode: string; municipality?: string | null };

/** Screen 0 · coverage checker (C1.1, C4.1): «Sí llegamos a {localidad}» or the waitlist. */
export function CoverageCheck() {
  const t = useTranslations("home.coverage");
  const locale = useLocale();
  const id = useId();
  const [postalCode, setPostalCode] = useState("");
  const [result, setResult] = useState<Result>({ status: "idle" });

  async function check() {
    // Seven digits typed without the dash are accepted as 1234-567.
    const code = /^\d{7}$/.test(postalCode.trim()) ? `${postalCode.trim().slice(0, 4)}-${postalCode.trim().slice(4)}` : postalCode.trim();
    if (!POSTAL_CODE.test(code)) return setResult({ status: "invalid" });
    setPostalCode(code);
    setResult({ status: "checking" });
    const response = await api.GET("/api/coverage/check", { params: { query: { postalCode: code } } }).catch(() => null);
    if (!response?.data) return setResult({ status: "error" });
    setResult({ status: response.data.covered ? "covered" : "outside", postalCode: code, municipality: response.data.municipality });
  }

  return (
    <section aria-labelledby={`${id}-title`} className="mt-6 rounded-[18px] border border-border bg-card p-4">
      <h2 id={`${id}-title`} className="text-sm font-semibold">
        {t("title")}
      </h2>
      <form
        noValidate
        className="mt-2 flex gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          void check();
        }}
      >
        <label htmlFor={`${id}-postal`} className="sr-only">
          {t("label")}
        </label>
        <input
          id={`${id}-postal`}
          inputMode="numeric"
          autoComplete="postal-code"
          placeholder={t("placeholder")}
          value={postalCode}
          aria-invalid={result.status === "invalid" ? true : undefined}
          aria-describedby={`${id}-result`}
          onChange={(event) => {
            setPostalCode(event.target.value);
            if (result.status !== "idle") setResult({ status: "idle" });
          }}
          className="h-11 min-w-0 flex-1 rounded-xl border-[1.5px] border-input bg-background px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
        />
        <Button type="submit" variant="outline" disabled={result.status === "checking"}>
          {result.status === "checking" && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
          {t("check")}
        </Button>
      </form>
      <div id={`${id}-result`} aria-live="polite" className="empty:hidden mt-3">
        {result.status === "invalid" && <p className="text-sm font-medium text-destructive">{t("invalid")}</p>}
        {result.status === "error" && <p className="text-sm font-medium text-destructive">{t("error")}</p>}
        {result.status === "covered" && (
          <p className="flex items-center gap-2 text-sm font-semibold text-primary">
            <CheckCircle2 aria-hidden="true" className="size-[18px] shrink-0" />
            {t("covered", { municipality: result.municipality ?? result.postalCode })}
          </p>
        )}
        {result.status === "outside" && <Waitlist postalCode={result.postalCode} locale={locale} />}
      </div>
    </section>
  );
}
