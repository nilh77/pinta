import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { MyJobsScreen } from "@/components/jobs/my-jobs-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "myJobs" });
  return { title: t("title"), robots: { index: false } };
}

/** Screen 9 · Mis trabajos. */
export default async function MyJobsPage({ params }: Props) {
  await pageLocale(params);
  return <MyJobsScreen />;
}
