import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PainterVisitScreen } from "@/components/painter/painter-visit-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "painter.visit" });
  return { title: t("title"), robots: { index: false } };
}

/** P3 · Visita técnica. */
export default async function PainterVisitPage({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return <PainterVisitScreen id={id} />;
}
