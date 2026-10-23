import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AuthCard } from "@/components/auth/auth-card";
import { PainterOnboarding } from "@/components/painter/painter-onboarding";
import { pageLocale } from "@/i18n/page-locale";

type Props = {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ userId?: string; token?: string }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "painter.onboarding" });
  return { title: t("title"), robots: { index: false } };
}

/** Target of the link in the painter invitation email (A4.2). */
export default async function PainterSignUpPage({ params, searchParams }: Props) {
  await pageLocale(params);
  const t = await getTranslations("painter.onboarding");
  const { userId, token } = await searchParams;

  return (
    <AuthCard title={t("title")}>
      <PainterOnboarding userId={userId} token={token} />
    </AuthCard>
  );
}
