import { defineRouting } from "next-intl/routing";

// Routes from docs/screens.md are the internal keys; translated pathnames come in H8.
export const routing = defineRouting({
  locales: ["pt", "es", "en"],
  defaultLocale: "pt",
  localePrefix: "always",
  // PT is the default for everyone (docs/prd-overview.md); the browser language does not redirect.
  localeDetection: false,
});

export type AppLocale = (typeof routing.locales)[number];
