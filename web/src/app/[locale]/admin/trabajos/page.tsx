import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Suspense } from "react";
import { JobsBoard } from "@/components/admin/jobs/jobs-board";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.jobs" });
  return { title: t("title"), robots: { index: false } };
}

/** A3.1 · Tablero de trabajos (docs/screens.md). */
export default async function Page({ params }: Props) {
  await pageLocale(params);
  return (
    <Suspense>
      <JobsBoard />
    </Suspense>
  );
}
