"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Controller, useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Link, useRouter } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { PASSWORD_MIN_LENGTH, registerSchema, type RegisterInput, type RegisterValues } from "@/lib/auth/schemas";
import { applyServerFieldErrors } from "@/lib/auth/server-errors";
import { useRegister } from "@/lib/auth/use-auth";
import { FormError } from "./form-error";

const FIELDS = ["name", "email", "phone", "password", "acceptTerms"] as const;

export function RegisterForm() {
  const t = useTranslations();
  const locale = useLocale();
  const router = useRouter();
  const register = useRegister();
  const form = useForm<RegisterInput, unknown, RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: "", email: "", phone: "+351 ", password: "", acceptTerms: false, marketingConsent: false },
  });
  const { errors } = form.formState;
  const emailTaken = register.error instanceof ApiError && register.error.code === "email_taken";

  const onSubmit = form.handleSubmit((values) =>
    register.mutate(
      { ...values, locale },
      {
        onSuccess: () => router.push("/"),
        onError: (error) => applyServerFieldErrors(error, FIELDS, form.setError),
      },
    ),
  );

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <FormError error={register.error}>
        {emailTaken && (
          <Link href="/entrar" className="font-semibold underline underline-offset-4">
            {t("register.signIn")}
          </Link>
        )}
      </FormError>
      <FormField
        id="name"
        autoComplete="name"
        label={t("register.name")}
        placeholder={t("register.namePlaceholder")}
        error={errors.name}
        {...form.register("name")}
      />
      <FormField
        id="email"
        type="email"
        spellCheck={false}
        autoComplete="email"
        label={t("register.email")}
        placeholder={t("register.emailPlaceholder")}
        error={errors.email}
        {...form.register("email")}
      />
      <FormField
        id="phone"
        type="tel"
        autoComplete="tel"
        label={t("register.phone")}
        hint={t("register.phoneHint")}
        error={errors.phone}
        {...form.register("phone")}
      />
      <FormField
        id="password"
        type="password"
        autoComplete="new-password"
        label={t("register.password")}
        hint={t("register.passwordHint", { min: PASSWORD_MIN_LENGTH })}
        error={errors.password}
        errorValues={{ min: PASSWORD_MIN_LENGTH }}
        {...form.register("password")}
      />
      <CheckboxField id="acceptTerms" label={t("register.acceptTerms")} control={form.control} name="acceptTerms" />
      <CheckboxField id="marketingConsent" label={t("register.marketing")} control={form.control} name="marketingConsent" />
      <Button type="submit" size="lg" disabled={register.isPending} className="mt-2 w-full">
        {register.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("register.submit")}
      </Button>
      <p className="text-center text-sm text-muted-foreground">
        {t("register.haveAccount")}{" "}
        <Link href="/entrar" className="font-semibold text-primary underline-offset-4 hover:underline">
          {t("register.signIn")}
        </Link>
      </p>
    </form>
  );
}

function CheckboxField({
  id,
  label,
  name,
  control,
}: {
  id: string;
  label: string;
  name: "acceptTerms" | "marketingConsent";
  control: ReturnType<typeof useForm<RegisterInput, unknown, RegisterValues>>["control"];
}) {
  const t = useTranslations();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <div className="flex flex-col gap-1">
          <div className="flex min-h-11 items-start gap-3">
            <Checkbox
              id={id}
              ref={field.ref}
              checked={field.value}
              onCheckedChange={(checked) => field.onChange(checked === true)}
              onBlur={field.onBlur}
              aria-invalid={fieldState.error ? true : undefined}
              aria-describedby={fieldState.error ? `${id}-error` : undefined}
              className="mt-0.5 size-5"
            />
            <Label htmlFor={id} className="text-sm leading-normal font-normal text-muted-foreground">
              {label}
            </Label>
          </div>
          {fieldState.error?.message && (
            <p id={`${id}-error`} className="pl-8 text-[13px] font-medium text-destructive">
              {t(fieldState.error.message as never)}
            </p>
          )}
        </div>
      )}
    />
  );
}
