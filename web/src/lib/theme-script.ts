/**
 * The parts of the theme that the server layout needs: no React here, so a Server Component can import it
 * (lib/theme.ts holds the client hooks).
 */
export const THEME_KEY = "pinta-theme";
export const THEME_COLOR = { light: "#f5faf8", dark: "#0d1715" } as const;
export const DARK_QUERY = "(prefers-color-scheme: dark)";

/** Inline in <head>: same rules as `resolveTheme`, before React loads, so there is no light flash. Keep it tiny. */
export const themeScript = `(function(){try{var c=localStorage.getItem("${THEME_KEY}");var d=c==="dark"||(c!=="light"&&matchMedia("${DARK_QUERY}").matches);document.documentElement.classList.toggle("dark",d);document.querySelectorAll('meta[name="theme-color"]').forEach(function(m){m.content=d?"${THEME_COLOR.dark}":"${THEME_COLOR.light}"})}catch(_){}})()`;
