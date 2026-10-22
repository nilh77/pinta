"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { safeNextPath, signInSchema, type SignInValues } from "@/lib/auth/schemas";
import { useSignIn } from "@/lib/auth/use-auth";
import { FormError } from "./form-error";

export function SignInForm({ next }: { next?: string }) {
  const t = useTranslations("signIn");
  const router = useRouter();
  const signIn = useSignIn();
  const form = useForm<SignInValues>({ resolver: zodResolver(signInSchema), defaultValues: { email: "", password: "" } });
  const { errors } = form.formState;

  const onSubmit = form.handleSubmit((values) =>
    signIn.mutate(values, { onSuccess: () => router.push(safeNextPath(next)) }),
  );

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <FormError error={signIn.error} />
      <FormField id="email" type="email" autoComplete="email" label={t("email")} error={errors.email} {...form.register("email")} />
      <FormField
        id="password"
        type="password"
        autoComplete="current-password"
        label={t("password")}
        error={errors.password}
        {...form.register("password")}
      />
      <Button type="submit" size="lg" disabled={signIn.isPending} className="mt-2 w-full">
        {signIn.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("submit")}
      </Button>
      <p className="text-center text-sm text-muted-foreground">
        {t("noAccount")}{" "}
        <Link href="/registro" className="font-semibold text-primary underline-offset-4 hover:underline">
          {t("createAccount")}
        </Link>
      </p>
    </form>
  );
}
