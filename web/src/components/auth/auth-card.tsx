import type { ReactNode } from "react";
import { SiteHeader } from "@/components/site-header";

/** Shared frame for sign-in, register and confirm-email: header plus one centered column. */
export function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <>
      <SiteHeader />
      <main id="main" className="mx-auto w-full max-w-md px-4 pt-6 pb-16 sm:px-6 sm:pt-12">
        <h1 className="text-[26px] leading-tight font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1.5 text-sm text-muted-foreground">{subtitle}</p>}
        <div className="mt-6">{children}</div>
      </main>
    </>
  );
}
