"use client";

import { Briefcase } from "lucide-react";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { useMe, useSignOut } from "@/lib/auth/use-auth";
import { LocaleSwitcher } from "./locale-switcher";
import { Logo } from "./logo";

/** `children`: page-specific links shown before the language switcher on wide screens (landing D1). */
export function SiteHeader({ children }: { children?: ReactNode }) {
  const t = useTranslations("nav");
  const me = useMe();
  const signOut = useSignOut();
  const router = useRouter();

  return (
    <header className="mx-auto flex w-full max-w-6xl items-center justify-between gap-3 px-4 py-3 sm:px-6">
      <Logo />
      <div className="flex items-center gap-2">
        {children}
        <LocaleSwitcher />
        {me.isPending ? (
          <span aria-hidden="true" className="h-11 w-20 animate-pulse motion-reduce:animate-none rounded-xl bg-primary-soft" />
        ) : me.data ? (
          <div className="flex items-center gap-2">
            <span className="hidden text-sm font-medium sm:inline">{t("greeting", { name: me.data.name })}</span>
            {me.data.role !== "painter" && (
              <Button asChild variant="outline" size="sm" className="max-sm:size-11 max-sm:px-0">
                <Link href={me.data.role === "admin" ? "/admin/revision" : "/mis-trabajos"}>
                  <Briefcase aria-hidden="true" className="sm:hidden" />
                  <span className="max-sm:sr-only">{t(me.data.role === "admin" ? "admin" : "myJobs")}</span>
                </Link>
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              disabled={signOut.isPending}
              onClick={() => signOut.mutate(undefined, { onSuccess: () => router.push("/") })}
            >
              {t("signOut")}
            </Button>
          </div>
        ) : (
          <Button asChild variant="ghost" size="sm">
            <Link href="/entrar">{t("signIn")}</Link>
          </Button>
        )}
      </div>
    </header>
  );
}
