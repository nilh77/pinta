# 008 — Feedback de pulsación en los botones (y arreglar el de las paredes)

- **Status**: TODO
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Physicality (press feedback)
- **Estimated scope**: 2 archivos, 3 líneas
- **Depends on**: 001 (`--duration-press` y la curva `ease-out` fuerte)
- **Sistema**: `plans/000-motion-system.md` §3 `--duration-press`, §5 «Pulsar», §7 y §8 (propiedad `scale`).

## Problem

Pinta es una PWA móvil, y en táctil no hay hover: los botones no responden al tocarlos. Solo cambia el color al soltar, si cambia.

```tsx
// web/src/components/ui/button.tsx:7 — current (start of the base class string)
  "inline-flex shrink-0 items-center justify-center gap-2 rounded-[14px] text-base font-semibold whitespace-nowrap transition-[color,background-color,border-color,box-shadow] outline-none …
```

Las paredes del editor de estancia sí tienen pulsación, pero la transición no la anima: en Tailwind v4, `active:scale-95` usa la propiedad CSS `scale`, y la lista de transición nombra `transform`. La reducción salta en seco.

```tsx
// web/src/components/quote/room-editor.tsx:223 — current (start of wallClass)
    "absolute flex items-center justify-center rounded-md text-[11px] font-semibold transition-[background-color,color,transform] duration-200 ease-out active:scale-95 …
```

## Target

- `Button`: `active:scale-[0.97]`, animado en 160 ms con `cubic-bezier(0.23, 1, 0.32, 1)` (`--duration-press`, `ease-out`). La variante `link` no se encoge. Con movimiento reducido no hay escala; el color sigue cambiando.
- Paredes: la transición incluye `scale` en lugar de `transform` y usa `--duration-press`.

## Repo conventions to follow

- `buttonVariants` con `cva` en `button.tsx:6-38`: la base es la primera cadena; las variantes van en `variants.variant`.
- Tokens del plan 001: `duration-(--duration-press)`, `ease-out`.

## Steps

1. En `web/src/components/ui/button.tsx:7`, dentro de la cadena base, sustituye
   `transition-[color,background-color,border-color,box-shadow]`
   por
   `transition-[color,background-color,border-color,box-shadow,scale] duration-(--duration-press) ease-out active:scale-[0.97] motion-reduce:active:scale-100`.
   El resto de la cadena no cambia.
2. En la misma `cva`, variante `link` (línea 20), añade `active:scale-100`:
   `link: "text-primary underline-offset-4 hover:underline active:scale-100",`
3. En `web/src/components/quote/room-editor.tsx:223`, dentro de `wallClass`, sustituye
   `transition-[background-color,color,transform] duration-200 ease-out`
   por
   `transition-[background-color,color,scale] duration-(--duration-press) ease-out`.
   Deja igual `active:scale-95` y `motion-reduce:transition-none`.

## Boundaries

- Solo esas tres ediciones. No añadas pulsación a tarjetas, enlaces de navegación, pestañas ni al selector de idioma: se pulsan muchas veces y el cambio de color basta.
- No cambies colores ni tamaños.
- No uses `transition-all`.
- Si el código no coincide, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde (si hay tests de snapshot de `Button`, actualízalos solo por este cambio de clases).
- **Feel check** (teléfono real o emulación táctil):
  - Mantén pulsado «Ver estimación» o «Entrar»: el botón se encoge apenas (3 %) al tocar y vuelve al soltar, sin rebote; se percibe como respuesta, no como animación.
  - Un enlace con estilo `link` no se encoge.
  - Un botón `disabled` no reacciona.
  - En el editor de estancia, pulsa una pared: ahora la reducción es suave en lugar de saltar. Al 10 % en DevTools → Animations se ve la transición de `scale`.
  - Con `prefers-reduced-motion: reduce`: no hay escala en los botones; el color sí cambia.
- **Done when**:
  - [ ] Todos los `Button`, salvo `link`, se encogen a 0.97 en 160 ms al pulsar.
  - [ ] Las paredes animan su escala en lugar de saltar.
  - [ ] Con movimiento reducido no hay escala; el color sí cambia.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**.
