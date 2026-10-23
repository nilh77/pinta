"use client";

import { ClipboardCheck, LayoutList, Loader2, MapPinned, PaintRoller, Tags } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, type ReactNode } from "react";
import { LocaleSwitcher } from "@/components/locale-switcher";
import { Logo } from "@/components/logo";
import { Button } from "@/components/ui/button";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { signedOut, useMe, useSignOut } from "@/lib/auth/use-auth";

const SECTIONS = [
  { href: "/admin/trabajos", key: "jobs", icon: LayoutList },
  { href: "/admin/revision", key: "review", icon: ClipboardCheck },
  { href: "/admin/precios", key: "pricing", icon: Tags },
  { href: "/admin/pintores", key: "painters", icon: PaintRoller },
  { href: "/admin/cobertura", key: "coverage", icon: MapPinned },
] as const;

/** Admin area: only for the admin role. Without a session it goes to sign-in and back; other roles see a 403 page. */
export function AdminShell({ children }: { children: ReactNode }) {
  const t = useTranslations("admin");
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();
  const signOut = useSignOut();

  useEffect(() => {
    if (me.data === null) router.replace(signedOut() ? "/" : `/entrar?next=${encodeURIComponent(pathname)}`);
  }, [me.data, pathname, router]);

  if (me.isPending || me.data === null) {
    return (
      <div role="status" className="flex min-h-dvh items-center justify-center gap-2 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
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

  const initials = me.data.name
    .split(/\s+/)
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <div className="flex min-h-dvh flex-col lg:flex-row">
      <aside className="flex flex-col gap-3 border-b border-border bg-card px-4 py-3 lg:sticky lg:top-0 lg:h-dvh lg:w-56 lg:shrink-0 lg:border-r lg:border-b-0 lg:px-3.5 lg:py-5">
        <div className="flex items-center justify-between gap-2 lg:px-1.5">
          <div className="flex items-center gap-2">
            <Logo />
            <span className="rounded-md bg-primary-soft px-1.5 py-0.5 text-[11px] font-bold tracking-wide text-primary">
              {t("badge")}
            </span>
          </div>
          <div className="lg:hidden">
            <LocaleSwitcher />
          </div>
        </div>
        <nav aria-label={t("nav.label")} className="flex gap-1 overflow-x-auto lg:flex-col">
          {SECTIONS.map(({ href, key, icon: Icon }) => {
            const current = pathname === href || pathname.startsWith(`${href}/`);
            return (
              <Link
                key={href}
                href={href}
                aria-current={current ? "page" : undefined}
                className="flex min-h-11 items-center gap-2.5 rounded-xl px-3 text-sm font-semibold text-muted-foreground hover:bg-primary-soft hover:text-foreground aria-[current=page]:bg-primary-soft aria-[current=page]:text-primary"
              >
                <Icon aria-hidden="true" className="size-4" />
                {t(`nav.${key}`)}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto hidden flex-col gap-3 lg:flex">
          <LocaleSwitcher />
          <div className="flex items-center gap-2.5 px-1.5">
            <span aria-hidden="true" className="flex size-9 items-center justify-center rounded-full bg-primary-soft text-sm font-bold text-primary">
              {initials}
            </span>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold">{me.data.name}</div>
              <div className="text-xs text-muted-foreground">{t("role")}</div>
            </div>
          </div>
          <Button
            variant="ghost"
            size="sm"
            disabled={signOut.isPending}
            onClick={() => signOut.mutate(undefined, { onSuccess: () => router.push("/") })}
          >
            {t("signOut")}
          </Button>
        </div>
      </aside>
      <main id="main" className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-7">
        {children}
      </main>
    </div>
  );
}
