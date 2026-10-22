import { MapPin } from "lucide-react";
import { getTranslations } from "next-intl/server";
import { EmailConfirmationBanner } from "@/components/email-confirmation-banner";
import { HomeActions } from "@/components/home-actions";
import { HouseIllustration } from "@/components/house-illustration";
import { SiteHeader } from "@/components/site-header";
import { pageLocale } from "@/i18n/page-locale";

/** Screen 0 · Inicio, minimal for H0: hero with the palette; the quote CTA arrives in H2. */
export default async function HomePage({ params }: { params: Promise<{ locale: string }> }) {
  await pageLocale(params);
  const t = await getTranslations("home");

  return (
    <>
      <SiteHeader />
      <EmailConfirmationBanner />
      <main id="main" className="mx-auto grid w-full max-w-6xl gap-10 px-4 pt-6 pb-20 sm:px-6 lg:grid-cols-[1.1fr_0.9fr] lg:items-center lg:gap-16 lg:pt-16">
        <div>
          <p className="inline-flex items-center gap-1.5 rounded-[10px] bg-accent-soft px-2.5 py-1.5 text-xs font-semibold text-accent-ink">
            <MapPin aria-hidden="true" className="size-3.5" />
            {t("badge")}
          </p>
          <h1 className="mt-3.5 max-w-[14ch] text-4xl leading-[1.08] font-extrabold tracking-[-0.03em] text-balance sm:text-5xl lg:text-6xl">
            {t("title")}
          </h1>
          <p className="mt-3 max-w-prose text-base leading-relaxed text-muted-foreground lg:text-lg">{t("subtitle")}</p>
          <HomeActions />
        </div>
        <div className="mx-auto w-full max-w-sm lg:max-w-md">
          <HouseIllustration />
        </div>
      </main>
    </>
  );
}
