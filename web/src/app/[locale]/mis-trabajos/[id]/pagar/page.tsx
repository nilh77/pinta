import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Suspense } from "react";
import { PaymentScreen } from "@/components/payments/payment-screen";
import { pageLocale } from "@/i18n/page-locale";

type Props = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "payment" });
  return { title: t("pageTitle"), robots: { index: false } };
}

/** Screen 11 · Pago. */
export default async function PaymentPage({ params }: Props) {
  await pageLocale(params);
  const { id } = await params;
  return (
    <Suspense>
      <PaymentScreen id={id} />
    </Suspense>
  );
}
