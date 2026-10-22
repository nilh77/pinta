import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { pageLocale } from "@/i18n/page-locale";
import { AuthCard } from "@/components/auth/auth-card";
import { ResetPasswordForm } from "@/components/auth/reset-password-form";

type Props = {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ userId?: string; token?: string }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "resetPassword" });
  return { title: t("title"), robots: { index: false } };
}

/** Target of the link in the reset email (S1). */
export default async function ResetPasswordPage({ params, searchParams }: Props) {
  await pageLocale(params);
  const t = await getTranslations("resetPassword");
  const { userId, token } = await searchParams;

  return (
    <AuthCard title={t("title")}>
      <ResetPasswordForm userId={userId} token={token} />
    </AuthCard>
  );
}
