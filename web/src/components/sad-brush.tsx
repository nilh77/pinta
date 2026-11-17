import { cn } from "@/lib/utils";

/**
 * The status pages' illustration (docs/screens.md, «Errores 404 y 500»): a sad brush lying next to a drop of paint
 * (404) or beside the tin it knocked over (500). Drawn with the theme's tokens, so it follows light and dark.
 */
export function SadBrush({ variant, className }: { variant: "lost" | "spilled"; className?: string }) {
  return (
    <svg viewBox="12 82 222 90" aria-hidden="true" className={cn("h-auto w-full max-w-[280px]", className)}>
      {/* Floor shadow */}
      <ellipse cx="120" cy="160" rx="100" ry="8" className="fill-primary-soft" />

      {variant === "spilled" ? (
        <g>
          {/* Tipped-over tin and its puddle */}
          <path d="M150 150c18-6 52-6 74 0 6 2 4 8-4 9-22 3-48 3-68 0-8-1-8-7-2-9Z" className="fill-accent" />
          <g transform="rotate(-80 186 132)">
            <rect x="160" y="112" width="52" height="40" rx="5" className="fill-card stroke-foreground" strokeWidth="3" />
            <rect x="160" y="112" width="52" height="11" rx="4" className="fill-primary" />
            <path d="M166 112c4-10 36-10 40 0" fill="none" className="stroke-foreground" strokeWidth="3" strokeLinecap="round" />
          </g>
        </g>
      ) : (
        /* A single drop that got away */
        <path d="M196 120c0 0-13 16-13 24a13 13 0 0 0 26 0c0-8-13-24-13-24Z" className="fill-accent stroke-foreground" strokeWidth="3" strokeLinejoin="round" />
      )}

      {/* The brush, lying down and a little deflated */}
      <g transform="rotate(-8 100 130)">
        {/* Handle */}
        <rect x="20" y="118" width="74" height="18" rx="9" className="fill-primary stroke-foreground" strokeWidth="3" />
        <circle cx="32" cy="127" r="3" className="fill-card" />
        {/* Ferrule with the face */}
        <rect x="90" y="108" width="40" height="38" rx="6" className="fill-card stroke-foreground" strokeWidth="3" />
        <path d="M100 122q3 3 6 0M114 122q3 3 6 0" fill="none" className="stroke-foreground" strokeWidth="2.5" strokeLinecap="round" />
        <path d="M103 136q7-6 14 0" fill="none" className="stroke-foreground" strokeWidth="2.5" strokeLinecap="round" />
        {/* Drooping bristles, paint at the tips */}
        <path
          d="M130 110c14 0 30 4 38 12 6 6 4 14-2 16-4 1-6-2-8-1-3 2-3 6-8 6-4 0-5-4-8-4-3 0-4 4-8 3-3-1-4-4-4-7Z"
          className="fill-accent stroke-foreground"
          strokeWidth="3"
          strokeLinejoin="round"
        />
        <path d="M134 118c8 1 18 4 24 9" fill="none" className="stroke-foreground" strokeWidth="2" strokeLinecap="round" opacity="0.5" />
      </g>
    </svg>
  );
}
