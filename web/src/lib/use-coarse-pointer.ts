"use client";

import { useSyncExternalStore } from "react";

const QUERY = "(pointer: coarse)";

function subscribe(onChange: () => void) {
  const media = window.matchMedia(QUERY);
  media.addEventListener("change", onChange);
  return () => media.removeEventListener("change", onChange);
}

/**
 * True on touch screens (phones and tablets), where «Hacer foto» opens the camera (docs/screens.md, pantalla 5 and P2).
 * False on the server and on desktops with a mouse.
 */
export function useCoarsePointer() {
  return useSyncExternalStore(subscribe, () => window.matchMedia(QUERY).matches, () => false);
}
