"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert, Loader2, LogOut } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormError } from "@/components/auth/form-error";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { usePathname, useRouter } from "@/i18n/navigation";
import { useAccount, useChangePassword, useRequestDeletion, useUpdateAccount, type AccountProfile } from "@/lib/account/use-account";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { PASSWORD_MIN_LENGTH } from "@/lib/auth/schemas";
import { useSignOut } from "@/lib/auth/use-auth";
import { formatDateTime } from "@/lib/format";

const profileSchema = z.object({
  name: z.string().trim().min(1, "validation.required").max(200, "validation.tooLong"),
  email: z.email("validation.email").max(256, "validation.tooLong"),
  phone: z
    .string()
    .trim()
    .refine((v) => /^\+[1-9]\d{7,14}$/.test(v.replace(/\s/g, "")), "validation.phone"),
  locale: z.enum(["pt", "es", "en"]),
  taxId: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\d{9}$/.test(v), "account.errors.taxId"),
  companyName: z.string().trim().max(200, "validation.tooLong"),
  marketingConsent: z.boolean(),
});
type ProfileValues = z.infer<typeof profileSchema>;

const selectClass =
  "h-11 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

/** Screen 12 · Cuenta (board Perfil) [C5.8]: data, password, language, sign out and deletion request. */
export function AccountScreen() {
  const t = useTranslations("account");
  const account = useAccount();

  if (account.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (account.isError) {
    return (
      <div role="alert" className="flex flex-col items-start gap-3 py-16">
        <p className="text-destructive">{t("error")}</p>
        <Button variant="outline" onClick={() => account.refetch()}>
          {t("retry")}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
      <Section title={t("data.title")}>
        <ProfileForm profile={account.data} />
      </Section>
      <Section title={t("password.title")}>
        <PasswordForm />
      </Section>
      <Section title={t("session.title")}>
        <SignOutButton />
      </Section>
      <Section title={t("deletion.title")}>
        <Deletion profile={account.data} />
      </Section>
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[20px] border border-border bg-card p-4">
      <h2 className="mb-3 font-bold">{title}</h2>
      {children}
    </section>
  );
}

/** Changing the language moves the page to it; a new email waits for its confirmation. */
function ProfileForm({ profile }: { profile: AccountProfile }) {
  const t = useTranslations("account.data");
  const router = useRouter();
  const pathname = usePathname();
  const locale = useLocale();
  const save = useUpdateAccount();
  const form = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      name: profile.name,
      email: profile.email,
      phone: profile.phone ?? "",
      locale: profile.locale,
      taxId: profile.taxId ?? "",
      companyName: profile.companyName ?? "",
      marketingConsent: profile.marketingConsent,
    },
  });
  const { errors } = form.formState;
  const server = save.error instanceof ApiError ? save.error.problem?.errors : undefined;
  const serverError = (field: string) => (server?.[field]?.[0] ? { type: "server", message: server[field][0] } : undefined);

  const submit = form.handleSubmit((v) =>
    save.mutate(
      {
        name: v.name.trim(),
        email: v.email.trim(),
        phone: v.phone.replace(/\s/g, ""),
        locale: v.locale,
        taxId: v.taxId.trim() || null,
        companyName: v.companyName.trim() || null,
        marketingConsent: v.marketingConsent,
      },
      {
        onSuccess: (saved) => {
          toast.success(saved.emailConfirmed ? t("saved") : t("savedConfirm", { email: saved.email }));
          if (saved.locale !== locale) router.replace(pathname, { locale: saved.locale });
        },
      },
    ),
  );

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-4">
      {!server && <FormError error={save.error} />}
      {!profile.emailConfirmed && (
        <p className="flex items-center gap-2 rounded-xl bg-accent-soft px-3 py-2 text-sm text-accent-ink">
          <CircleAlert aria-hidden="true" className="size-4 shrink-0" />
          {t("unconfirmed", { email: profile.email })}
        </p>
      )}
      <div className="grid gap-4 md:grid-cols-2">
        <FormField id="name" autoComplete="name" label={t("name")} error={errors.name ?? serverError("name")} {...form.register("name")} />
        <FormField
          id="email"
          type="email"
          autoComplete="email"
          spellCheck={false}
          label={t("email")}
          hint={t("emailHint")}
          error={errors.email ?? serverError("email")}
          {...form.register("email")}
        />
        <FormField id="phone" type="tel" autoComplete="tel" label={t("phone")} error={errors.phone ?? serverError("phone")} {...form.register("phone")} />
        <div className="flex flex-col gap-1.5">
          <label htmlFor="locale" className="text-[13px] font-semibold">
            {t("locale")}
          </label>
          <select id="locale" className={selectClass} {...form.register("locale")}>
            <option value="pt">Português</option>
            <option value="es">Español</option>
            <option value="en">English</option>
          </select>
          <p className="text-xs text-muted-foreground">{t("localeHint")}</p>
        </div>
        <FormField id="taxId" inputMode="numeric" label={t("taxId")} error={errors.taxId ?? serverError("taxId")} {...form.register("taxId")} />
        <FormField id="companyName" label={t("company")} error={errors.companyName ?? serverError("companyName")} {...form.register("companyName")} />
      </div>
      <label className="flex items-start gap-3 text-sm">
        <input type="checkbox" className="mt-0.5 size-5 accent-[var(--primary)]" {...form.register("marketingConsent")} />
        {t("marketing")}
      </label>
      <Button type="submit" disabled={save.isPending} className="self-start">
        {save.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("save")}
      </Button>
    </form>
  );
}

function PasswordForm() {
  const t = useTranslations("account.password");
  const change = useChangePassword();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const errors = change.error instanceof ApiError ? change.error.problem?.errors : undefined;
  const tooShort = next.length > 0 && next.length < PASSWORD_MIN_LENGTH;

  return (
    <form
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (!current || next.length < PASSWORD_MIN_LENGTH) return;
        change.mutate(
          { currentPassword: current, newPassword: next },
          {
            onSuccess: () => {
              setCurrent("");
              setNext("");
              toast.success(t("done"));
            },
          },
        );
      }}
      className="flex flex-col gap-4"
    >
      {!errors && <FormError error={change.error} />}
      <div className="grid gap-4 md:grid-cols-2">
        <FormField
          id="current-password"
          type="password"
          autoComplete="current-password"
          label={t("current")}
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
          error={errors?.currentPassword ? { type: "server", message: errors.currentPassword[0] } : undefined}
        />
        <FormField
          id="new-password"
          type="password"
          autoComplete="new-password"
          label={t("new")}
          hint={t("hint", { min: PASSWORD_MIN_LENGTH })}
          value={next}
          onChange={(e) => setNext(e.target.value)}
          error={
            tooShort
              ? { type: "min", message: "validation.passwordLength" }
              : errors?.newPassword
                ? { type: "server", message: errors.newPassword[0] }
                : undefined
          }
          errorValues={{ min: PASSWORD_MIN_LENGTH }}
        />
      </div>
      <Button type="submit" variant="outline" disabled={change.isPending || !current || next.length < PASSWORD_MIN_LENGTH} className="self-start">
        {change.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("submit")}
      </Button>
    </form>
  );
}

function SignOutButton() {
  const t = useTranslations("account.session");
  const signOut = useSignOut();
  const router = useRouter();
  return (
    <Button variant="outline" disabled={signOut.isPending} onClick={() => signOut.mutate(undefined, { onSuccess: () => router.push("/") })}>
      <LogOut aria-hidden="true" />
      {t("signOut")}
    </Button>
  );
}

/** C5.8: the request goes to Pinta, who executes it; once asked, the page says so. */
function Deletion({ profile }: { profile: AccountProfile }) {
  const t = useTranslations("account.deletion");
  const locale = useLocale();
  const errorText = useErrorText();
  const request = useRequestDeletion();
  const [confirming, setConfirming] = useState(false);

  if (profile.deletionRequestedAt) {
    return <p className="text-sm">{t("requested", { date: formatDateTime(locale, profile.deletionRequestedAt) })}</p>;
  }

  return (
    <div className="flex flex-col items-start gap-3">
      <p className="text-sm text-muted-foreground">{t("body")}</p>
      <Button variant="outline" className="text-destructive" disabled={request.isPending} onClick={() => setConfirming(true)}>
        {t("button")}
      </Button>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("confirmTitle")}
        description={t("confirmBody")}
        confirmLabel={t("confirm")}
        cancelLabel={t("cancel")}
        onConfirm={() => request.mutate(undefined, { onSuccess: () => toast.success(t("sent")), onError: (e) => toast.error(errorText(e)) })}
      />
    </div>
  );
}
