import createMiddleware from "next-intl/middleware";
import { routing } from "./i18n/routing";

export default createMiddleware(routing);

export const config = {
  // Everything except the proxied API, the Hangfire dashboard, Next internals and files with an extension.
  matcher: "/((?!api|hangfire|_next|_vercel|.*\\..*).*)",
};
