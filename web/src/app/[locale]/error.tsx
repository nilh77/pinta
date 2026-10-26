"use client";

import { useTranslations } from "next-intl";
import { StatusPage } from "@/components/status-page";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";

/** 500 in the page language: try again or go home (docs/screens.md, global states). */
export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const t = useTranslations("globalStates.error");
  return (
    <StatusPage code="500" title={t("title")} body={t("body")}>
      <Button size="lg" onClick={reset}>
        {t("retry")}
      </Button>
      <Button asChild size="lg" variant="outline">
        <Link href="/">{t("home")}</Link>
      </Button>
    </StatusPage>
  );
}
