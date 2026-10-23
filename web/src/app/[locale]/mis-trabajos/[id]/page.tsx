import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { MyJobScreen } from "@/components/jobs/my-job-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "myJobs.detail" });
  return { title: t("title"), robots: { index: false } };
}

/** Screen 10 · Detalle del trabajo. */
export default async function MyJobPage({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return <MyJobScreen id={id} />;
}
