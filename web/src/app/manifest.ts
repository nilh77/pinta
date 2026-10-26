import type { MetadataRoute } from "next";

/** Installable PWA (docs/screens.md, global states): palette B, icons from tools/pwa_icons.py. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Pinta",
    short_name: "Pinta",
    description: "Pintura de casas no distrito de Lisboa.",
    // "/" returns to the language chosen on this device.
    start_url: "/",
    scope: "/",
    display: "standalone",
    orientation: "portrait",
    background_color: "#f5faf8",
    theme_color: "#0f766e",
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}
