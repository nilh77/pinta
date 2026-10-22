"use client";

import { CircleAlert, CircleCheck, Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useConfirmEmail, useMe, useResendConfirmation } from "@/lib/auth/use-auth";

/** Landing page of the confirmation link: confirms once on open (C4.5). */
export function ConfirmEmail({ userId, token }: { userId?: string; token?: string }) {
  const t = useTranslations();
  const confirm = useConfirmEmail();
  const me = useMe();
  const resend = useResendConfirmation();
  const started = useRef(false);
  const hasParams = Boolean(userId && token);

  useEffect(() => {
    // React runs effects twice in development; the link must be used once.
    if (!hasParams || started.current) return;
    started.current = true;
    confirm.mutate({ userId: userId!, token: token! });
  }, [confirm, hasParams, token, userId]);

  if (hasParams && (confirm.isIdle || confirm.isPending)) {
    return (
      <p role="status" className="flex items-center gap-2 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin" />
        {t("confirmEmail.loading")}
      </p>
    );
  }

  if (confirm.isSuccess) {
    return (
      <div role="status" className="flex flex-col gap-4">
        <p className="flex items-center gap-2 text-lg font-semibold text-primary">
          <CircleCheck aria-hidden="true" className="size-6" />
          {t("confirmEmail.success")}
        </p>
        <p className="text-muted-foreground">{t("confirmEmail.successBody")}</p>
        <Button asChild size="lg" className="w-full">
          <Link href="/">{t("confirmEmail.goHome")}</Link>
        </Button>
      </div>
    );
  }

  const canResend = me.data && !me.data.emailConfirmed;
  return (
    <div role="alert" className="flex flex-col gap-4">
      <p className="flex items-center gap-2 text-lg font-semibold text-destructive">
        <CircleAlert aria-hidden="true" className="size-6" />
        {t("confirmEmail.error")}
      </p>
      <p className="text-muted-foreground">{t("confirmEmail.errorBody")}</p>
      {canResend ? (
        <>
          <Button size="lg" className="w-full" disabled={resend.isPending || resend.isSuccess} onClick={() => resend.mutate()}>
            {resend.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
            {t("confirmEmail.resend")}
          </Button>
          <p role="status" className="text-sm">
            {resend.isSuccess && t("emailBanner.resent")}
            {resend.error instanceof ApiError && t(`errors.${resend.error.code}`)}
          </p>
        </>
      ) : (
        !me.isPending && (
          <p className="text-sm text-muted-foreground">
            <Link href="/entrar" className="font-semibold text-primary underline-offset-4 hover:underline">
              {t("signIn.submit")}
            </Link>{" "}
            · {t("confirmEmail.signInToResend")}
          </p>
        )
      )}
    </div>
  );
}
