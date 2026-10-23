import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PainterJobsScreen } from "@/components/painter/painter-jobs-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "painter.jobs" });
  return { title: t("title"), robots: { index: false } };
}

/** P1 · Mis trabajos del pintor. */
export default async function PainterJobsPage({ params }: Props) {
  await pageLocale(params);
  return <PainterJobsScreen />;
}
