import type { ReactNode } from "react";
import { AppHeader } from "@/components/app-header";
import { Logo } from "@/components/logo";
import { SadBrush } from "@/components/sad-brush";

/** Shared frame of the 404 and error pages (docs/screens.md, global states): text, actions and the sad brush. */
export function StatusPage({ code, title, body, children }: { code: string; title: string; body: string; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <AppHeader leading={<Logo className="min-h-0 text-xl" />} />
      <main
        id="main"
        className="mx-auto flex w-full max-w-4xl flex-1 flex-col-reverse items-center justify-center gap-8 px-4 pb-24 sm:px-6 md:flex-row md:justify-between"
      >
        <div className="flex w-full max-w-xl flex-col items-start gap-3">
          <p className="font-display text-6xl font-extrabold tracking-tight text-primary">{code}</p>
          <h1 className="text-[28px] font-extrabold tracking-tight">{title}</h1>
          <p className="max-w-prose text-muted-foreground">{body}</p>
          <div className="mt-3 flex flex-wrap gap-2">{children}</div>
        </div>
        <SadBrush variant={code === "404" ? "lost" : "spilled"} className="w-56 md:w-72" />
      </main>
    </div>
  );
}
