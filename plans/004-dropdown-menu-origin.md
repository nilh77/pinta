# 004 — Menús ⋯ y «Añadir»: escalar desde su botón, curva fuerte y salida

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Physicality & origin
- **Estimated scope**: 1 archivo (`web/src/components/quote/room-schema.tsx`), 2 líneas
- **Depends on**: 001 (`menuMotion` en `web/src/components/ui/motion.ts`)
- **Sistema**: `plans/000-motion-system.md` §4 `menuMotion` y §5 «Menú ⋯ y menú Añadir».

## Problem

```tsx
// web/src/components/quote/room-schema.tsx:189-190 — current
const menuClass =
  "z-50 min-w-52 rounded-2xl border border-border bg-card p-1.5 shadow-lg data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 motion-reduce:animate-none";
```

Lo usan los dos menús del esquema: el ⋯ de cada tarjeta (`room-schema.tsx:134`, `align="end"`) y el de «Añadir» (`:172`, `align="center"`).

- Sin `transform-origin`: el zoom sale del **centro** del menú y no de su botón. El menú ⋯ se abre hacia abajo y a la izquierda del botón de la esquina, así que el crecimiento desde el centro no conecta con el lugar tocado.
- Sin salida: al cerrar desaparece de golpe.
- Curva `ease` y 150 ms (valores por defecto de `tw-animate-css`).
- `motion-reduce:animate-none` también quita el fundido.

Radix publica el origen correcto en `--radix-dropdown-menu-content-transform-origin`.

## Target

```tsx
const menuClass = cn("z-50 min-w-52 rounded-2xl border border-border bg-card p-1.5 shadow-lg", menuMotion);
```

con `menuMotion` de `web/src/components/ui/motion.ts` (plan 001, revisión 2 del sistema): origen en `--radix-dropdown-menu-content-transform-origin`; entrada con fundido, escala 0.9 y 8 px desde el lado del botón (`data-[side=…]:slide-in-from-*-2`), con `ease-spring` en 260 ms; salida con fundido y escala 0.95, `ease-out` en 140 ms; `motion-fade-only` para movimiento reducido.

## Repo conventions to follow

- `cn` ya está importado en `room-schema.tsx`: lo usa la línea 88 (`className={cn(…)}`).
- Clases compartidas de movimiento en `web/src/components/ui/motion.ts`.

## Steps

1. Añade a las importaciones de `web/src/components/quote/room-schema.tsx`:
   `import { menuMotion } from "@/components/ui/motion";`
2. Sustituye las líneas 189-190 por el bloque **Target**.
3. No cambies `itemClass` (líneas 191-192) ni los `DropdownMenu.Content`.

## Boundaries

- Solo ese archivo. No cambies `align`, `sideOffset` ni el contenido de los menús.
- No animes los elementos del menú (resaltado instantáneo: se recorre muchas veces con teclado).
- Si el código no coincide, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde.
- **Feel check**:
  - Abre el ⋯ de una tarjeta: el menú crece desde la esquina superior derecha, junto al botón, no desde su centro. Compruébalo al 10 % en DevTools → Animations.
  - Abre «Añadir» (centrado bajo el botón): crece desde el borde superior central.
  - Cierra con Escape o tocando fuera: se encoge y desvanece en unos 140 ms, más rápido que la apertura.
  - Abre y cierra muy rápido varias veces: nunca se queda medio visible ni a mitad de opacidad.
  - Con `prefers-reduced-motion: reduce`: solo fundido, sin escala.
- **Done when**:
  - [ ] Los dos menús crecen desde su botón con resorte (260 ms, llegan en ~100 ms) y se encogen hacia él (140 ms).
  - [ ] Las opciones no tienen entrada propia; el resaltado sigue siendo instantáneo.
  - [ ] Con movimiento reducido: solo fundido.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**.
