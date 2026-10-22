import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PricingScreen } from "@/components/admin/pricing/pricing-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.pricing" });
  return { title: t("title"), robots: { index: false } };
}

/** A1 · Precios y simulador (docs/screens.md). */
export default async function PricingPage({ params }: Props) {
  await pageLocale(params);
  return <PricingScreen />;
}
