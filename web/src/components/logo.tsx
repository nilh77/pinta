"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { homeFor, useMe } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

/**
 * The "pinta" wordmark with the yellow dot from the design canvas. It leads to the role's start page with a session
 * and to the landing without one (docs/screens.md, «Cabecera»).
 */
export function Logo({ className }: { className?: string }) {
  const t = useTranslations("common");
  const me = useMe();
  return (
    <Link
      href={homeFor(me.data)}
      aria-label={`${t("brand")} — ${t("home")}`}
      className={cn(
        "inline-flex min-h-11 items-center gap-1.5 rounded-xl font-display text-2xl font-extrabold tracking-tight text-foreground",
        className,
      )}
    >
      <span aria-hidden="true">pinta</span>
      <span aria-hidden="true" className="size-[11px] rounded-full bg-accent" />
    </Link>
  );
}
