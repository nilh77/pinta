import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AdminJobView } from "@/components/admin/jobs/admin-job-view";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "admin.job" });
  return { title: t("title"), robots: { index: false } };
}

/** Job detail with tabs (A2.2, A3.2–A3.5). */
export default async function AdminJobPage({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return <AdminJobView id={id} layout="tabs" />;
}
