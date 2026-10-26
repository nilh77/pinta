"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";
import { setApiLanguage } from "@/lib/api/client";
import { startPwa } from "@/lib/pwa/install";
import { Toaster } from "./toaster";

export function Providers({ locale, children }: { locale: string; children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient());
  // Set during render so the first queries already send the page language.
  setApiLanguage(locale);
  useEffect(startPwa, []);
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <Toaster />
    </QueryClientProvider>
  );
}
