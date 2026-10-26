import createClient, { type Middleware } from "openapi-fetch";
import type { paths } from "./schema";

const XSRF_HEADER = "X-XSRF-TOKEN";
const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

let xsrfToken: Promise<string> | null = null;
let language = "pt";

/** Same-origin client: Next.js proxies /api to the API and the session cookie travels with every call. */
export const api = createClient<paths>({ baseUrl: "", credentials: "same-origin" });

/** API errors and catalog texts follow the page language, not the browser's. */
export function setApiLanguage(locale: string) {
  language = locale;
}

/** The antiforgery token is tied to the signed-in user: drop it after login, logout or registration. */
export function resetAntiforgeryToken() {
  xsrfToken = null;
}

function antiforgeryToken(): Promise<string> {
  xsrfToken ??= fetch("/api/auth/antiforgery", { credentials: "same-origin" })
    .then((response) => response.json() as Promise<{ token: string }>)
    .then((body) => body.token)
    .catch((error: unknown) => {
      xsrfToken = null;
      throw error;
    });
  return xsrfToken;
}

let sessionSeen = false;
const expiredListeners = new Set<() => void>();

/**
 * Global state «Sesión caducada» (docs/screens.md): a 401 only means the session expired when this page had one. A
 * visitor who was never signed in, or who signed out, gets no dialog. Returns true when the dialog should open.
 */
export function trackSession(path: string, status: number) {
  if (path === "/api/auth/logout") {
    sessionSeen = false;
    return false;
  }
  if (status === 401) {
    const expired = sessionSeen;
    sessionSeen = false;
    return expired;
  }
  if (path === "/api/auth/me" || path === "/api/auth/login" || path === "/api/auth/register") {
    sessionSeen = status >= 200 && status < 300;
  }
  return false;
}

export function onSessionExpired(listener: () => void) {
  expiredListeners.add(listener);
  return () => {
    expiredListeners.delete(listener);
  };
}

const headers: Middleware = {
  async onRequest({ request }) {
    request.headers.set("Accept-Language", language);
    if (!SAFE_METHODS.has(request.method)) {
      request.headers.set(XSRF_HEADER, await antiforgeryToken());
    }
    return request;
  },
  async onResponse({ request, response }) {
    if (trackSession(new URL(request.url, "http://localhost").pathname, response.status)) {
      expiredListeners.forEach((listener) => listener());
    }
    if (response.status === 403) {
      // A stale token (e.g. the session expired) is replaced on the next attempt.
      const body = await response.clone().json().catch(() => null);
      if (body?.code === "antiforgery_failed") resetAntiforgeryToken();
    }
    return response;
  },
};

api.use(headers);
