"use client";

import { useCallback, useEffect, useSyncExternalStore } from "react";
import { DARK_QUERY, THEME_COLOR, THEME_KEY } from "./theme-script";

export { THEME_KEY, themeScript } from "./theme-script";

/**
 * Light / dark theme (docs/screens.md, «Tema»; plans/011-dark-mode.md). The choice lives in localStorage on this
 * device; `system` (the default) follows prefers-color-scheme and its changes. The `.dark` class on <html> drives the
 * palette; `themeScript` sets it before the first paint, so there is no light flash.
 */
export type ThemeChoice = "system" | "light" | "dark";
export type Theme = "light" | "dark";

const CHANGE_EVENT = "pinta-theme-change";

export function parseChoice(value: string | null | undefined): ThemeChoice {
  return value === "light" || value === "dark" ? value : "system";
}

export function resolveTheme(choice: ThemeChoice, systemDark: boolean): Theme {
  return choice === "system" ? (systemDark ? "dark" : "light") : choice;
}

function readChoice(): ThemeChoice {
  try {
    return parseChoice(localStorage.getItem(THEME_KEY));
  } catch {
    return "system";
  }
}

const systemDark = () => window.matchMedia(DARK_QUERY).matches;

/**
 * Puts `theme` on the page. Colour transitions are switched off for one frame, so the whole page changes at once
 * instead of every button and card fading on its own (docs/motion.md, «Cambiar de tema»).
 */
export function applyTheme(theme: Theme, root: HTMLElement = document.documentElement) {
  root.classList.add("theme-switching");
  root.classList.toggle("dark", theme === "dark");
  document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]').forEach((meta) => (meta.content = THEME_COLOR[theme]));
  // Force a style pass with transitions off, then let them back on.
  void root.offsetHeight;
  requestAnimationFrame(() => root.classList.remove("theme-switching"));
}

function subscribe(onChange: () => void) {
  const media = window.matchMedia(DARK_QUERY);
  const onStorage = (event: StorageEvent) => event.key === THEME_KEY && onChange();
  media.addEventListener("change", onChange);
  window.addEventListener("storage", onStorage);
  window.addEventListener(CHANGE_EVENT, onChange);
  return () => {
    media.removeEventListener("change", onChange);
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(CHANGE_EVENT, onChange);
  };
}

const snapshot = () => `${readChoice()}:${systemDark() ? "dark" : "light"}`;

/** The current choice and the theme it resolves to; `setChoice` saves and applies it. */
export function useTheme() {
  const value = useSyncExternalStore(subscribe, snapshot, () => "system:light");
  const [choicePart, systemPart] = value.split(":");
  const choice = parseChoice(choicePart);
  const theme = resolveTheme(choice, systemPart === "dark");

  const setChoice = useCallback((next: ThemeChoice) => {
    try {
      if (next === "system") localStorage.removeItem(THEME_KEY);
      else localStorage.setItem(THEME_KEY, next);
    } catch {
      // Storage blocked (private mode): the choice still applies to this page.
    }
    applyTheme(resolveTheme(next, systemDark()));
    window.dispatchEvent(new Event(CHANGE_EVENT));
  }, []);

  return { choice, theme, setChoice };
}

/**
 * Keeps the page in step when the resolved theme changes outside `setChoice`: the system switches to dark with the
 * choice on `system`, or another tab changes the choice. Mount once, near the root.
 */
export function useThemeSync() {
  const { theme } = useTheme();
  useEffect(() => {
    if (document.documentElement.classList.contains("dark") !== (theme === "dark")) applyTheme(theme);
  }, [theme]);
}
