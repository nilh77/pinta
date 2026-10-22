"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { cn } from "@/lib/utils";

export function LocaleSwitcher() {
  const t = useTranslations("common");
  const locale = useLocale();
  const pathname = usePathname();

  return (
    <nav aria-label={t("language")}>
      <ul className="flex items-center rounded-xl border border-border bg-card p-0.5">
        {routing.locales.map((option) => (
          <li key={option}>
            <Link
              href={pathname}
              locale={option}
              lang={option}
              hrefLang={option}
              aria-label={t(`languages.${option}`)}
              aria-current={option === locale ? "true" : undefined}
              className={cn(
                "flex size-11 items-center justify-center rounded-[10px] text-[13px] font-semibold uppercase",
                option === locale ? "bg-primary-soft text-primary" : "text-muted-foreground hover:text-foreground",
              )}
            >
              {option}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
