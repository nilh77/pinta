"use client";

import { MailCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/problem";
import { useMe, useResendConfirmation } from "@/lib/auth/use-auth";

/** Shown while the signed-in user has not confirmed the email (C4.5). */
export function EmailConfirmationBanner() {
  const t = useTranslations();
  const me = useMe();
  const resend = useResendConfirmation();

  if (!me.data || me.data.emailConfirmed) return null;

  return (
    <section
      aria-labelledby="email-banner-title"
      className="mx-auto mt-2 flex w-full max-w-6xl flex-col gap-3 px-4 sm:px-6"
    >
      <div className="flex flex-col gap-3 rounded-2xl bg-accent-soft p-4 text-accent-ink sm:flex-row sm:items-center sm:justify-between">
        <div className="flex gap-3">
          <MailCheck aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
          <div>
            <h2 id="email-banner-title" className="text-base font-bold">
              {t("emailBanner.title")}
            </h2>
            <p className="text-sm">{t("emailBanner.body", { email: me.data.email })}</p>
            <p role="status" className="text-sm font-medium">
              {resend.isSuccess && t("emailBanner.resent")}
              {resend.error instanceof ApiError && t(`errors.${resend.error.code}`)}
            </p>
          </div>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="self-start sm:self-center"
          disabled={resend.isPending}
          onClick={() => resend.mutate()}
        >
          {t("emailBanner.resend")}
        </Button>
      </div>
    </section>
  );
}
