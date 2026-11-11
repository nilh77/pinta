"use client";

import { ClipboardCheck, LayoutList, MapPinned, PaintRoller, Settings, Tags, Wallet } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, type ReactNode } from "react";
import { AppHeader } from "@/components/app-header";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { signedOut, useMe } from "@/lib/auth/use-auth";

const SECTIONS = [
  { href: "/admin/trabajos", key: "jobs", icon: LayoutList },
  { href: "/admin/revision", key: "review", icon: ClipboardCheck },
  { href: "/admin/precios", key: "pricing", icon: Tags },
  { href: "/admin/pintores", key: "painters", icon: PaintRoller },
  { href: "/admin/cobertura", key: "coverage", icon: MapPinned },
  { href: "/admin/finanzas", key: "finance", icon: Wallet },
  { href: "/admin/ajustes", key: "settings", icon: Settings },
] as const;

/** Admin area: only for the admin role. Without a session it goes to sign-in and back; other roles see a 403 page. */
export function AdminShell({ children }: { children: ReactNode }) {
  const t = useTranslations("admin");
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (me.data === null) router.replace(signedOut() ? "/" : `/entrar?next=${encodeURIComponent(pathname)}`);
  }, [me.data, pathname, router]);

  if (me.isPending || me.data === null) {
    return (
      <div role="status" className="flex min-h-dvh flex-col lg:flex-row">
        <span className="sr-only">{t("loading")}</span>
        <div className="flex flex-col gap-3 border-b border-border bg-card px-4 py-3 lg:h-dvh lg:w-56 lg:shrink-0 lg:border-r lg:border-b-0 lg:px-3.5 lg:py-5">
          <Skeleton className="h-8 w-32" />
          <div className="flex gap-1 overflow-hidden lg:flex-col">
            {Array.from({ length: 5 }, (_, i) => (
              <Skeleton key={i} className="h-11 w-28 shrink-0 lg:w-full" />
            ))}
          </div>
        </div>
        <div className="flex min-w-0 flex-1 flex-col gap-4 px-4 py-6 sm:px-6 lg:px-8 lg:py-7">
          <Skeleton className="h-9 w-56" />
          {Array.from({ length: 3 }, (_, i) => (
            <Skeleton key={i} className="h-28 rounded-[20px]" />
          ))}
        </div>
      </div>
    );
  }

  if (me.isError || me.data.role !== "admin") {
    return (
      <main id="main" className="mx-auto flex min-h-dvh max-w-md flex-col items-center justify-center gap-4 px-4 text-center">
        <h1 className="text-2xl font-bold">{t("forbidden.title")}</h1>
        <p className="text-muted-foreground">{t("forbidden.body")}</p>
        <Button asChild>
          <Link href="/">{t("forbidden.home")}</Link>
        </Button>
      </main>
    );
  }

  const nav = (className: string) => (
    <nav aria-label={t("nav.label")} className={className}>
      {SECTIONS.map(({ href, key, icon: Icon }) => {
        const current = pathname === href || pathname.startsWith(`${href}/`);
        return (
          <Link
            key={href}
            href={href}
            aria-current={current ? "page" : undefined}
            className="flex min-h-11 shrink-0 items-center gap-2.5 rounded-xl px-3 text-sm font-semibold text-muted-foreground hover:bg-primary-soft hover:text-foreground aria-[current=page]:bg-primary-soft aria-[current=page]:text-primary"
          >
            <Icon aria-hidden="true" className="size-4" />
            {t(`nav.${key}`)}
          </Link>
        );
      })}
    </nav>
  );
  const brand = (
    <div className="flex items-center gap-2">
      <Logo className="min-h-0 text-xl" />
      <span className="rounded-md bg-primary-soft px-1.5 py-0.5 text-[11px] font-bold tracking-wide text-primary">{t("badge")}</span>
    </div>
  );

  // Language and account live in the shared header; the sections stay in a floating glass sidebar on desktop and
  // in a scrolling row under the header on phones (docs/screens.md, «Cabecera»).
  return (
    <div className="flex min-h-dvh flex-col lg:flex-row">
      <aside className="hidden lg:sticky lg:top-4 lg:m-4 lg:mr-0 lg:flex lg:h-[calc(100dvh-2rem)] lg:w-56 lg:shrink-0 lg:flex-col lg:gap-4 lg:rounded-3xl lg:glass lg:bg-glass-chrome lg:px-3.5 lg:py-5 lg:shadow-chrome">
        <div className="px-1.5">{brand}</div>
        {nav("flex flex-col gap-1")}
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <AppHeader leading={brand} leadingClassName="lg:hidden" />
        {nav("flex gap-1 overflow-x-auto px-4 pb-2 sm:px-6 lg:hidden")}
        <main id="main" className="min-w-0 flex-1 px-4 pt-2 pb-6 sm:px-6 lg:px-8 lg:pb-7">
          {children}
        </main>
      </div>
    </div>
  );
}
