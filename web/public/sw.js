// Pinta service worker (docs/screens.md, PWA): caches the app shell and its texts, never the API.
// Static build files and icons are cache-first; pages are network-first with the cached copy as the offline fallback.
const VERSION = "pinta-v1";
const LANDINGS = ["/pt", "/es", "/en"];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(VERSION)
      .then((cache) => cache.addAll(["/manifest.webmanifest", "/icons/icon-192.png", ...LANDINGS]))
      .catch(() => undefined)
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== VERSION).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== "GET" || url.origin !== self.location.origin) return;
  // Personal data lives behind /api: never cached. The admin's Hangfire dashboard neither.
  if (url.pathname.startsWith("/api/") || url.pathname.startsWith("/hangfire")) return;

  if (url.pathname.startsWith("/_next/static/") || url.pathname.startsWith("/icons/")) {
    event.respondWith(cacheFirst(request));
  } else if (request.mode === "navigate") {
    event.respondWith(networkFirst(request, url));
  }
});

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  const response = await fetch(request);
  if (response.ok) (await caches.open(VERSION)).put(request, response.clone());
  return response;
}

async function networkFirst(request, url) {
  try {
    const response = await fetch(request);
    if (response.ok && response.type === "basic") (await caches.open(VERSION)).put(request, response.clone());
    return response;
  } catch {
    const locale = LANDINGS.find((path) => url.pathname === path || url.pathname.startsWith(`${path}/`)) ?? "/pt";
    return (await caches.match(request)) ?? (await caches.match(locale)) ?? Response.error();
  }
}
