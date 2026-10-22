import type { Metadata } from "next";
import { Suspense } from "react";
import { getTranslations } from "next-intl/server";
import { SchemaStep } from "@/components/quote/schema-step";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "quote.schema" });
  return { title: t("metaTitle") };
}

export default async function Page({ params }: Props) {
  await pageLocale(params);
  // The editor reads `?el=` with useSearchParams, which needs a Suspense boundary on a static page.
  return (
    <Suspense>
      <SchemaStep />
    </Suspense>
  );
}
