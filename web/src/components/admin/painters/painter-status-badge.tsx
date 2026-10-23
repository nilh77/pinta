"use client";

import { useTranslations } from "next-intl";
import type { Schemas } from "@/lib/jobs/api";
import { cn } from "@/lib/utils";

const TONES: Record<Schemas["PainterStatus"], string> = {
  invited: "bg-accent-soft text-accent-ink",
  active: "bg-primary-soft text-primary",
  inactive: "bg-muted text-muted-foreground",
};

export function PainterStatusBadge({ status }: { status: Schemas["PainterStatus"] }) {
  const t = useTranslations("admin.painters.status");
  return <span className={cn("inline-flex rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap", TONES[status])}>{t(status)}</span>;
}
