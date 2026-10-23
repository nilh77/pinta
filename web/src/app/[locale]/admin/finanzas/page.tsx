import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { FinanceScreen } from "@/components/admin/finance/finance-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.finance" });
  return { title: t("title"), robots: { index: false } };
}

/** Finanzas (A5.1–A5.4) (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return <FinanceScreen />;
}
