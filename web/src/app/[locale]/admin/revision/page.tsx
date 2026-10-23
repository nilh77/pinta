import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Suspense } from "react";
import { ReviewScreen } from "@/components/admin/review/review-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.review" });
  return { title: t("title"), robots: { index: false } };
}

/** A2 · Revisión de solicitudes (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return (
    <Suspense>
      <ReviewScreen />
    </Suspense>
  );
}
