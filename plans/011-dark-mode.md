# 011 — Modo oscuro con selector en la cabecera

- **Status**: DONE (2026-11-12), salvo lo marcado como sin verificar en «Verification»
- **Commit**: 27bb8c5 (`ui-improvements`)
- **Category**: Tema, tokens y accesibilidad
- **Estimated scope**: ~25 archivos (la mayoría, cambiar un color fijo por un token), 2 documentos, 3 traducciones; 5 commits
- **Depende de**: 010 (cabecera, cristal y sus tokens de `.dark`)

## Problem

La app solo tiene modo claro (`web/src/app/globals.css:4`: «Light only in the MVP»). Los tokens del cristal ya tienen bloque `.dark` (plan 010), pero nada pone esa clase y el resto de la paleta no tiene versión oscura. Además, 23 colores están escritos a mano en 19 componentes (`#0b5f59`, `#b9c9c4`, `#7c918b`, `#fffbeb`, `#4a3700`, `#e0ecff`, `#1d4ed8`…) y el botón destructivo usa `text-white`, así que en oscuro no seguirían al tema.

## Target

### A. Paleta oscura (`globals.css`, bloque `.dark`)

Neutros con el mismo matiz verde azulado (~178°) y la marca aclarada para fondo oscuro. Contraste medido (WCAG):

| Token | Claro | Oscuro | Contraste en oscuro |
|---|---|---|---|
| `--background` | `#f5faf8` | `#0d1715` | — |
| `--foreground` | `#10201d` | `#e4efec` | 15,5:1 sobre el fondo |
| `--card` / `--popover` | `#ffffff` | `#14211e` | texto 14,1:1 |
| `--muted-foreground` | `#4f5f5b` | `#9fb3ae` | 7,5:1 sobre tarjeta |
| `--primary` | `#0f766e` | `#3cbdb0` | como texto, 7,2:1 sobre tarjeta |
| `--primary-hover` | `#0b5f59` | `#5ccabf` | — |
| `--primary-foreground` | `#ffffff` | `#062421` | 7,1:1 sobre `--primary` |
| `--primary-soft`, `--secondary`, `--muted` | `#e6f4f1` | `#123a35` / `#18302c` | `--primary` encima 5,4:1 |
| `--secondary-foreground` | `#0f766e` | `#8fe0d5` | 8,2:1 |
| `--accent` | `#f2b705` | `#f2b705` (igual) | `--accent-foreground` 9,3:1 |
| `--accent-soft` / `--accent-ink` | `#fdf0c2` / `#6b5000` | `#3b300b` / `#f5d36b` | 8,9:1 |
| `--destructive` | `#b42318` | `#f07065` | como texto, 5,7:1 |
| `--destructive-foreground` (nuevo) | `#ffffff` | `#0d1715` | 6,3:1 sobre `--destructive` |
| `--border` / `--input` | `#d7e3df` | `#263a35` | — |
| `--ring` | `#0f766e` | `#3cbdb0` | — |

El cristal ya tiene sus valores de `.dark` (brillo 0.07, sombras más densas); se pasan a valores fijos como en claro, y se mide el contraste de los rellenos en oscuro antes de cerrarlo.

### B. Colores fijos → tokens

Nuevos tokens (claro / oscuro) para lo que hoy está escrito a mano:

- `--primary-ink` (`#0b5f59` / `#8fe0d5`): texto verde intenso (`address-step`, `estimate-step`, `payment-screen`, `payments-list`, `forgot-password-form`, `sent-step`, `lib/jobs/status.ts`).
- `--line-soft` (`#b9c9c4`), `--line-dashed` (`#7c918b`), `--wall-off` (`#e3ebe8`), `--surface-sunken` (`#dce8e4`), `--illustration-line` (`#c9d6d2`), con sus equivalentes oscuros: líneas del esquema, paredes apagadas, ilustración.
- `--accent-wash` (`#fffbeb`), `--accent-line` (`#f2d27a`), `--accent-ink-strong` (`#4a3700`): avisos amarillos (`waitlist`, `estimate-step`, `photos-step`, botón «Añadir» del esquema).
- `--info-soft` / `--info` (`#e0ecff` / `#1d4ed8`) y `--neutral-soft` (`#eef1f0`): insignias de estado (`lib/jobs/status.ts`, `payments-list`).
- Botón destructivo y su spinner: `text-destructive-foreground` en vez de `text-white`.

Se quedan como están, a propósito: las **muestras de pintura** del esquema (`PAINT_COLOURS`: representan el color real de cada pintura), el **visor de fotos** (siempre oscuro) y `app/global-error.tsx` (estilos en línea mínimos para cuando falla toda la app).

### C. Mecanismo

- `.dark` en `<html>`. Preferencia guardada en `localStorage` (`pinta-theme`: `system` | `light` | `dark`; por defecto `system`, que sigue `prefers-color-scheme` y escucha sus cambios).
- Un script en línea en el `<head>` aplica la clase **antes de pintar** (sin parpadeo); `<html suppressHydrationWarning>`. Sin dependencias (no `next-themes`).
- `color-scheme: light` / `dark` en `:root` / `.dark`, para barras de scroll y controles nativos.
- `themeColor` de la `viewport` con las dos variantes por `media`; el manifiesto PWA sigue en claro.
- Stripe Elements (`payment-screen`): `appearance` con el tema oscuro de Stripe y nuestros colores cuando `.dark`.

### D. Selector en la cabecera

Un tercer control redondo en el grupo de la derecha (entre idioma y cuenta), con icono de sol o luna según el tema efectivo. Su panel crece desde él como los otros dos: **Sistema**, **Claro** y **Oscuro**, con la opción actual marcada. Al elegir, el cambio es inmediato; las transiciones de color no se animan en ese momento (se desactivan un fotograma para que la página no «pinte» a cámara lenta).

### E. Documentos y textos

- `docs/screens.md`: «Tema» en los estados globales y el tercer control en «Cabecera».
- `docs/motion.md`: quitar «solo modo claro» y anotar el cambio de tema sin transición.
- `web/messages/{pt,es,en}.json`: etiquetas del control y del panel.

## Steps (un commit por paso)

1. Documentos.
2. Paleta oscura y nuevos tokens en `globals.css` (+ `color-scheme`, `--destructive-foreground`), y colores fijos → tokens en los componentes (B).
3. Mecanismo: script anti-parpadeo, `lib/theme.ts` (leer, guardar, aplicar, escuchar el sistema) con tests, `themeColor`, Stripe.
4. Selector en la cabecera con su panel y traducciones.
5. Verificación (contraste en oscuro de cristal, insignias y avisos; capturas de las pantallas principales en los dos modos a 390 y 1440 px) y CHANGELOG.

## Boundaries

- Sin dependencias nuevas. No se tocan la API, los emails ni el manifiesto PWA.
- No se rediseña nada: mismo layout, mismos componentes; solo colores por token.
- Si un color fijo no encaja en ningún token de la lista, se para y se pregunta en vez de inventar uno.

## Verification

- `pnpm lint`, `pnpm typecheck`, `pnpm test` (tests de `lib/theme.ts`: preferencia por defecto, guardar, aplicar la clase, seguir al sistema).
- `grep` sin colores hex en componentes salvo `PAINT_COLOURS`, visor y `global-error`.
- Navegador: cada pantalla principal en claro y oscuro; recargar en oscuro sin destello claro; cambiar de tema desde el panel; contraste medido de texto sobre cristal, insignias y avisos.
- Sin verificar desde aquí: Safari y Firefox, y la emulación del modo del sistema en un dispositivo real.
