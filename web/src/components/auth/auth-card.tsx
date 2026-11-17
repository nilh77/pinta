import { BadgeCheck, Calculator, PaintRoller } from "lucide-react";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { HouseIllustration } from "@/components/house-illustration";
import { SiteHeader } from "@/components/site-header";

const PROMISES = [
  ["price", Calculator],
  ["painters", BadgeCheck],
  ["material", PaintRoller],
] as const;

/**
 * Shared frame of sign-in, register, recovery and the painter's invitation (docs/screens.md, «Acceso»): on desktop a
 * brand panel with the house and what Pinta offers next to the form card; on phones only the card, so the form is
 * the first thing on screen.
 */
export function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  const t = useTranslations("authPanel");

  return (
    <>
      <SiteHeader />
      <main
        id="main"
        className="mx-auto grid w-full max-w-5xl items-center gap-6 px-4 pt-2 pb-16 sm:px-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,440px)] lg:gap-12 lg:pt-10"
      >
        <aside className="hidden flex-col gap-8 rounded-[28px] bg-primary-soft p-10 lg:flex">
          <p className="font-display text-[32px] leading-tight font-extrabold tracking-tight text-balance">{t("title")}</p>
          <div className="max-w-sm">
            <HouseIllustration />
          </div>
          <ul className="mt-4 flex flex-col gap-3">
            {PROMISES.map(([key, Icon]) => (
              <li key={key} className="flex items-center gap-3 text-[15px] font-medium">
                <span className="grid size-9 shrink-0 place-items-center rounded-full bg-card text-primary">
                  <Icon aria-hidden="true" className="size-[18px]" />
                </span>
                {t(key)}
              </li>
            ))}
          </ul>
        </aside>

        <section className="w-full rounded-[28px] border border-border bg-card p-6 shadow-[0_1px_2px_rgb(16_32_29/0.04),0_12px_32px_-12px_rgb(16_32_29/0.12)] sm:p-8">
          <h1 className="text-[26px] leading-tight font-bold tracking-tight">{title}</h1>
          {subtitle && <p className="mt-1.5 text-sm text-muted-foreground">{subtitle}</p>}
          <div className="mt-6">{children}</div>
        </section>
      </main>
    </>
  );
}
