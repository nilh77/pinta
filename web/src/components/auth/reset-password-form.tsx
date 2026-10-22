"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert, CircleCheck, Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { PASSWORD_MIN_LENGTH, resetPasswordSchema, type ResetPasswordValues } from "@/lib/auth/schemas";
import { useResetPassword } from "@/lib/auth/use-auth";
import { FormError } from "./form-error";

/** Target of the link in the reset email (S1). */
export function ResetPasswordForm({ userId, token }: { userId?: string; token?: string }) {
  const t = useTranslations("resetPassword");
  const reset = useResetPassword();
  const form = useForm<ResetPasswordValues>({ resolver: zodResolver(resetPasswordSchema), defaultValues: { password: "" } });
  const invalidLink = !userId || !token || (reset.error instanceof ApiError && reset.error.problem?.errors?.token !== undefined);

  if (reset.isSuccess) {
    return (
      <div role="status" className="flex flex-col gap-4">
        <p className="flex items-center gap-2 text-lg font-semibold text-primary">
          <CircleCheck aria-hidden="true" className="size-6" />
          {t("success")}
        </p>
        <p className="text-muted-foreground">{t("successBody")}</p>
        <Button asChild size="lg" className="w-full">
          <Link href="/entrar">{t("signIn")}</Link>
        </Button>
      </div>
    );
  }

  if (invalidLink) {
    return (
      <div role="alert" className="flex flex-col gap-4">
        <p className="flex items-center gap-2 text-lg font-semibold text-destructive">
          <CircleAlert aria-hidden="true" className="size-6" />
          {t("invalid")}
        </p>
        <p className="text-muted-foreground">{t("invalidBody")}</p>
        <Button asChild size="lg" className="w-full">
          <Link href="/recuperar">{t("requestAgain")}</Link>
        </Button>
      </div>
    );
  }

  const serverPasswordError = reset.error instanceof ApiError ? reset.error.problem?.errors?.password?.[0] : undefined;
  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => reset.mutate({ userId, token, password: values.password }))}
      className="flex flex-col gap-4"
    >
      {!serverPasswordError && <FormError error={reset.error} />}
      <FormField
        id="password"
        type="password"
        autoComplete="new-password"
        label={t("password")}
        hint={t("passwordHint", { min: PASSWORD_MIN_LENGTH })}
        error={form.formState.errors.password ?? (serverPasswordError ? { type: "server", message: serverPasswordError } : undefined)}
        errorValues={{ min: PASSWORD_MIN_LENGTH }}
        {...form.register("password")}
      />
      <Button type="submit" size="lg" disabled={reset.isPending} className="mt-2 w-full">
        {reset.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("submit")}
      </Button>
    </form>
  );
}
