import { useTranslations } from "next-intl";
import { StatusPage } from "@/components/status-page";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";

/** 404 in the page language, with a link home (docs/screens.md, global states). */
export default function NotFound() {
  const t = useTranslations("globalStates.notFound");
  return (
    <StatusPage code="404" title={t("title")} body={t("body")}>
      <Button asChild size="lg">
        <Link href="/">{t("home")}</Link>
      </Button>
    </StatusPage>
  );
}
