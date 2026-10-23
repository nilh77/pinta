import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { NewPainterScreen } from "@/components/admin/painters/new-painter-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.painters.new" });
  return { title: t("title"), robots: { index: false } };
}

/** Nuevo pintor (A4.2) (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <NewPainterScreen />;
}
