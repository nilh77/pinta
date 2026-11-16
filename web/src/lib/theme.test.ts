import { afterEach, describe, expect, it, vi } from "vitest";
import { applyTheme, parseChoice, resolveTheme, themeScript, THEME_KEY } from "./theme";

const media = (dark: boolean) =>
  vi.fn().mockReturnValue({ matches: dark, addEventListener: () => {}, removeEventListener: () => {} }) as unknown as typeof window.matchMedia;

afterEach(() => {
  localStorage.clear();
  document.documentElement.className = "";
  document.head.innerHTML = "";
  vi.unstubAllGlobals();
});

describe("parseChoice and resolveTheme", () => {
  it("defaults to the system when nothing (or junk) is stored", () => {
    expect(parseChoice(null)).toBe("system");
    expect(parseChoice("purple")).toBe("system");
    expect(parseChoice("dark")).toBe("dark");
  });

  it("follows the system only when the choice is system", () => {
    expect(resolveTheme("system", true)).toBe("dark");
    expect(resolveTheme("system", false)).toBe("light");
    expect(resolveTheme("light", true)).toBe("light");
    expect(resolveTheme("dark", false)).toBe("dark");
  });
});

describe("applyTheme", () => {
  it("toggles the dark class and the browser bar colour", () => {
    document.head.innerHTML = '<meta name="theme-color" content="#f5faf8">';
    applyTheme("dark");
    expect(document.documentElement.classList.contains("dark")).toBe(true);
    expect(document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')!.content).toBe("#0d1715");
    applyTheme("light");
    expect(document.documentElement.classList.contains("dark")).toBe(false);
  });
});

describe("themeScript (runs before React, in <head>)", () => {
  const run = () => new Function(themeScript)();

  it("applies a stored dark choice even when the system is light", () => {
    vi.stubGlobal("matchMedia", media(false));
    localStorage.setItem(THEME_KEY, "dark");
    run();
    expect(document.documentElement.classList.contains("dark")).toBe(true);
  });

  it("follows a dark system when nothing is stored", () => {
    vi.stubGlobal("matchMedia", media(true));
    run();
    expect(document.documentElement.classList.contains("dark")).toBe(true);
  });

  it("keeps light when light is chosen on a dark system", () => {
    vi.stubGlobal("matchMedia", media(true));
    localStorage.setItem(THEME_KEY, "light");
    run();
    expect(document.documentElement.classList.contains("dark")).toBe(false);
  });
});
