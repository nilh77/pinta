import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AddressStep } from "@/components/quote/address-step";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "quote.address" });
  return { title: t("metaTitle") };
}

export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <AddressStep />;
}
