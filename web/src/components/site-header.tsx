"use client";

import type { ReactNode } from "react";
import { AppHeader } from "./app-header";
import { Logo } from "./logo";

/**
 * Header of the public pages and of the client and painter areas. `children`: page-specific links shown next to the
 * logo on wide screens (landing D1).
 */
export function SiteHeader({ children }: { children?: ReactNode }) {
  return (
    <AppHeader
      leading={
        <>
          <Logo className="min-h-0 text-xl" />
          {children}
        </>
      }
    />
  );
}
