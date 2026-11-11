"use client";

import { zodResolver } from "@hookform/resolvers/zod";
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
    // A painter without a target lands on their jobs (P1).
    signIn.mutate(values, { onSuccess: (me) => router.push(!next && me.role === "painter" ? "/pintor" : safeNextPath(next)) }),
  );

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <FormError error={signIn.error} />
      <FormField
        id="email"
        type="email"
        spellCheck={false}
        autoComplete="email"
        label={t("email")}
        error={errors.email}
        {...form.register("email")}
      />
      <FormField
        id="password"
        type="password"
        autoComplete="current-password"
        label={t("password")}
        error={errors.password}
        {...form.register("password")}
      />
      <Link href="/recuperar" className="-mt-2 self-end text-sm font-semibold text-primary underline-offset-4 hover:underline">
        {t("forgot")}
      </Link>
      <Button pending={signIn.isPending} type="submit" size="lg" disabled={signIn.isPending} className="mt-2 w-full">
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
