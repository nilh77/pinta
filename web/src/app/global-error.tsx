"use client";

/** Last resort when the layout itself fails: no translations are loaded, so it speaks the three languages. */
export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="pt">
      <body style={{ fontFamily: "system-ui, sans-serif", background: "#f5faf8", color: "#10201d", margin: 0 }}>
        <main style={{ maxWidth: 560, margin: "0 auto", padding: "15vh 16px" }}>
          <p style={{ fontSize: 56, fontWeight: 800, color: "#0f766e", margin: 0 }}>500</p>
          <h1 lang="pt">Algo correu mal.</h1>
          <p lang="es">Algo ha ido mal.</p>
          <p lang="en">Something went wrong.</p>
          <p style={{ display: "flex", gap: 8 }}>
            <button type="button" onClick={reset} style={{ minHeight: 44, padding: "0 16px", borderRadius: 12, border: 0, background: "#0f766e", color: "#fff", fontWeight: 600 }}>
              Tentar de novo · Reintentar · Try again
            </button>
            {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- the layout failed: a full page load is the way back. */}
            <a href="/" style={{ minHeight: 44, display: "inline-flex", alignItems: "center", padding: "0 16px", color: "#0f766e", fontWeight: 600 }}>
              Pinta
            </a>
          </p>
        </main>
      </body>
    </html>
  );
}
