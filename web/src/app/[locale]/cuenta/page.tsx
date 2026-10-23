import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AccountScreen } from "@/components/account/account-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "account" });
  return { title: t("title"), robots: { index: false } };
}

/** Screen 12 · Cuenta del cliente. */
export default async function AccountPage({ params }: Props) {
  await pageLocale(params);
  return <AccountScreen />;
}
