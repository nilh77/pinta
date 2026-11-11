"use client";

import { Check, UserRound } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { DropdownMenu } from "radix-ui";
import type { ReactNode } from "react";
import { MorphMenuContent, morphMenuItemClass } from "@/components/ui/dropdown-menu";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { useMe, useSignOut } from "@/lib/auth/use-auth";
import { cn } from "@/lib/utils";

/** A round 40 px glass control of the header cluster. */
const controlClass =
  "flex size-10 shrink-0 items-center justify-center rounded-full glass bg-glass-control text-[13px] font-semibold text-foreground shadow-chrome outline-none hover:bg-card focus-visible:ring-[3px] focus-visible:ring-ring/50";

/** First row of a header panel: clear of the top-right corner, where the open control stays visible. */
const panelHeadClass = "flex min-h-10 flex-col justify-center px-3 pr-12";

/**
 * The shared header (docs/screens.md, «Cabecera»): no bar, two floating glass pieces. On the left a pill with
 * `leading` (the logo, plus «back» or section links); on the right the language and account controls, whose panels
 * grow out of them (docs/motion.md). `plainLeading` drops the pill: the landing keeps its logo and section links as
 * they were, and its header scrolls away as before (without the pill the links would sit on top of the content).
 */
export function AppHeader({
  leading,
  leadingClassName,
  plainLeading = false,
}: {
  leading: ReactNode;
  leadingClassName?: string;
  plainLeading?: boolean;
}) {
  return (
    <header
      className={cn(
        "morph-header pointer-events-none z-20 flex items-center gap-2",
        // The landing header sits in the page column as before (max-w-6xl, px-4/6, py-3); the others float at 16 px.
        plainLeading ? "relative mx-auto w-full max-w-6xl px-4 py-3 sm:px-6" : "sticky top-0 p-4",
      )}
    >
      <div
        className={cn(
          "pointer-events-auto flex min-w-0 items-center gap-1",
          !plainLeading && "h-10 rounded-full glass bg-glass-chrome px-4 shadow-chrome",
          leadingClassName,
        )}
      >
        {leading}
      </div>
      <div data-morph-cluster className="pointer-events-auto ml-auto flex items-center gap-2">
        <LanguageMenu />
        <AccountMenu />
      </div>
    </header>
  );
}

function LanguageMenu() {
  const t = useTranslations("common");
  const locale = useLocale();
  const pathname = usePathname();
  const code = locale.toUpperCase();

  return (
    <DropdownMenu.Root modal={false}>
      {/* The name starts with the visible code, so voice control users can say what they see (WCAG 2.5.3). */}
      <DropdownMenu.Trigger data-morph-trigger aria-label={t("changeLanguage", { code })} className={controlClass}>
        {code}
      </DropdownMenu.Trigger>
      <MorphMenuContent>
        <DropdownMenu.Label className={cn(panelHeadClass, "text-xs font-semibold text-muted-foreground")}>{t("language")}</DropdownMenu.Label>
        {routing.locales.map((option) => (
          <DropdownMenu.Item key={option} asChild className={morphMenuItemClass}>
            <Link href={pathname} locale={option} lang={option} hrefLang={option} aria-current={option === locale ? "true" : undefined}>
              <span className="w-7 text-xs font-semibold text-muted-foreground uppercase">{option}</span>
              <span className="flex-1">{t(`languages.${option}`)}</span>
              {option === locale && <Check aria-hidden="true" className="size-4 text-primary" />}
            </Link>
          </DropdownMenu.Item>
        ))}
      </MorphMenuContent>
    </DropdownMenu.Root>
  );
}

const ROLE_LINKS = {
  client: [
    ["/mis-trabajos", "myJobs"],
    ["/cuenta", "account"],
  ],
  painter: [
    ["/pintor", "painterJobs"],
    ["/pintor/pagos", "payouts"],
  ],
  admin: [["/admin/revision", "admin"]],
} as const;

function AccountMenu() {
  const t = useTranslations("nav");
  const me = useMe();
  const signOut = useSignOut();
  const router = useRouter();

  if (me.isPending) {
    return <span aria-hidden="true" className="size-10 shrink-0 animate-pulse rounded-full bg-primary-soft motion-reduce:animate-none" />;
  }
  if (!me.data) {
    return (
      <Link href="/entrar" aria-label={t("signIn")} className={controlClass}>
        <UserRound aria-hidden="true" className="size-4" />
      </Link>
    );
  }

  const { name, email, role } = me.data;
  const initials = name
    .split(/\s+/)
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <DropdownMenu.Root modal={false}>
      <DropdownMenu.Trigger data-morph-trigger aria-label={t("accountMenu", { name })} className={controlClass}>
        {initials}
      </DropdownMenu.Trigger>
      <MorphMenuContent>
        <div className={panelHeadClass}>
          <span className="truncate text-sm font-semibold">{name}</span>
          <span className="truncate text-xs text-muted-foreground">{email}</span>
        </div>
        <DropdownMenu.Separator className="mx-1.5 my-1 h-px bg-glass-hairline" />
        {ROLE_LINKS[role].map(([href, key]) => (
          <DropdownMenu.Item key={href} asChild className={morphMenuItemClass}>
            <Link href={href}>{t(key)}</Link>
          </DropdownMenu.Item>
        ))}
        <DropdownMenu.Separator className="mx-1.5 my-1 h-px bg-glass-hairline" />
        <DropdownMenu.Item
          disabled={signOut.isPending}
          onSelect={() => signOut.mutate(undefined, { onSuccess: () => router.push("/") })}
          className={morphMenuItemClass}
        >
          {t("signOut")}
        </DropdownMenu.Item>
      </MorphMenuContent>
    </DropdownMenu.Root>
  );
}
