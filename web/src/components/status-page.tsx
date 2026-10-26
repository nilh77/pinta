import type { ReactNode } from "react";
import { LocaleSwitcher } from "@/components/locale-switcher";
import { Logo } from "@/components/logo";

/** Shared frame of the 404 and error pages (docs/screens.md, global states). */
export function StatusPage({ code, title, body, children }: { code: string; title: string; body: string; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="mx-auto flex w-full max-w-2xl items-center justify-between px-4 pt-3 sm:px-6">
        <Logo className="text-[22px]" />
        <LocaleSwitcher />
      </header>
      <main id="main" className="mx-auto flex w-full max-w-2xl flex-1 flex-col items-start justify-center gap-3 px-4 pb-24 sm:px-6">
        <p className="font-display text-6xl font-extrabold tracking-tight text-primary">{code}</p>
        <h1 className="text-[28px] font-extrabold tracking-tight">{title}</h1>
        <p className="max-w-prose text-muted-foreground">{body}</p>
        <div className="mt-3 flex flex-wrap gap-2">{children}</div>
      </main>
    </div>
  );
}
