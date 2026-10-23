import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Suspense } from "react";
import { CoverageScreen } from "@/components/admin/coverage-areas/coverage-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.coverage" });
  return { title: t("title"), robots: { index: false } };
}

/** Cobertura (A1.7) (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return (
    <Suspense>
      <CoverageScreen />
    </Suspense>
  );
}
