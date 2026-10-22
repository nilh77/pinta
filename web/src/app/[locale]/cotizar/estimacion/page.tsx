import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { EstimateStep } from "@/components/quote/estimate-step";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "quote.estimate" });
  return { title: t("metaTitle") };
}

export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <EstimateStep />;
}
