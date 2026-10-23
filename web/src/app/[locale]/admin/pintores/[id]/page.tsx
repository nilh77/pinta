import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PainterScreen } from "@/components/admin/painters/painter-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.painters.detail" });
  return { title: t("title"), robots: { index: false } };
}

/** Ficha del pintor (A4.1, A4.3, A4.4) (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return <PainterScreen id={id} />;
}
