"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert, Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { FormError } from "@/components/auth/form-error";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { useRouter } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { PASSWORD_MIN_LENGTH, resetPasswordSchema, type ResetPasswordValues } from "@/lib/auth/schemas";
import { useAcceptInvite, usePainterProfile, useUpdatePainterProfile, type PainterProfile } from "@/lib/painter/use-painter";

const compact = (value: string) => value.replace(/\s/g, "").toUpperCase();

const profileSchema = z.object({
  phone: z
    .string()
    .trim()
    .min(1, "validation.required")
    .refine((v) => /^\+[1-9]\d{7,14}$/.test(v.replace(/\s/g, "")), "validation.phone"),
  taxId: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\d{9}$/.test(v), "painter.onboarding.errors.taxId"),
  municipalities: z.array(z.string()).min(1, "painter.onboarding.errors.zones"),
  iban: z
    .string()
    .refine((v) => v.trim() === "" || /^PT50\d{21}$/.test(compact(v)), "painter.onboarding.errors.iban"),
});
type ProfileValues = z.infer<typeof profileSchema>;

/**
 * /pintor/alta (A4.2): the invitation link sets the password, then the painter checks the profile the admin prefilled.
 * The password just chosen authorises replacing an IBAN the admin already entered.
 */
export function PainterOnboarding({ userId, token }: { userId?: string; token?: string }) {
  const t = useTranslations("painter.onboarding");
  const accept = useAcceptInvite();
  const [password, setPassword] = useState<string | null>(null);
  const form = useForm<ResetPasswordValues>({ resolver: zodResolver(resetPasswordSchema), defaultValues: { password: "" } });
  const invalidLink = !userId || !token || (accept.error instanceof ApiError && accept.error.problem?.errors?.token !== undefined);

  if (password) return <ProfileStep password={password} />;

  if (invalidLink) {
    return (
      <div role="alert" className="flex flex-col gap-3">
        <p className="flex items-center gap-2 text-lg font-semibold text-destructive">
          <CircleAlert aria-hidden="true" className="size-6" />
          {t("invalid")}
        </p>
        <p className="text-muted-foreground">{t("invalidBody")}</p>
      </div>
    );
  }

  const serverPasswordError = accept.error instanceof ApiError ? accept.error.problem?.errors?.password?.[0] : undefined;
  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) =>
        accept.mutate({ userId, token, password: values.password }, { onSuccess: () => setPassword(values.password) }),
      )}
      className="flex flex-col gap-4"
    >
      <p className="text-sm text-muted-foreground">{t("passwordIntro")}</p>
      {!serverPasswordError && <FormError error={accept.error} />}
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
      <Button type="submit" size="lg" disabled={accept.isPending} className="mt-2 w-full">
        {accept.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("continue")}
      </Button>
    </form>
  );
}

function ProfileStep({ password }: { password: string }) {
  const t = useTranslations("painter.onboarding");
  const profile = usePainterProfile();

  if (profile.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-8 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (profile.isError) {
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-destructive">{t("error")}</p>
        <Button variant="outline" onClick={() => profile.refetch()}>
          {t("retry")}
        </Button>
      </div>
    );
  }

  return <ProfileForm profile={profile.data} password={password} />;
}

function ProfileForm({ profile, password }: { profile: PainterProfile; password: string }) {
  const t = useTranslations("painter.onboarding");
  const router = useRouter();
  const save = useUpdatePainterProfile();
  const form = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { phone: profile.phone ?? "", taxId: profile.taxId ?? "", municipalities: [...profile.municipalities], iban: "" },
  });
  const { errors } = form.formState;
  const server = save.error instanceof ApiError ? save.error.problem?.errors : undefined;
  const serverError = (field: string) => {
    const message = server?.[field]?.[0];
    return message ? { type: "server", message } : undefined;
  };

  const onSubmit = form.handleSubmit((values) =>
    save.mutate(
      {
        phone: values.phone.replace(/\s/g, ""),
        taxId: values.taxId.trim() || null,
        municipalities: values.municipalities,
        iban: values.iban.trim() ? compact(values.iban) : null,
        currentPassword: values.iban.trim() && profile.ibanMasked ? password : null,
      },
      { onSuccess: () => router.push("/pintor") },
    ),
  );

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
      <p className="text-sm text-muted-foreground">{t("profileIntro", { name: profile.name })}</p>
      {!server && <FormError error={save.error} />}
      <FormField
        id="phone"
        type="tel"
        autoComplete="tel"
        label={t("phone")}
        hint={t("phoneHint")}
        error={errors.phone ?? serverError("phone")}
        {...form.register("phone")}
      />
      <FormField
        id="taxId"
        inputMode="numeric"
        label={t("taxId")}
        error={errors.taxId ?? serverError("taxId")}
        {...form.register("taxId")}
      />
      <Controller
        control={form.control}
        name="municipalities"
        render={({ field }) => (
          <fieldset className="flex flex-col gap-2" aria-describedby={errors.municipalities ? "zones-error" : undefined}>
            <legend className="mb-1.5 text-[13px] font-semibold">{t("zones")}</legend>
            <div className="flex flex-wrap gap-2">
              {profile.availableMunicipalities.map((m) => {
                const checked = field.value.includes(m);
                return (
                  <label
                    key={m}
                    className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border border-border px-3 text-sm font-medium has-checked:border-primary has-checked:bg-primary-soft has-focus-visible:ring-[3px] has-focus-visible:ring-ring/50"
                  >
                    <input
                      type="checkbox"
                      className="sr-only"
                      checked={checked}
                      onChange={() => field.onChange(checked ? field.value.filter((x) => x !== m) : [...field.value, m])}
                    />
                    {m}
                  </label>
                );
              })}
            </div>
            {(errors.municipalities || serverError("municipalities")) && (
              <p id="zones-error" className="text-sm text-destructive">
                {errors.municipalities?.message ? t("errors.zones") : serverError("municipalities")?.message}
              </p>
            )}
          </fieldset>
        )}
      />
      <FormField
        id="iban"
        autoComplete="off"
        spellCheck={false}
        label={t("iban")}
        hint={profile.ibanMasked ? t("ibanKeep", { iban: profile.ibanMasked }) : t("ibanHint")}
        placeholder={profile.ibanMasked ?? "PT50 0000 0000 0000 0000 0000 0"}
        error={errors.iban ?? serverError("iban")}
        {...form.register("iban")}
      />
      <Button type="submit" size="lg" disabled={save.isPending} className="mt-2 w-full">
        {save.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("finish")}
      </Button>
    </form>
  );
}
