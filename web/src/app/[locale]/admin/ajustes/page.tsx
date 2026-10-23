import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { SettingsScreen } from "@/components/admin/settings/settings-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.settings" });
  return { title: t("title"), robots: { index: false } };
}

/** Ajustes (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <SettingsScreen />;
}
