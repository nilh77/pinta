# 001 — Crear la base de movimiento: tokens, clase de movimiento reducido y clases compartidas

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: LOW (es requisito de 003, 004, 005, 008 y 009)
- **Category**: Cohesion & tokens
- **Estimated scope**: 4 archivos (`globals.css`, `motion.ts` nuevo, `docs/motion.md` nuevo, `CLAUDE.md`), unas 60 líneas de código más el documento
- **Sistema**: implementa §3, §4 y §7 de `plans/000-motion-system.md` (revisión 2, «vivo y funcional») y publica ese documento como `docs/motion.md`.

## Problem

`web/src/app/globals.css` no define ningún token de movimiento. Cada componente elige su curva y su duración a mano: `duration-200` suelto en `web/src/components/ui/sheet.tsx:34`, `web/src/components/quote/room-editor.tsx:223` y `:263`; el resto usa los valores por defecto de Tailwind (150 ms) o de `tw-animate-css` (150 ms, `ease`). Además, `motion-reduce:animate-none` quita la animación entera (también el fundido), cuando con movimiento reducido solo debería quitarse el desplazamiento y la escala.

```css
/* web/src/app/globals.css:1-2 — current (no motion tokens anywhere in the file) */
@import "tailwindcss";
@import "tw-animate-css";
```

## Target

1. Curvas como tokens de tema de Tailwind. Generan las utilidades `ease-out`, `ease-in-out`, `ease-drawer` y `ease-spring`, que también fijan `--tw-ease`, la variable que lee `tw-animate-css`. `--ease-spring` es un resorte con rebote 0.3 expresado con `linear()` de CSS, sin librería.
2. Duraciones como variables de `:root`. Tailwind v4 no tiene espacio de tema para duraciones; se usan con `duration-(--duration-…)`.
3. La clase `motion-fade-only`: con `prefers-reduced-motion: reduce` anula el desplazamiento y la escala de `tw-animate-css` y fuerza el fundido. Va **fuera de cualquier `@layer`** para ganar a las utilidades.
4. Un módulo con las cadenas de clases compartidas: `web/src/components/ui/motion.ts`.
5. El sistema publicado en `docs/motion.md` y enlazado desde `CLAUDE.md`.

## Repo conventions to follow

- Los colores ya son tokens en `globals.css:5-29` (`:root`) y se exponen en `@theme inline` (`globals.css:31-61`). Los tokens de movimiento van en bloques **nuevos** (`@theme` sin `inline` y un `:root` aparte), no mezclados con la paleta.
- Comentarios de código en inglés, como el resto del archivo (`/* Palette B (…) */`).
- Las clases se componen con `cn()` de `web/src/lib/utils.ts`.

## Steps

1. En `web/src/app/globals.css`, justo después del bloque `@theme inline { … }` (que termina en la línea 61), añade:

```css
/*
 * Motion tokens (docs/motion.md). Enter with a spring that settles, leave with a strong ease-out,
 * sheets with the drawer curve. --ease-spring: damping ratio 0.7 (bounce 0.3), reaches the target at 38 %.
 */
@theme {
  --ease-out: cubic-bezier(0.23, 1, 0.32, 1);
  --ease-in-out: cubic-bezier(0.77, 0, 0.175, 1);
  --ease-drawer: cubic-bezier(0.32, 0.72, 0, 1);
  --ease-spring: linear(0, 0.0337, 0.1179, 0.2308, 0.3562, 0.482, 0.6001, 0.7056, 0.7957, 0.8698, 0.9283, 0.9726, 1.0043, 1.0255, 1.0383, 1.0445, 1.046, 1.044, 1.0399, 1.0346, 1.0288, 1.023, 1.0177, 1.0129, 1.0089, 1.0056, 1.003, 1.001, 0.9996, 0.9987, 0.9982, 0.9979, 1);
}

/* Durations, used as duration-(--duration-…). Spring durations include the settle; exits are shorter. */
:root {
  --duration-press: 120ms;
  --duration-release: 360ms;
  --duration-menu-in: 260ms;
  --duration-menu-out: 140ms;
  --duration-layer-in: 340ms;
  --duration-layer-out: 180ms;
  --duration-drawer-in: 380ms;
  --duration-drawer-out: 240ms;
}

/*
 * Reduced motion keeps the fade and drops movement and scale (tw-animate-css variables).
 * Unlayered on purpose: it must win over the utilities layer.
 */
@media (prefers-reduced-motion: reduce) {
  .motion-fade-only {
    --tw-enter-translate-x: 0;
    --tw-enter-translate-y: 0;
    --tw-exit-translate-x: 0;
    --tw-exit-translate-y: 0;
    --tw-enter-scale: 1;
    --tw-exit-scale: 1;
    --tw-enter-opacity: 0;
    --tw-exit-opacity: 0;
  }
}
```

   El bloque `@layer base { … }` de las líneas 63-74 se queda como está, después de lo nuevo.

2. Crea `web/src/components/ui/motion.ts` con exactamente este contenido:

```ts
/**
 * Shared motion classes for Radix layers (docs/motion.md). Layers enter with a settling spring from where they
 * come from and leave faster with a strong ease-out. `motion-fade-only` keeps only the fade with reduced motion.
 */

/** Dim backdrop behind dialogs: fades with the dialog. */
export const overlayMotion =
  "ease-out data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-layer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:duration-(--duration-layer-out)";

/** Centred dialogs: rise and settle from the centre (correct for modals), shrink slightly on the way out. */
export const dialogMotion =
  "motion-fade-only data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-90 data-[state=open]:slide-in-from-bottom-4 data-[state=open]:ease-spring data-[state=open]:duration-(--duration-layer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:ease-out data-[state=closed]:duration-(--duration-layer-out)";

/** Dropdown menus: grow out of the trigger, nudged from its side, never from the centre. */
export const menuMotion =
  "motion-fade-only origin-(--radix-dropdown-menu-content-transform-origin) data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-90 data-[state=open]:ease-spring data-[state=open]:duration-(--duration-menu-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:ease-out data-[state=closed]:duration-(--duration-menu-out)";

/** Backdrop behind a sheet: same curve and timing as the sheet. */
export const drawerOverlayMotion =
  "ease-drawer data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-drawer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:duration-(--duration-drawer-out)";

/** Sheet: from the bottom below 768 px, from the right from 768 px. Each axis only at its breakpoint. */
export const drawerMotion =
  "motion-fade-only ease-drawer data-[state=open]:animate-in data-[state=open]:duration-(--duration-drawer-in) data-[state=closed]:animate-out data-[state=closed]:duration-(--duration-drawer-out) max-md:data-[state=open]:slide-in-from-bottom max-md:data-[state=closed]:slide-out-to-bottom md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-right";
```

3. No uses todavía estas constantes en ningún componente: eso lo hacen los planes 003, 004, 005 y 009.

4. Crea `docs/motion.md` copiando `plans/000-motion-system.md` desde «## 1. Personalidad» hasta el final de «## 9», sin la cabecera de estado ni la §10 (decisiones para aprobar). Título: `# Sistema de movimiento`, con una línea inicial: «Reglas de movimiento del frontend. Los tokens viven en `web/src/app/globals.css` y las clases compartidas en `web/src/components/ui/motion.ts`.» En español, como el resto de `docs/`.

5. En `CLAUDE.md`, en la tabla de documentos (tras la fila de `docs/screens.md`), añade:
   `| \`docs/motion.md\` | Motion system: principles, tokens, choreography per event, reduced motion, performance |`
   y, en **Conventions**, una viñeta:
   `- Motion: only the tokens in \`globals.css\` and the classes in \`web/src/components/ui/motion.ts\`; follow \`docs/motion.md\`. No new animation dependencies.`

## Boundaries

- No toques la paleta (`globals.css:4-61`) ni `@layer base`.
- No cambies los valores: vienen de `plans/000-motion-system.md` (revisión 2) y del catálogo de la auditoría.
- Redefinir `--ease-out` cambia la utilidad `ease-out` que ya usan `web/src/components/quote/room-editor.tsx:223` y `:263` (transiciones de color de 200 ms). Es intencionado: deja esas líneas como están.
- No añadas dependencias.
- Si el código no coincide con lo descrito (cambios desde el commit 02c3f9b), PARA e informa en lugar de improvisar.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde. `pnpm build` compila; en el CSS generado (`.next/static/css/*.css`) aparecen `--ease-spring`, `--duration-drawer-in` y `.motion-fade-only` **fuera** de `@layer`, y las cinco cadenas de `motion.ts` producen sus reglas (comprobar `zoom-in-90`, `ease-spring`, `origin-(--radix-dropdown-menu-content-transform-origin)` y `max-md:data-[state=open]:slide-in-from-bottom`).
- **Feel check**: este plan no cambia nada visible, salvo las paredes del editor de estancia: con la nueva curva, el cambio a amarillo debe sentirse algo más inmediato.
- **Done when**:
  - [ ] Los tres bloques están en `globals.css` y `.motion-fade-only` queda fuera de `@layer` en el CSS compilado.
  - [ ] `motion.ts` existe con las cinco constantes, sin usos todavía, y sus clases aparecen en el CSS compilado.
  - [ ] `docs/motion.md` existe y `CLAUDE.md` lo enlaza.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde y `pnpm build` compila.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar** (en español): «Sistema de movimiento: tokens, clases compartidas y `docs/motion.md`».
