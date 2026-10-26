"use client";

import { MapPinOff } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api/client";

/** Out of the service area: leave an email for when Pinta gets there (C4.1). */
export function Waitlist({ postalCode, locale }: { postalCode: string; locale: string }) {
  const t = useTranslations("quote.address.waitlist");
  const [email, setEmail] = useState("");
  const [state, setState] = useState<"idle" | "sending" | "done" | "error">("idle");

  return (
    <div role="alert" className="flex flex-col gap-3 rounded-2xl border border-[#f2d27a] bg-[#fffbeb] p-4 text-[#4a3700]">
      <p className="flex items-start gap-2.5 text-sm">
        <MapPinOff aria-hidden="true" className="mt-0.5 size-[18px] shrink-0" />
        {t("outside")}
      </p>
      {state === "done" ? (
        <p className="text-sm font-semibold">{t("done")}</p>
      ) : (
        <form
          className="flex flex-col gap-2 sm:flex-row"
          onSubmit={async (e) => {
            e.preventDefault();
            setState("sending");
            const result = await api
              .POST("/api/coverage/waitlist", { body: { email, postalCode, locale: locale as "pt" | "es" | "en" } })
              .catch(() => null);
            setState(result?.response.ok ? "done" : "error");
          }}
        >
          <label className="sr-only" htmlFor="waitlist-email">
            {t("email")}
          </label>
          <input
            id="waitlist-email"
            type="email"
            required
            autoComplete="email"
            placeholder={t("email")}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base text-foreground outline-none sm:flex-1 focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
          />
          <Button type="submit" disabled={state === "sending"}>
            {t("join")}
          </Button>
        </form>
      )}
      {state === "error" && <p className="text-sm font-medium text-destructive">{t("error")}</p>}
    </div>
  );
}
