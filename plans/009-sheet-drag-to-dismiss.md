# 009 — Arrastrar hacia abajo para cerrar la hoja en móvil

- **Status**: TODO
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Interruptibility (gesto)
- **Estimated scope**: 1 archivo (`web/src/components/ui/sheet.tsx`), unas 50 líneas
- **Depends on**: 001, 002 y 003 (la salida tiene que existir y ser vertical antes de añadir el gesto)
- **Sistema**: `plans/000-motion-system.md` §5 «Arrastrar la hoja», §6, §7 y §8.

## Problem

La hoja muestra un asa en móvil que invita a arrastrar para cerrar, pero no hay gesto: arrastrarla no hace nada.

```tsx
// web/src/components/ui/sheet.tsx:37-38 — current
          <div aria-hidden="true" className="mx-auto mt-2 h-1.5 w-10 rounded-full bg-border md:hidden" />
          <div className="flex items-center gap-2 px-4 pt-2 pb-1 md:pt-5">
```

En una PWA móvil, arrastrar hacia abajo es la forma natural de cerrar una hoja.

## Target

- Solo por debajo de 768 px. Se arrastra desde el asa y la fila del título; el contenido sigue haciendo scroll normal.
- Durante el arrastre, la hoja sigue al dedo 1:1 hacia abajo. Hacia arriba ofrece resistencia creciente: desplazamiento `-Math.sqrt(-dy) * 2`, nunca un tope seco.
- Al soltar, se cierra si la velocidad media `dy / ms > 0.11` **o** si se arrastró más del 25 % de su altura. Si no, vuelve a su sitio con `translate` animado y la curva `ease-drawer`.
- Al cerrar desde una posición arrastrada, la salida empieza **donde está** la hoja. El keyframe `exit` de `tw-animate-css` anima `transform` y el arrastre usa la propiedad `translate`: se suman y no hay salto.
- Los botones de la cabecera (renombrar, cerrar) siguen funcionando: el gesto no arranca si el puntero empieza sobre un `button`, `a` o `input`.

## Repo conventions to follow

- `sheet.tsx` ya es `"use client"` y usa `cn()`.
- Constantes con nombre en MAYÚSCULAS a nivel de módulo, como `PATH` en `web/src/components/quote/schema-step.tsx:34`.
- El estado del arrastre se reinicia en cada apertura porque `SheetEditor` se vuelve a montar con una `key` por sesión (plan 002). No hace falta reiniciarlo a mano.

## Steps

1. En `web/src/components/ui/sheet.tsx`, cambia la importación de React a
   `import { useRef, useState, type PointerEvent, type ReactNode } from "react";`
   y añade bajo las importaciones:

```tsx
/** Release faster than this (px/ms) or further than this fraction of the height and the sheet closes. */
const DISMISS_VELOCITY = 0.11;
const DISMISS_FRACTION = 0.25;
```

2. Dentro de `Sheet`, antes del `return`, añade:

```tsx
  const content = useRef<HTMLDivElement>(null);
  const drag = useRef<{ y: number; time: number } | null>(null);
  const [offset, setOffset] = useState(0);
  const [dragging, setDragging] = useState(false);

  const startDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (!window.matchMedia("(max-width: 767px)").matches) return;
    if ((event.target as Element).closest("button, a, input")) return;
    drag.current = { y: event.clientY, time: event.timeStamp };
    event.currentTarget.setPointerCapture(event.pointerId);
    setDragging(true);
  };
  const moveDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (!drag.current) return;
    const dy = event.clientY - drag.current.y;
    // Downwards follows the finger; upwards resists more the further it goes.
    setOffset(dy >= 0 ? dy : -Math.sqrt(-dy) * 2);
  };
  const endDrag = (event: PointerEvent<HTMLDivElement>) => {
    const start = drag.current;
    drag.current = null;
    setDragging(false);
    if (!start) return;
    const dy = event.clientY - start.y;
    const velocity = dy / Math.max(1, event.timeStamp - start.time);
    const height = content.current?.offsetHeight ?? Infinity;
    if (event.type === "pointerup" && dy > 0 && (velocity > DISMISS_VELOCITY || dy > height * DISMISS_FRACTION)) {
      onOpenChange(false); // keep the offset: the exit animation starts from here
      return;
    }
    setOffset(0);
  };
```

3. En `Dialog.Content`, añade `ref={content}` y
   `style={offset !== 0 ? { translate: \`0 ${offset}px\` } : undefined}`,
   y añade a su `cn(…)` el elemento
   `!dragging && "transition-[translate] motion-reduce:transition-none"`.
   El `ease-drawer` y la duración ya los aporta `drawerMotion` (plan 003).

4. Envuelve el asa y la fila del título (líneas 37-47 actuales: el `div` del asa y el `div` de la cabecera) en un único contenedor:

```tsx
          <div
            onPointerDown={startDrag}
            onPointerMove={moveDrag}
            onPointerUp={endDrag}
            onPointerCancel={endDrag}
            className="max-md:touch-none max-md:cursor-grab"
          >
            {/* the existing handle div and header div, unchanged */}
          </div>
```

## Boundaries

- Solo `web/src/components/ui/sheet.tsx`. No añadas librerías (ni `vaul` ni Motion).
- No hagas arrastrable el área de contenido (`overflow-y-auto`): tiene scroll propio.
- No cambies el comportamiento de escritorio ni de tableta (≥ 768 px): allí `startDrag` sale enseguida.
- No muevas el velo con el arrastre (queda fuera de alcance).
- Si `drawerMotion` no existe en `motion.ts` o la hoja sigue desmontándose al cerrar (plan 002 sin aplicar), PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde.
- **Feel check** (en un teléfono real o con emulación táctil a 390 px):
  - Arrastra lento desde el asa unos 40 px y suelta: la hoja vuelve a su sitio con suavidad, sin salto.
  - Arrastra lento más de un cuarto de la altura y suelta: se cierra y la salida continúa desde donde estaba, sin volver arriba antes.
  - Haz un gesto corto y rápido hacia abajo (menos del 25 %): se cierra por velocidad.
  - Arrastra hacia arriba: se mueve poco y cada vez menos; al soltar vuelve.
  - Toca la X y el lápiz de renombrar: funcionan y no arrastran.
  - Haz scroll en el contenido de una estancia larga: hace scroll, no arrastra la hoja.
  - Con `prefers-reduced-motion: reduce`, el arrastre sigue funcionando; la vuelta a su sitio es instantánea y el cierre, solo con fundido.
- **Done when**:
  - [ ] < 768 px: cierra por velocidad (> 0.11 px/ms) o distancia (> 25 %); si no, vuelve con `ease-drawer`.
  - [ ] La salida tras arrastrar parte de la posición actual, sin salto.
  - [ ] La X, renombrar y el scroll del contenido funcionan igual que antes; ≥ 768 px no cambia.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**: «La hoja del editor se cierra arrastrando hacia abajo».
