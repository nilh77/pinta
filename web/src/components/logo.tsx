import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";

/** The "pinta" wordmark with the yellow dot from the design canvas. */
export function Logo({ className }: { className?: string }) {
  const t = useTranslations("common");
  return (
    <Link
      href="/"
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
