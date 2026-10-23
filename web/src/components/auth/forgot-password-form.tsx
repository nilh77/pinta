"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, MailCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { forgotPasswordSchema, type ForgotPasswordValues } from "@/lib/auth/schemas";
import { useForgotPassword } from "@/lib/auth/use-auth";
import { FormError } from "./form-error";

/** Recuperar contraseña (S1): the answer is the same whether the email has an account or not. */
export function ForgotPasswordForm() {
  const t = useTranslations("forgotPassword");
  const forgot = useForgotPassword();
  const form = useForm<ForgotPasswordValues>({ resolver: zodResolver(forgotPasswordSchema), defaultValues: { email: "" } });

  if (forgot.isSuccess) {
    return (
      <div role="status" className="flex flex-col gap-4">
        <p className="flex items-start gap-2.5 rounded-xl bg-primary-soft px-3.5 py-3 text-sm text-[#0b5f59]">
          <MailCheck aria-hidden="true" className="mt-0.5 size-[18px] shrink-0 text-primary" />
          {t("sent", { email: forgot.variables.email })}
        </p>
        <BackToSignIn />
      </div>
    );
  }

  return (
    <form noValidate onSubmit={form.handleSubmit((values) => forgot.mutate(values))} className="flex flex-col gap-4">
      <FormError error={forgot.error} />
      <FormField
        id="email"
        type="email"
        spellCheck={false}
        autoComplete="email"
        label={t("email")}
        error={form.formState.errors.email}
        {...form.register("email")}
      />
      <Button type="submit" size="lg" disabled={forgot.isPending} className="mt-2 w-full">
        {forgot.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("submit")}
      </Button>
      <BackToSignIn />
    </form>
  );
}

function BackToSignIn() {
  const t = useTranslations("forgotPassword");
  return (
    <Link href="/entrar" className="text-center text-sm font-semibold text-primary underline-offset-4 hover:underline">
      {t("backToSignIn")}
    </Link>
  );
}
