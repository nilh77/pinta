import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PainterJobScreen } from "@/components/painter/painter-job-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "painter.job" });
  return { title: t("title"), robots: { index: false } };
}

/** P2 · Trabajo y ejecución. */
export default async function PainterJobPage({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return <PainterJobScreen id={id} />;
}
