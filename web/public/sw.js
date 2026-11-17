// Pinta service worker (docs/screens.md, PWA): caches the app shell and its texts, never the API.
// Static build files and icons are cache-first; pages are network-first with the cached copy as the offline fallback.
const VERSION = "pinta-v2";
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

// Web push (docs/integrations.md, «Avisos y notificaciones push»): the API sends { title, body, url, tag }.
self.addEventListener("push", (event) => {
  let message = { title: "Pinta", body: "", url: "/pt/avisos" };
  try {
    message = { ...message, ...event.data.json() };
  } catch {
    // An empty or unreadable message still shows Pinta's notice.
  }
  event.waitUntil(
    self.registration.showNotification(message.title, {
      body: message.body,
      icon: "/icons/icon-192.png",
      badge: "/icons/icon-192.png",
      tag: message.tag,
      data: { url: message.url },
    }),
  );
});

// Tapping it focuses an open Pinta tab on the notice's page, or opens one.
self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target = new URL(event.notification.data?.url ?? "/pt/avisos", self.location.origin).href;
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(async (windows) => {
      const open = windows.find((w) => new URL(w.url).origin === self.location.origin);
      if (open) {
        await open.focus();
        return open.navigate(target);
      }
      return self.clients.openWindow(target);
    }),
  );
});
