import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { pageLocale } from "@/i18n/page-locale";
import { AuthCard } from "@/components/auth/auth-card";
import { SignInForm } from "@/components/auth/sign-in-form";

type Props = { params: Promise<{ locale: string }>; searchParams: Promise<{ next?: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "signIn" });
  return { title: t("title") };
}

/** Entrar (S1); `next` returns the user to the page they asked for. */
export default async function SignInPage({ params, searchParams }: Props) {
  await pageLocale(params);
  const t = await getTranslations("signIn");
  const { next } = await searchParams;

  return (
    <AuthCard title={t("title")} subtitle={t("subtitle")}>
      <SignInForm next={next} />
    </AuthCard>
  );
}
