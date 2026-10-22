"use client";

import { ChevronLeft, RefreshCw } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useState, type ReactNode } from "react";
import { LocaleSwitcher } from "@/components/locale-switcher";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import type { CatalogInfo, Draft } from "@/lib/quote/draft";
import { useCatalog, type PublicCatalog } from "@/lib/quote/use-quote-data";
import { useQuoteDraft } from "@/lib/quote/use-quote-draft";
import { cn } from "@/lib/utils";

const STEPS = ["job", "price", "details", "send"] as const;

/** The 4 steps of the request (boards Tipo, Main, Estimacion). */
export function QuoteProgress({ current }: { current: 1 | 2 | 3 | 4 }) {
  const t = useTranslations("quote.progress");
  return (
    <ol aria-label={t("label")} className="grid grid-cols-4 gap-1.5">
      {STEPS.map((step, i) => {
        const state = i + 1 < current ? "done" : i + 1 === current ? "current" : "todo";
        return (
          <li key={step} aria-current={state === "current" ? "step" : undefined} className="flex flex-col gap-1.5">
            <span aria-hidden="true" className={cn("h-1 rounded-full", state === "todo" ? "bg-border" : "bg-primary")} />
            <span className={cn("truncate text-[11px]", state === "current" ? "font-semibold text-primary" : "text-muted-foreground")}>
              {i + 1} · {t(step)}
              {state === "done" && <span className="sr-only"> ({t("done")})</span>}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

export type QuoteContext = {
  catalog: PublicCatalog;
  info: CatalogInfo;
  draft: Draft | null;
  update: ReturnType<typeof useQuoteDraft>["update"];
  reset: () => void;
};

type ShellProps = {
  step: 1 | 2 | 3 | 4;
  back: { href: string; label: string };
  /** Wide layouts (desktop builder D2) opt out of the centred column. */
  wide?: boolean;
  children: (context: QuoteContext) => ReactNode;
};

/** Header, progress and the shared loading / error states of the builder; renders `children` once data is ready. */
export function QuoteShell({ step, back, wide, children }: ShellProps) {
  const t = useTranslations("quote");
  const catalog = useCatalog();
  const { draft, hydrated, update, reset } = useQuoteDraft();
  const ready = catalog.data && catalog.info && hydrated;
  const slow = useSlow(!ready && !catalog.isError);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className={cn("mx-auto w-full px-4 pt-3 sm:px-6", wide ? "max-w-[1400px]" : "max-w-2xl")}>
        <div className="flex items-center justify-between gap-2">
          <Link
            href={back.href}
            aria-label={back.label}
            className="flex size-11 items-center justify-center rounded-xl text-foreground hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <ChevronLeft aria-hidden="true" className="size-6" />
          </Link>
          <Logo className="text-[22px]" />
          <LocaleSwitcher />
        </div>
        <div className="mt-2 lg:mx-auto lg:max-w-xl">
          <QuoteProgress current={step} />
        </div>
      </header>
      <main id="main" className={cn("mx-auto flex w-full flex-1 flex-col px-4 pt-5 sm:px-6", wide ? "max-w-[1400px]" : "max-w-2xl")}>
        {ready ? (
          children({ catalog: catalog.data!, info: catalog.info!, draft, update, reset })
        ) : catalog.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3 rounded-2xl border border-border bg-card p-6">
            <h1 className="text-xl font-bold">{t("states.errorTitle")}</h1>
            <p className="text-sm text-muted-foreground">{t("states.errorBody")}</p>
            <Button onClick={() => catalog.refetch()}>
              <RefreshCw aria-hidden="true" />
              {t("states.retry")}
            </Button>
          </div>
        ) : (
          <div role="status" className="flex flex-col gap-3">
            <span className="sr-only">{t("states.loading")}</span>
            <div className="h-8 w-2/3 animate-pulse rounded-xl bg-primary-soft motion-reduce:animate-none" />
            <div className="h-4 w-full animate-pulse rounded-lg bg-primary-soft motion-reduce:animate-none" />
            <div className="mt-3 grid grid-cols-2 gap-2">
              {[0, 1, 2, 3].map((i) => (
                <div key={i} className={cn("h-24 animate-pulse rounded-2xl bg-primary-soft motion-reduce:animate-none", i === 0 && "col-span-2")} />
              ))}
            </div>
            {slow && (
              <div className="mt-2 flex items-center gap-3 text-sm text-muted-foreground">
                {t("states.slow")}
                <Button variant="outline" size="sm" onClick={() => catalog.refetch()}>
                  {t("states.retry")}
                </Button>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
}

/** "Está tardando más de lo normal" after 10 s (docs/screens.md, common rules). */
function useSlow(waiting: boolean) {
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    if (!waiting) return;
    const timer = setTimeout(() => setSlow(true), 10_000);
    return () => {
      clearTimeout(timer);
      setSlow(false);
    };
  }, [waiting]);
  return slow;
}
