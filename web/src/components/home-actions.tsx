"use client";

import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { useMe } from "@/lib/auth/use-auth";

export function HomeActions() {
  const t = useTranslations("home");
  const me = useMe();

  return (
    <div className="mt-6 flex flex-col gap-3 sm:max-w-sm">
      {me.isPending ? (
        <span aria-hidden="true" className="h-[52px] animate-pulse motion-reduce:animate-none rounded-[14px] bg-primary-soft" />
      ) : me.data ? (
        <p className="text-sm text-muted-foreground">{t("signedIn", { email: me.data.email })}</p>
      ) : (
        <>
          <Button asChild size="lg" className="w-full">
            <Link href="/registro">{t("createAccount")}</Link>
          </Button>
          <p className="text-sm text-muted-foreground">{t("comingSoon")}</p>
        </>
      )}
    </div>
  );
}
