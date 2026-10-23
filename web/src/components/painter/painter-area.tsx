"use client";

import { Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, type ReactNode } from "react";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { signedOut, useMe } from "@/lib/auth/use-auth";
import { homeFor } from "@/lib/painter/use-painter";

/** Painter area (P1, P2): sign-in first and back; other roles see where to go instead. */
export function PainterArea({ children }: { children: ReactNode }) {
  const t = useTranslations("painter");
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (me.data === null) router.replace(signedOut() ? "/" : `/entrar?next=${encodeURIComponent(pathname)}`);
  }, [me.data, pathname, router]);

  return (
    <div className="flex min-h-dvh flex-col">
      <SiteHeader />
      <main id="main" className="mx-auto w-full max-w-3xl flex-1 px-4 pt-2 pb-16 sm:px-6">
        {me.isPending || me.data === null ? (
          <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
        ) : me.data?.role !== "painter" ? (
          <div className="flex flex-col items-start gap-4 py-16">
            <p className="text-muted-foreground">{t("notPainter")}</p>
            <Button asChild>
              <Link href={me.data?.role === "client" ? "/mis-trabajos" : homeFor(me.data?.role)}>{t("goHome")}</Link>
            </Button>
          </div>
        ) : (
          <>
            <nav aria-label={t("nav.label")} className="mb-4 flex gap-1 border-b border-border">
              {(
                [
                  ["/pintor", "jobs"],
                  ["/pintor/pagos", "payouts"],
                ] as const
              ).map(([href, key]) => {
                const current = key === "payouts" ? pathname.startsWith("/pintor/pagos") : !pathname.startsWith("/pintor/pagos");
                return (
                  <Link
                    key={href}
                    href={href}
                    aria-current={current ? "page" : undefined}
                    className="-mb-px inline-flex min-h-11 items-center border-b-2 border-transparent px-3 text-sm font-semibold text-muted-foreground aria-[current=page]:border-primary aria-[current=page]:text-primary"
                  >
                    {t(`nav.${key}`)}
                  </Link>
                );
              })}
            </nav>
            {children}
          </>
        )}
      </main>
    </div>
  );
}
