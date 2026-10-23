import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PainterPayoutsScreen } from "@/components/painter/painter-payouts-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "painter.payouts" });
  return { title: t("title"), robots: { index: false } };
}

/** P4 · Mis pagos. */
export default async function PainterPayoutsPage({ params }: Props) {
  await pageLocale(params);
  return <PainterPayoutsScreen />;
}
