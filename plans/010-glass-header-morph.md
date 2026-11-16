# 010 — Cabecera de cristal con controles que se transforman en su panel

- **Status**: DONE (2026-11-11), salvo lo marcado como sin verificar en «Verification»
- **Commit**: 338c264 (`ui-improvements`)
- **Severity**: — (rediseño pedido, no hallazgo)
- **Category**: Material, cabecera y continuidad espacial (*morph*)
- **Estimated scope**: ~14 archivos de código, 3 documentos, 3 ficheros de traducción; 5 commits
- **Sistema**: extiende `docs/motion.md` (tokens, capas, reducido) con el material de cristal y la transformación desde el control.

## Decisiones ya tomadas (2026-11-11)

| Tema | Decisión |
|---|---|
| Modo oscuro | Se preparan los tokens de `.dark` aunque nada los active todavía (la app sigue solo en claro). |
| Cabecera | Sin barra: logo en una píldora de cristal (no hay migas de pan) e **idioma** y **cuenta** como controles redondos con panel. Sin notificaciones (fuera del PRD). Primero se actualiza `docs/screens.md`. |
| Alcance | Las cuatro cabeceras: portada y área del cliente/pintor (`SiteHeader`), cotizador (`QuoteShell`), área admin (`AdminShell`) y las páginas sueltas (`status-page`, `sent-step`). |
| Asentamiento | El resorte del sistema (`--ease-spring`, se pasa un 4,6 %), no el `--ease-bounce` del encargo (12,5 %). |

## Problem

Hoy la cabecera es una fila plana sobre el fondo liso `#f5faf8`: logo, selector de idioma de tres botones (`web/src/components/locale-switcher.tsx`) y enlaces de cuenta (`web/src/components/site-header.tsx:22-59`). Cada área repite su propia variante (`quote/quote-shell.tsx:64-79`, `admin/admin-shell.tsx:64-111`, `status-page.tsx:9-12`, `quote/sent-step.tsx:44-47`). No hay ningún control que abra un panel, así que el idioma ocupa sitio fijo en el móvil y la cuenta se reparte en dos o tres botones según el rol.

## Target

### A. Material de cristal (`globals.css`)

- **Fondo con brillos** en `body` (clase `canvas-backdrop`): pseudo-elemento `::before` fijo, `z-index: -1`, `pointer-events: none`, sin crear contexto de apilamiento en el shell. Dos degradados radiales con `--glow-a` (arriba a la izquierda) y `--glow-b` (abajo a la derecha) al `--backdrop-wash`.
- **Tokens** (claro, valores fijos porque la paleta B es fija; la derivación queda en el comentario):
  - `--canvas: oklch(0.955 0.0062 178.6)` (de `--muted-foreground` con L 0.955 y C × 0.3)
  - `--glow-a: oklch(0.74 0.0861 186.4)` (de `--primary` con L 0.74)
  - `--glow-b: oklch(0.74 0.0861 246.4)` (`--glow-a` + 60° de tono)
  - `--backdrop-wash: 10%`, `--glass-blur: 7px`, `--glass-overlay-blur: 14px`, `--glass-saturate: 1.8`, `--glass-chrome: 48%`, `--glass-fill: 64%`, `--glass-control: 55%`, `--glass-hairline: color-mix(in oklab, var(--foreground) 7%, transparent)`, `--glass-sheen: oklch(1 0 0 / 0.7)`, `--chrome-contact: oklch(0 0 0 / 0.04)`, `--chrome-ambient: oklch(0 0 0 / 0.09)`, `--shadow-chrome: 0 2px 6px 0 var(--chrome-contact), 0 16px 40px -14px var(--chrome-ambient)`.
  - `.dark`: los valores del encargo, con la sintaxis relativa (`oklch(from var(--background) …)`), que se resolverá cuando exista la paleta oscura.
  - `@media (prefers-reduced-transparency: reduce)`: blur 20px, saturate 1.3, rellenos al 92 %.
- **Utilidad `glass`** (`@utility`): `backdrop-filter: blur(var(--glass-blur)) saturate(var(--glass-saturate))` (+ `-webkit-`), `border-color: transparent`, y el borde de luz como **sombras interiores** a través de `--tw-inset-shadow` y `--tw-inset-ring-shadow`, para que `shadow-*` y `ring-*` sigan funcionando.
- **Tres rellenos** (utilidades de color con `color-mix`): `bg-glass-chrome` (`--card` al `--glass-chrome`), `bg-glass-control` (al `--glass-control`) y `bg-glass-fill` (`--popover` al `--glass-fill`). El proyecto no tiene `--sidebar`: se usa `--card`.

### B. Cabecera nueva (`components/app-header.tsx`, nuevo)

- Fila `sticky top-0 z-20 p-4`, `pointer-events-none`; sus piezas `pointer-events-auto`. Clase `morph-header`.
- **Izquierda**: píldora de 40 px, redondeada, `glass bg-glass-chrome shadow-chrome`, con el logo (y, según la pantalla, «volver» en el cotizador o los enlaces de sección de la portada en escritorio).
- **Derecha**: `data-morph-cluster`, controles redondos de 40 px a 8 px, `glass bg-glass-control shadow-chrome`, cada uno con `data-morph-trigger` y `aria-label` traducido:
  - **Idioma** (`LanguageMenu`): icono de globo + código actual; el panel lista PT/ES/EN como enlaces (`lang`, `hrefLang`, `aria-current`).
  - **Cuenta** (`AccountMenu`): sin sesión, un enlace redondo a «Entrar» (sin panel); con sesión, iniciales y un panel por rol: cliente (Mis trabajos, Cuenta, Salir), pintor (Mis trabajos, Pagos, Salir), admin (Panel, Salir).
- Se usa en `SiteHeader` (portada, entrar/registro, cliente, pintor), `QuoteShell` (la barra de progreso queda debajo, sin cambios), `status-page` y `sent-step`. En el **admin**, la barra lateral de escritorio pasa a `glass bg-glass-chrome` y conserva la navegación; idioma, nombre y «Salir» salen de la barra y van al grupo de la derecha; en el móvil, la navegación sigue debajo de la cabecera.
- `LocaleSwitcher` se elimina (lo sustituye `LanguageMenu`); el cambio de idioma de la cuenta (`/cuenta`) no cambia.

### C. Panel que crece desde su control (*morph*)

- `components/ui/dropdown-menu.tsx` (nuevo): `MorphMenuContent`, envoltorio de `DropdownMenu.Content` con `align="end"`, `sideOffset={-40}`, radio 20 px, fila superior con 40 px de margen derecho, `glass bg-glass-fill` con `--glass-blur: var(--glass-overlay-blur)`, clase `morph-popover` y el hook. Menús con `modal={false}` para saltar de uno a otro. **Sin `Popover`**: los dos paneles son menús y no hace falta otro envoltorio.
- `lib/use-trigger-morph.ts` (nuevo): el hook del encargo (§3.4) tal cual: busca el disparador por `aria-controls`/`aria-labelledby`; mide cuando `style.animationName` (longhand) deja de ser `none`, sobre `[data-radix-popper-content-wrapper]`; fija `--morph-inset`, `--morph-seed-r` y `--morph-r`; aparta los vecinos del grupo con `translateX` usando posiciones en reposo (`DOMMatrixReadOnly(...).m41`), con `data-morph-yield` cuando no hay sitio; restaura al cerrar; `data-morph-closing` en el disparador hasta desmontar; una sesión por nodo en un `WeakMap` con contador y `queueMicrotask`.
- **CSS fuera de capas** (para ganar a `animate-in`/`animate-out`), con los valores del encargo salvo el asentamiento:
  - Entrada: `popover-morph-in 380ms var(--ease-out)` (clip desde el círculo hasta 2 rem más allá del panel) + `popover-morph-settle 640ms var(--ease-spring)` (escala 0.94 → 1). Salida: `popover-morph-out 220ms var(--ease-out)`.
  - Vecinos: `transform` en 240 ms al abrir y 220 ms al cerrar, `--ease-out`; `opacity` 200 ms. `[data-morph-yield] { opacity: 0; pointer-events: none }`.
  - `.morph-header:has([data-morph-trigger][data-state="open"], [data-morph-trigger][data-morph-closing]) { z-index: 60 }`; el disparador abierto pierde su superficie (fondo transparente, sin sombra ni blur, 200 ms).
  - Movimiento reducido: fundidos de 120 ms y vecinos sin transición.
  - El `--ease-morph` del encargo, `cubic-bezier(0.2, 0.9, 0.25, 1)`, es casi idéntico a nuestro `--ease-out`, `cubic-bezier(0.23, 1, 0.32, 1)`: se usa el del sistema y no se crea un token paralelo.
- Foco: al cerrar vuelve al control (Radix lo hace con su `Trigger`; se comprueba).

### D. Superficies de cristal en capas

Menús del esquema, diálogos y la hoja pasan a `glass bg-glass-fill` con el blur de capa. Si alguna no llega a 4,5:1 de contraste sobre la zona más clara del fondo, sube su relleno (se mide, no se supone).

### E. Documentos y textos

- `docs/screens.md`: nueva sección «Cabecera» en los componentes compartidos (píldora, idioma, cuenta por rol, comportamiento en el móvil) y en las pantallas que la usan.
- `docs/motion.md`: §4 y §5 con el material, la transformación desde el control y su coreografía; §7 con transparencia reducida.
- `web/messages/{pt,es,en}.json`: etiquetas de los controles y del panel de cuenta.

## Repo conventions to follow

- Tokens en `globals.css` y piezas compartidas en `components/ui/` (`motion.ts`, `skeleton.tsx`); reglas que deben ganar a las utilidades, fuera de capas (como `.motion-fade-only`).
- Textos solo en `messages/*.json`; comentarios en inglés; Radix desde `radix-ui`.
- `useReturnFocus` no se aplica a los menús: tienen `Trigger` y Radix devuelve el foco.

## Steps (un commit por paso)

1. **Documentos**: `docs/screens.md` y `docs/motion.md`.
2. **Material**: tokens, `.dark`, transparencia reducida, `canvas-backdrop`, `glass` y rellenos en `globals.css`; superficies de capa (D). Test de que `.glass` y las reglas reducidas existen en el CSS compilado.
3. **Hook y envoltorio**: `use-trigger-morph.ts` con tests de sus cálculos puros (inset, radio de la semilla, desplazamiento de vecinos, restauración) y `ui/dropdown-menu.tsx`.
4. **Cabecera**: `app-header.tsx`, `LanguageMenu`, `AccountMenu`, traducciones; sustitución en `SiteHeader`, `QuoteShell`, `status-page`, `sent-step` y `AdminShell`; eliminar `LocaleSwitcher`.
5. **Verificación y CHANGELOG**: contraste medido, 390/800/1440 px, cambio de un panel a otro, movimiento reducido.

## Boundaries

- No se añaden dependencias. No se cambian rutas, roles ni la API.
- No se toca la barra de progreso del cotizador ni el contenido de las páginas.
- Si `backdrop-filter` o `:has()` no están, la cabecera debe seguir siendo usable (relleno más opaco; sin cambio de `z-index` el panel sigue funcionando).

## Verification

- **Mecánico**: `pnpm lint`, `pnpm typecheck`, `pnpm test`; CSS compilado con `.glass`, `.morph-popover` y las reglas reducidas fuera de capas.
- **En el navegador integrado (Chromium)**: 390, 800 y 1440 px; abrir idioma y cuenta, ver que crecen desde su círculo y vuelven a él; los vecinos se mueven con el panel; pasar de un panel al otro sin cerrar y reabrir; en el móvil, lo que tapa el panel se desvanece; el foco vuelve al control; contraste ≥ 4,5:1 medido sobre el brillo más claro.
- **Sin verificar desde aquí**: Safari y Firefox (en particular `backdrop-filter` con `-webkit-`, `clip-path` animado e `inset … round`), `prefers-reduced-transparency` real (solo Chromium lo soporta) y el modo oscuro, que no se puede activar.
- **Done when**:
  - [ ] Cabecera y paneles se leen como cristal sobre el fondo con brillos; texto ≥ 4,5:1; con transparencia reducida, casi opacos.
  - [ ] Cada panel crece desde su círculo y vuelve a él; los vecinos se mueven con él y regresan al cerrar.
  - [ ] Pasar de un panel abierto a otro control lo abre sin salto ni parpadeo.
  - [ ] En el móvil, las piezas tapadas se desvanecen en vez de salir de la pantalla.
  - [ ] Movimiento reducido: fundidos simples; el foco vuelve al control al cerrar.
  - [ ] Documentos, traducciones y CHANGELOG al día.
