import { ArrowRight, Lock, MapPin, PaintRoller, Search, ShieldCheck } from "lucide-react";
import { getTranslations } from "next-intl/server";
import { CoverageCheck } from "@/components/coverage-check";
import { EmailConfirmationBanner } from "@/components/email-confirmation-banner";
import { HouseIllustration } from "@/components/house-illustration";
import { Logo } from "@/components/logo";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { pageLocale } from "@/i18n/page-locale";

const STEPS = ["describe", "price", "book"] as const;
const PROMISES = [
  { key: "material", icon: PaintRoller, tone: "accent" },
  { key: "painters", icon: ShieldCheck, tone: "primary" },
  { key: "payment", icon: Lock, tone: "primary" },
  { key: "reviewed", icon: Search, tone: "primary" },
] as const;
const WORKS = ["rooms", "homes", "ceilings", "walls", "facades"] as const;

/** Screen 0 · Inicio (board Inicio, desktop D1). Coverage checker and from-price arrive in H3 with C4.1. [C1.1] */
export default async function HomePage({ params }: { params: Promise<{ locale: string }> }) {
  await pageLocale(params);
  const t = await getTranslations("home");

  const quoteButton = (variant: "primary" | "dark" = "primary") => (
    <Button asChild size="lg" className={variant === "dark" ? "w-full bg-foreground hover:bg-foreground/90 sm:w-auto" : "w-full sm:w-auto"}>
      <Link href="/cotizar">
        {t("cta")}
        <ArrowRight aria-hidden="true" />
      </Link>
    </Button>
  );

  return (
    <>
      <SiteHeader>
        <nav aria-label={t("sectionsLabel")} className="hidden items-center gap-1 lg:flex">
          {(["como", "que", "garantias"] as const).map((id) => (
            <a key={id} href={`#${id}`} className="flex h-11 items-center rounded-xl px-3 text-sm font-medium text-muted-foreground hover:text-foreground">
              {t(`nav.${id}`)}
            </a>
          ))}
        </nav>
      </SiteHeader>
      <EmailConfirmationBanner />
      <main id="main" className="mx-auto w-full max-w-6xl px-4 pb-16 sm:px-6">
        <section className="grid gap-10 pt-6 lg:grid-cols-[1.1fr_0.9fr] lg:items-center lg:gap-16 lg:pt-16">
          <div>
            <p className="inline-flex items-center gap-1.5 rounded-[10px] bg-accent-soft px-2.5 py-1.5 text-xs font-semibold text-accent-ink">
              <MapPin aria-hidden="true" className="size-3.5" />
              {t("badge")}
            </p>
            <h1 className="mt-3.5 max-w-[14ch] text-4xl leading-[1.08] font-extrabold tracking-[-0.03em] text-balance sm:text-5xl lg:text-6xl">
              {t("title")}
            </h1>
            <p className="mt-3 max-w-prose text-base leading-relaxed text-muted-foreground lg:text-lg">{t("subtitle")}</p>
            <div className="mt-6 flex flex-col gap-2 sm:flex-row sm:items-center">
              {quoteButton()}
              <Button asChild variant="ghost" size="lg">
                <a href="#como">{t("howLink")}</a>
              </Button>
            </div>
            <div className="max-w-md">
              <CoverageCheck />
            </div>
          </div>
          <div className="mx-auto w-full max-w-sm lg:max-w-md">
            <HouseIllustration />
          </div>
        </section>

        <section id="como" aria-labelledby="como-title" className="scroll-mt-6 pt-16">
          <h2 id="como-title" className="text-2xl font-bold tracking-tight lg:text-3xl">{t("how.title")}</h2>
          <ol className="mt-5 grid gap-4 md:grid-cols-3">
            {STEPS.map((step, i) => (
              <li key={step} className="flex gap-3.5 rounded-2xl md:flex-col md:border md:border-border md:bg-card md:p-5">
                <span
                  aria-hidden="true"
                  className={`flex size-9 shrink-0 items-center justify-center rounded-full font-display text-[15px] font-bold ${i === 2 ? "bg-accent text-accent-foreground" : "bg-primary text-primary-foreground"}`}
                >
                  {i + 1}
                </span>
                <div>
                  <h3 className="text-[15px] font-semibold">{t(`how.${step}.title`)}</h3>
                  <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{t(`how.${step}.body`)}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <section id="garantias" aria-labelledby="garantias-title" className="scroll-mt-6 pt-14">
          <h2 id="garantias-title" className="text-2xl font-bold tracking-tight lg:text-3xl">{t("promises.title")}</h2>
          <p className="mt-2 max-w-prose text-sm text-muted-foreground">{t("promises.subtitle")}</p>
          <ul className="mt-5 grid grid-cols-2 gap-2.5 lg:grid-cols-4">
            {PROMISES.map(({ key, icon: Icon, tone }) => (
              <li key={key} className="rounded-2xl border border-border bg-card p-3.5 lg:p-5">
                <span
                  aria-hidden="true"
                  className={`flex size-9 items-center justify-center rounded-full ${tone === "accent" ? "bg-accent-soft text-accent-ink" : "bg-primary-soft text-primary"}`}
                >
                  <Icon className="size-[18px]" />
                </span>
                <h3 className="mt-2.5 text-sm font-semibold">{t(`promises.${key}.title`)}</h3>
                <p className="mt-0.5 text-xs leading-snug text-muted-foreground">{t(`promises.${key}.body`)}</p>
              </li>
            ))}
          </ul>
        </section>

        <section id="que" aria-labelledby="que-title" className="scroll-mt-6 pt-14">
          <h2 id="que-title" className="text-2xl font-bold tracking-tight lg:text-3xl">{t("works.title")}</h2>
          <ul className="mt-4 flex flex-wrap gap-2">
            {WORKS.map((work) => (
              <li key={work} className="rounded-xl border border-border bg-card px-3.5 py-2.5 text-sm font-medium">
                {t(`works.${work}`)}
              </li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="cta-title" className="mt-14 rounded-3xl bg-accent px-5 py-6 lg:flex lg:items-center lg:justify-between lg:gap-8 lg:px-10 lg:py-10">
          <div>
            <h2 id="cta-title" className="text-2xl leading-tight font-extrabold tracking-tight text-accent-foreground lg:text-3xl">
              {t("ctaBand.title")}
            </h2>
            <p className="mt-2 text-sm text-[#3d3000]">{t("ctaBand.body")}</p>
          </div>
          <div className="mt-4 lg:mt-0 lg:shrink-0">{quoteButton("dark")}</div>
        </section>
      </main>
      <footer className="border-t border-border">
        <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-8 sm:px-6">
          <Logo />
          <p className="text-[13px] text-muted-foreground">{t("footer")}</p>
        </div>
      </footer>
    </>
  );
}
