"use client";

import { Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, type ReactNode } from "react";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { signedOut, useMe } from "@/lib/auth/use-auth";

/** Client area (screens 9–10): sign-in first and back; other roles see where to go instead. */
export function ClientArea({ children }: { children: ReactNode }) {
  const t = useTranslations("myJobs");
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (me.data === null) router.replace(signedOut() ? "/" : `/entrar?next=${encodeURIComponent(pathname)}`);
  }, [me.data, pathname, router]);

  return (
    <div className="flex min-h-dvh flex-col">
      <SiteHeader />
      <main id="main" className="mx-auto w-full max-w-3xl flex-1 px-4 pt-2 pb-16 sm:px-6 lg:max-w-5xl">
        {me.isPending || me.data === null ? (
          <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
        ) : me.data?.role !== "client" ? (
          <div className="flex flex-col items-start gap-4 py-16">
            <p className="text-muted-foreground">{t("notClient")}</p>
            <Button asChild>
              <Link href={me.data?.role === "admin" ? "/admin/revision" : me.data?.role === "painter" ? "/pintor" : "/"}>{t("goHome")}</Link>
            </Button>
          </div>
        ) : (
          children
        )}
      </main>
    </div>
  );
}
