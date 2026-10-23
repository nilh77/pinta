"use client";

import { useLocale, useTranslations } from "next-intl";
import type { Schemas } from "@/lib/jobs/api";
import { formatDate } from "@/lib/format";
import { cn } from "@/lib/utils";

type Light = NonNullable<Schemas["ReviewLight"]>;

const CLASSES: Record<Light, string> = {
  green: "bg-primary-soft text-[#0b5f59] before:bg-primary",
  amber: "bg-accent-soft text-accent-ink before:bg-accent",
  red: "bg-destructive/10 text-destructive before:bg-destructive",
};

/** A2.1 traffic light: green with more than 24 working hours left, amber with less, red when overdue. */
export function ReviewLightBadge({ light, dueOn }: { light: Light; dueOn: string | null }) {
  const t = useTranslations("admin.review.light");
  const locale = useLocale();
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap before:size-2 before:rounded-full before:content-['']",
        CLASSES[light],
      )}
    >
      {t(light)}
      {dueOn && ` · ${formatDate(locale, dueOn, true)}`}
    </span>
  );
}
