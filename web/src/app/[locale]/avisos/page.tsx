import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { NotificationsScreen } from "@/components/notifications/notifications-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "notifications" });
  return { title: t("title"), robots: { index: false } };
}

/** «Avisos»: the full list of the bell (docs/screens.md). */
export default async function NotificationsPage({ params }: Props) {
  await pageLocale(params);
  return <NotificationsScreen />;
}
