import type { ReactNode } from "react";
import { AppHeader } from "@/components/app-header";
import { Logo } from "@/components/logo";

/** Shared frame of the 404 and error pages (docs/screens.md, global states). */
export function StatusPage({ code, title, body, children }: { code: string; title: string; body: string; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <AppHeader leading={<Logo className="min-h-0 text-xl" />} />
      <main id="main" className="mx-auto flex w-full max-w-2xl flex-1 flex-col items-start justify-center gap-3 px-4 pb-24 sm:px-6">
        <p className="font-display text-6xl font-extrabold tracking-tight text-primary">{code}</p>
        <h1 className="text-[28px] font-extrabold tracking-tight">{title}</h1>
        <p className="max-w-prose text-muted-foreground">{body}</p>
        <div className="mt-3 flex flex-wrap gap-2">{children}</div>
      </main>
    </div>
  );
}
