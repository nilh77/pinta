"use client";

import { Toaster as Sonner } from "sonner";
import { useIsDesktop } from "@/lib/quote/use-quote-data";

/** Toasts at the top on phones and bottom right on desktop (docs/screens.md, global states). */
export function Toaster() {
  const desktop = useIsDesktop();
  return (
    <Sonner
      position={desktop ? "bottom-right" : "top-center"}
      toastOptions={{ classNames: { toast: "!rounded-2xl !border-border !bg-card !text-foreground !font-sans", actionButton: "!bg-primary !text-primary-foreground !font-semibold" } }}
    />
  );
}
