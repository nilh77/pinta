import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { pageLocale } from "@/i18n/page-locale";
import { AuthCard } from "@/components/auth/auth-card";
import { ConfirmEmail } from "@/components/auth/confirm-email";

type Props = {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ userId?: string; token?: string }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "confirmEmail" });
  return { title: t("title"), robots: { index: false } };
}

/** Target of the link in the confirmation email (C4.5). */
export default async function ConfirmEmailPage({ params, searchParams }: Props) {
  await pageLocale(params);
  const t = await getTranslations("confirmEmail");
  const { userId, token } = await searchParams;

  return (
    <AuthCard title={t("title")}>
      <ConfirmEmail userId={userId} token={token} />
    </AuthCard>
  );
}
