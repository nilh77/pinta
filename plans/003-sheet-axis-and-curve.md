# 003 — Hoja: un solo eje por breakpoint, curva de cajón y fundido con movimiento reducido

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: HIGH
- **Category**: Physicality & origin + Easing & duration (+ Accessibility)
- **Estimated scope**: 1 archivo (`web/src/components/ui/sheet.tsx`), unas 10 líneas
- **Depends on**: 001 (tokens y `drawerMotion` / `drawerOverlayMotion` en `web/src/components/ui/motion.ts`)
- **Sistema**: `plans/000-motion-system.md` §3, §5 «Abrir/Cerrar la hoja», §6 y §7.

## Problem

```tsx
// web/src/components/ui/sheet.tsx:27-35 — current
        <Dialog.Overlay className="fixed inset-0 z-40 bg-foreground/40 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 motion-reduce:animate-none" />
        <Dialog.Content
          aria-describedby={undefined}
          className={cn(
            "fixed z-50 flex flex-col bg-background shadow-xl outline-none",
            "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-3xl data-[state=open]:animate-in data-[state=open]:slide-in-from-bottom data-[state=closed]:animate-out data-[state=closed]:slide-out-to-bottom",
            "md:inset-y-0 md:right-0 md:left-auto md:max-h-none md:w-[440px] md:rounded-none md:rounded-l-3xl md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-right",
            "duration-200 motion-reduce:animate-none",
          )}
        >
```

Tres problemas:

1. **Diagonal en 768–1023 px.** `slide-in-from-bottom` (sin breakpoint) fija `--tw-enter-translate-y: 100%`, y `md:slide-in-from-right` solo añade `--tw-enter-translate-x: 100%`. El keyframe `enter` de `tw-animate-css` usa `translate3d(var(--tw-enter-translate-x), var(--tw-enter-translate-y), 0)`, así que el panel lateral entra y sale en diagonal desde la esquina inferior derecha.
2. **Curva y duración.** `duration-200` sin curva: `tw-animate-css` usa `ease` por defecto. Para un panel de altura completa, 200 ms con `ease` arranca blando y llega sin peso. Un cajón pide la curva `cubic-bezier(0.32, 0.72, 0, 1)`, 380 ms al entrar y 240 ms al salir.
3. **Movimiento reducido.** `motion-reduce:animate-none` quita también el fundido: la hoja aparece de golpe. Con movimiento reducido debe quitarse el desplazamiento y quedar el fundido.

## Target

```tsx
        <Dialog.Overlay className={cn("fixed inset-0 z-40 bg-foreground/40", drawerOverlayMotion)} />
        <Dialog.Content
          aria-describedby={undefined}
          className={cn(
            "fixed z-50 flex flex-col bg-background shadow-xl outline-none",
            "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-3xl",
            "md:inset-y-0 md:right-0 md:left-auto md:max-h-none md:w-[440px] md:rounded-none md:rounded-l-3xl",
            drawerMotion,
          )}
        >
```

donde `drawerMotion` y `drawerOverlayMotion` vienen de `web/src/components/ui/motion.ts` (plan 001):

- `drawerMotion` = `motion-fade-only ease-drawer data-[state=open]:animate-in data-[state=open]:duration-(--duration-drawer-in) data-[state=closed]:animate-out data-[state=closed]:duration-(--duration-drawer-out) max-md:data-[state=open]:slide-in-from-bottom max-md:data-[state=closed]:slide-out-to-bottom md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-right`
- `--ease-drawer: cubic-bezier(0.32, 0.72, 0, 1)`, `--duration-drawer-in: 380ms`, `--duration-drawer-out: 240ms` (revisión 2).

## Repo conventions to follow

- `cn()` de `@/lib/utils` ya se importa en `sheet.tsx:6`.
- Las clases de movimiento compartidas viven en `web/src/components/ui/motion.ts` (plan 001); impórtalas, no copies cadenas.

## Steps

1. En `web/src/components/ui/sheet.tsx`, añade la importación junto a las demás:
   `import { drawerMotion, drawerOverlayMotion } from "./motion";`
2. Sustituye la línea 27 (Overlay) por
   `<Dialog.Overlay className={cn("fixed inset-0 z-40 bg-foreground/40", drawerOverlayMotion)} />`
3. Sustituye el `className={cn(…)}` de `Dialog.Content` (líneas 30-35) por el del bloque **Target**: las clases de posición y forma se quedan; salen todas las de animación (`data-[state=…]:animate-*`, `slide-*`, `duration-200`, `motion-reduce:animate-none`) y entra `drawerMotion`.
4. No cambies nada más del componente (asa, cabecera, pie).
5. Durante la salida el velo sigue montado y se comía los toques (hallazgo de la verificación del plan 002; principio «la coreografía nunca bloquea la interacción»). Radix pone `pointer-events: auto` en línea sobre el velo y mezcla encima el `style` recibido, así que una clase no basta. En `web/src/components/ui/motion.ts` añade el helper `overlayStyle(open)` (devuelve `{ pointerEvents: "none" }` cuando está cerrado) y úsalo en el velo de la hoja: `style={overlayStyle(open)}`. El plan 005 lo usará en los velos de los diálogos.


## Boundaries

- Solo `web/src/components/ui/sheet.tsx` y, para el paso 5, el helper `overlayStyle` en `motion.ts`.
- No cambies el comportamiento de apertura y cierre (plan 002) ni añadas arrastre (plan 009).
- En `motion.ts` solo el cambio del paso 5; si falta una constante, PARA e informa.
- Si el código no coincide con el extracto, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde.
- **Feel check** (mejor con el plan 002 ya aplicado, para ver también la salida):
  - **390 px**: la hoja sube en vertical puro; arranca rápida y se asienta suave al final, sin rebote.
  - **800 px** (entre 768 y 1023): el panel entra **solo desde la derecha**, sin componente vertical. En DevTools → Animations al 10 %, el borde inferior del panel queda fijo durante todo el recorrido.
  - **≥ 1024 px**: no hay hoja (editor en línea); nada cambia.
  - Salida más corta que la entrada (240 ms frente a 380 ms) y el velo se desvanece al mismo ritmo.
  - En DevTools → Rendering, emula `prefers-reduced-motion: reduce`: la hoja y el velo aparecen y desaparecen con fundido, **sin desplazarse**.
- **Done when**:
  - [ ] < 768 px: desliza solo en vertical; 768–1023 px: solo en horizontal (comprobado al 10 %).
  - [ ] Entrada de 380 ms y salida de 240 ms con `ease-drawer`; velo y panel empiezan y terminan juntos.
  - [ ] Con movimiento reducido: solo fundido, sin desplazamiento.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**: «La hoja del editor entra por un solo eje con curva de cajón».
