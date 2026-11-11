# 008 — Feedback de pulsación en los botones (y arreglar el de las paredes)

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b (rediseñado sobre la revisión 2 del sistema el 2026-11-11)
- **Severity**: MEDIUM
- **Category**: Physicality (press feedback)
- **Estimated scope**: 3 archivos (`globals.css`, `button.tsx`, `room-editor.tsx`), unas 25 líneas
- **Depends on**: 001 (tokens `--duration-press`, `--duration-release`, `--ease-out`, `--ease-spring`)
- **Sistema**: `plans/000-motion-system.md` §3 (pulsación asimétrica), §5 «Pulsar», §7 y §8 (propiedad `scale`).

## Problem

Pinta es una PWA móvil, y en táctil no hay hover: los botones no responden al tocarlos. Solo cambia el color al soltar, si cambia.

```tsx
// web/src/components/ui/button.tsx:7 — current (start of the base class string)
  "inline-flex shrink-0 items-center justify-center gap-2 rounded-[14px] text-base font-semibold whitespace-nowrap transition-[color,background-color,border-color,box-shadow] outline-none …
```

Las paredes del editor de estancia sí tienen pulsación, pero la transición no la anima: en Tailwind v4, `active:scale-95` usa la propiedad CSS `scale`, y la lista de transición nombra `transform`. La reducción salta en seco.

```tsx
// web/src/components/quote/room-editor.tsx:223 — current (start of wallClass)
    "absolute flex items-center justify-center rounded-md text-[11px] font-semibold transition-[background-color,color,transform] duration-200 ease-out active:scale-95 … motion-reduce:transition-none …
```

## Target

La revisión 2 pide una pulsación **asimétrica**: hundirse rápido (120 ms, `ease-out`) y volver con resorte (360 ms, `ease-spring`). El color debe seguir su propia transición corta (150 ms, curva por defecto de Tailwind), sin resorte. Con utilidades de Tailwind una sola duración y una sola curva se aplican a todas las propiedades, así que esta mezcla no se puede expresar con clases sueltas sin repetir listas largas en cada componente.

Por eso se añade **una utilidad compartida, `pressable`**, en `globals.css` junto a los tokens:

- Transición por propiedad: colores y sombra en 150 ms con la curva por defecto; `scale` en `--duration-release` con `--ease-spring` (la vuelta).
- En `:active`: escala `var(--press-scale, 0.96)` y la transición de `scale` pasa a `--duration-press` con `--ease-out` (el hundimiento).
- Con `prefers-reduced-motion: reduce`: `:active` no escala; el color se mantiene.
- Quien necesite otra profundidad fija `--press-scale` (las paredes, objetivo pequeño: 0.92; la variante `link`: 1).

## Repo conventions to follow

- Tokens y piezas de movimiento compartidas viven en `web/src/app/globals.css` (plan 001); `docs/motion.md` las documenta.
- `buttonVariants` con `cva` en `button.tsx:6-38`: la base es la primera cadena; las variantes van en `variants.variant`.

## Steps

1. En `web/src/app/globals.css`, después del bloque `@media (prefers-reduced-motion: reduce) { .motion-fade-only { … } }`, añade:

```css
/*
 * Press feedback (docs/motion.md, «Pulsar»): sink fast with ease-out, come back with the spring; colours keep their own
 * short transition. --press-scale sets the depth (0.96 by default).
 */
@utility pressable {
  transition-property: color, background-color, border-color, box-shadow, scale;
  transition-duration: 150ms, 150ms, 150ms, 150ms, var(--duration-release);
  transition-timing-function: var(--default-transition-timing-function), var(--default-transition-timing-function), var(--default-transition-timing-function), var(--default-transition-timing-function), var(--ease-spring);

  &:active {
    scale: var(--press-scale, 0.96);
    transition-duration: 150ms, 150ms, 150ms, 150ms, var(--duration-press);
    transition-timing-function: var(--default-transition-timing-function), var(--default-transition-timing-function), var(--default-transition-timing-function), var(--default-transition-timing-function), var(--ease-out);
  }

  @media (prefers-reduced-motion: reduce) {
    &:active {
      scale: none;
    }
  }
}
```

2. En `web/src/components/ui/button.tsx:7`, en la cadena base, sustituye `transition-[color,background-color,border-color,box-shadow]` por `pressable`.
3. En la variante `link` (línea 20): `link: "text-primary underline-offset-4 hover:underline [--press-scale:1]",`.
4. En `web/src/components/quote/room-editor.tsx:223`, en `wallClass`, sustituye `transition-[background-color,color,transform] duration-200 ease-out active:scale-95` por `pressable [--press-scale:0.92]` y quita `motion-reduce:transition-none` (con movimiento reducido el color debe seguir cambiando; la escala ya la quita la utilidad).
5. En `docs/motion.md` (§3 «Distancias y escalas» o §4) y en `plans/000-motion-system.md`, menciona la utilidad `pressable` y `--press-scale`.

## Boundaries

- Solo esas ediciones. No añadas pulsación a tarjetas, enlaces de navegación, pestañas ni al selector de idioma.
- No cambies colores ni tamaños. No uses `transition-all`.
- Si el código no coincide, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test` en verde; el CSS compilado contiene `.pressable` y `.pressable:active` en la capa de utilidades, y la regla de movimiento reducido.
- **Feel check** (teléfono real o emulación táctil):
  - Mantén pulsado «Ver o meu preço» o «Entrar»: se hunde enseguida un 4 %; al soltar vuelve con un rebote ligero.
  - Un enlace con estilo `link` no se encoge. Un botón `disabled` no reacciona.
  - En el editor de estancia, pulsa una pared: se hunde (0.92) de forma suave y vuelve con rebote; el cambio a amarillo sigue siendo rápido.
  - El hover en escritorio cambia el color en 150 ms, sin rebote.
  - Con `prefers-reduced-motion: reduce`: no hay escala; el color sí cambia.
- **Done when**:
  - [ ] Todos los `Button`, salvo `link`, se hunden a 0.96 en 120 ms y vuelven con resorte en 360 ms.
  - [ ] Las paredes se hunden a 0.92 con transición y vuelven con resorte.
  - [ ] Los colores mantienen su transición de 150 ms sin resorte.
  - [ ] Con movimiento reducido no hay escala; el color sí cambia.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**.
