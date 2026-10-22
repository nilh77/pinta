import type { Metadata, Viewport } from "next";
import { Inter, Plus_Jakarta_Sans } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getTranslations } from "next-intl/server";
import type { ReactNode } from "react";
import { Providers } from "@/components/providers";
import { pageLocale } from "@/i18n/page-locale";
import { routing } from "@/i18n/routing";
import "../globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" });
const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  weight: ["600", "700", "800"],
  variable: "--font-jakarta",
  display: "swap",
});

type Props = { children: ReactNode; params: Promise<{ locale: string }> };

export const viewport: Viewport = { themeColor: "#f5faf8" };

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export async function generateMetadata({ params }: Omit<Props, "children">): Promise<Metadata> {
  const t = await getTranslations({ locale: await pageLocale(params), namespace: "metadata" });
  return {
    title: { default: t("title"), template: "%s · Pinta" },
    description: t("description"),
    alternates: { languages: Object.fromEntries(routing.locales.map((l) => [l, `/${l}`])) },
  };
}

export default async function LocaleLayout({ children, params }: Props) {
  const locale = await pageLocale(params);
  const t = await getTranslations({ locale, namespace: "common" });

  return (
    <html lang={locale} className={`${inter.variable} ${jakarta.variable}`}>
      <body className="min-h-dvh">
        <a
          href="#main"
          className="sr-only rounded-xl bg-primary px-4 py-3 font-semibold text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50"
        >
          {t("skipToContent")}
        </a>
        <NextIntlClientProvider>
          <Providers locale={locale}>{children}</Providers>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
