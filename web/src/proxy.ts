import { NextResponse, type NextRequest } from "next/server";
import { hasLocale } from "next-intl";
import createMiddleware from "next-intl/middleware";
import { routing } from "./i18n/routing";

const handleI18n = createMiddleware(routing);

export const LOCALE_COOKIE = "NEXT_LOCALE";
const ONE_YEAR = 60 * 60 * 24 * 365;

/**
 * Language without a saved choice is Portuguese; once the visitor opens /es or /en, that choice is remembered on the
 * device and the bare "/" goes back to it (C1.1). The browser language never redirects (docs/prd-overview.md).
 */
export default function proxy(request: NextRequest) {
  const saved = request.cookies.get(LOCALE_COOKIE)?.value;
  const { pathname } = request.nextUrl;
  if (pathname === "/" && hasLocale(routing.locales, saved)) {
    return NextResponse.redirect(new URL(`/${saved}`, request.url));
  }

  const response = handleI18n(request);
  const locale = pathname.split("/")[1];
  if (hasLocale(routing.locales, locale) && locale !== saved) {
    response.cookies.set(LOCALE_COOKIE, locale, { path: "/", maxAge: ONE_YEAR, sameSite: "lax" });
  }
  return response;
}

export const config = {
  // Everything except the proxied API, the Hangfire dashboard, Next internals and files with an extension.
  matcher: "/((?!api|hangfire|_next|_vercel|.*\\..*).*)",
};
