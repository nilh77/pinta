import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { SentStep } from "@/components/quote/sent-step";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; code: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "quote.sent" });
  return { title: t("metaTitle"), robots: { index: false } };
}

/** Screen 8 · Confirmación: /{locale}/cotizar/enviado/{code}. */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  const { code } = await params;
  return <SentStep code={decodeURIComponent(code)} />;
}
