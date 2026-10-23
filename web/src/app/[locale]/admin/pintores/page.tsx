import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PaintersScreen } from "@/components/admin/painters/painters-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.painters" });
  return { title: t("title"), robots: { index: false } };
}

/** Pintores (A4.1) (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <PaintersScreen />;
}
