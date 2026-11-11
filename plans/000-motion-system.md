# 000 — Sistema de movimiento de Pinta

- **Status**: APROBADO (2026-11-11)
- **Commit**: 02c3f9b
- **Qué es**: la especificación común de los planes 001–009. Ningún plan puede contradecirla. Al aprobarse, el plan 001 la copia a `docs/motion.md` y la añade a la tabla de documentos de `CLAUDE.md`. A partir de ahí, `docs/motion.md` es la fuente: si un plan y el documento discrepan, gana el documento.
- **Base**: `.agents/skills/improve-animations/AUDIT.md` (valores), `.agents/skills/choreograph/SKILL.md` (relaciones entre elementos) y `.agents/skills/animation-vocabulary/SKILL.md` (términos).

## 1. Personalidad

Pinta vende confianza: un precio claro, una persona que revisa y un pintor elegido con cuidado. El movimiento tiene que ser **preciso y tranquilo, con una pincelada**:

- **Preciso**: responde enseguida, sin exhibirse. Es una herramienta que el cliente usa para describir su casa y el admin para trabajar todo el día.
- **Tranquilo**: nada rebota ni da vueltas, ni hay cascadas en la interfaz de trabajo. Los cambios de estado se cuentan con color y opacidad.
- **Una pincelada**: el amarillo #F2B705 marca el gesto de «pintar» (paredes que se activan, el check de «solicitud enviada» y de «señal pagada»). Es el único sitio con margen para algo de deleite, y por eso se reserva a momentos raros.

## 2. Principios

1. **Primero la respuesta.** Todo lo que el usuario toca responde en ≤ 160 ms. Ninguna animación retrasa la acción ni el foco. Radix mueve el foco al montar, sin esperar a la animación.
2. **Cada capa viene de un sitio y vuelve a él** (*origin-aware animation*). Un menú crece desde su botón, una hoja sale del borde del que vino y vuelve a él, y un diálogo aparece en el centro. La salida es la inversa de la entrada, más corta.
3. **Una entrada por contenedor.** Si la capa se mueve, su contenido viaja con ella (*carry*). Nunca hay escalonado dentro de hojas, diálogos, menús, listas ni pestañas.
4. **Estado con color y opacidad, no con movimiento.** Seleccionar, activar o marcar un valor desactualizado cambia color u opacidad (`transition-colors`, opacidad 50–60 %). Así se hace ya hoy y se mantiene.
5. **La frecuencia manda.** Lo que se usa decenas de veces al día (navegación, pestañas, idioma, filas, resaltado de menú, teclado) no anima o solo cambia de color. Los momentos raros pueden durar más.
6. **Movimiento reducido = fundido.** Con `prefers-reduced-motion: reduce` se quita el desplazamiento y la escala y se conserva la opacidad. No se apaga todo (*reduced motion*, no *no motion*).
7. **Solo propiedades del compositor.** Se animan `opacity`, `transform`, `translate` y `scale`. Nunca `transition-all` ni propiedades de layout (`width`, `height`, `top`, `margin`).
8. **Sin dependencias nuevas.** CSS + `tw-animate-css` (ya instalado) + la presencia de Radix (mantiene montado el contenido hasta que acaba `animate-out`). Una librería JS solo entraría por un gesto o una secuencia que CSS no pueda expresar, y hoy no hay ninguna.

## 3. Tokens

Viven en `web/src/app/globals.css` (plan 001). Las curvas son tokens de tema de Tailwind: generan `ease-out`, `ease-in-out` y `ease-drawer`, que fijan `--tw-ease`, la variable que lee `tw-animate-css`. Las duraciones son variables de `:root` y se usan con `duration-(--duration-…)`.

### Curvas

| Token | Valor | Uso |
|---|---|---|
| `--ease-out` | `cubic-bezier(0.23, 1, 0.32, 1)` | Todo lo que entra o sale y la respuesta a pulsar. Curva por defecto. |
| `--ease-in-out` | `cubic-bezier(0.77, 0, 0.175, 1)` | Algo que ya está en pantalla y se desplaza de A a B (p. ej. una futura píldora deslizante en pestañas). |
| `--ease-drawer` | `cubic-bezier(0.32, 0.72, 0, 1)` | Hojas: entrada, salida y vuelta tras arrastrar. |
| Tailwind por defecto | `cubic-bezier(0.4, 0, 0.2, 1)`, 150 ms | Cambios de color (`transition-colors`). Se mantiene: ya está en ~20 sitios y es correcto para color. |
| `linear` | — | Solo spinners y progreso continuo. |

`ease-in` está prohibido en la interfaz.

### Duraciones

| Token | Valor | Uso |
|---|---|---|
| `--duration-press` | 160 ms | Respuesta al pulsar (escala). |
| `--duration-menu-in` / `-out` | 180 / 120 ms | Menús desplegables. |
| `--duration-layer-in` / `-out` | 220 / 150 ms | Diálogos centrados y su velo. |
| `--duration-drawer-in` / `-out` | 320 / 220 ms | Hojas y su velo. |
| (local) | 600 ms | Momentos raros (check dibujado). Se define en su plan, no como token. |

Toda la interfaz de trabajo queda por debajo de 300 ms, salvo la entrada de la hoja (320 ms). La hoja es un panel de altura completa, y el catálogo admite 200–500 ms para cajones. Las salidas son siempre más cortas que las entradas: el usuario ya decidió irse.

### Distancias y escalas

| Qué | Valor |
|---|---|
| Escala de entrada de menús y diálogos | 0.95 (`zoom-in-95` / `zoom-out-95`). Nunca `scale(0)`. |
| Escala al pulsar | 0.97 en botones, 0.95 en las paredes (objetivo pequeño). |
| Desplazamiento de hojas | 100 % de su propio tamaño (`slide-*`): nunca píxeles fijos. |

## 4. Capas compartidas

Las cadenas de clases viven en `web/src/components/ui/motion.ts` (plan 001). Es la única abstracción: cinco constantes de texto, sin componente envoltorio ni hook.

| Constante | Para | Relación entre velo y contenido |
|---|---|---|
| `overlayMotion` | Velo de los diálogos | Juntos (*together*): mismo inicio y duración que `dialogMotion`. |
| `dialogMotion` | Contenido de diálogos centrados | Fundido + escala 0.95 desde el centro (el centro es el origen correcto en un modal). |
| `menuMotion` | `DropdownMenu.Content` | Fundido + escala 0.95 desde `--radix-dropdown-menu-content-transform-origin`. |
| `drawerOverlayMotion` | Velo de la hoja | Juntos con `drawerMotion`: misma curva y duración. |
| `drawerMotion` | Contenido de la hoja | Desliza en un solo eje por anchura (ver §6). |

Todas incluyen `motion-fade-only` (§7), salvo los velos, que ya solo hacen fundido.

## 5. Coreografía por evento

Línea temporal de cada evento con varias piezas en movimiento (formato de `choreograph`). Lo que no aparece en una tabla no se mueve.

### Abrir la hoja del editor (< 1024 px)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Panel de la hoja | Protagonista | 0 ms | 320 ms | desliza desde el borde (abajo < 768; derecha 768–1023) | — |
| Velo | Decoración | 0 ms | 320 ms | fundido | Juntos con el panel |
| Contenido (paredes, medidas, pinturas) | Estático | — | — | viaja con el panel | *Carry*: una entrada por contenedor |
| Tarjeta tocada en el esquema | Estático | — | — | anillo de selección inmediato (`ring`) | — |

### Cerrar la hoja (Guardar, Cancelar, X, Escape, velo o arrastre)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Panel | Protagonista | 0 ms (o desde donde quedó tras arrastrar) | 220 ms | vuelve al borde por el que entró | Inversa de la entrada |
| Velo | Decoración | 0 ms | 220 ms | fundido | Juntos |
| Tarjeta guardada | Apoyo | 0 ms | 150 ms | color (borde, fondo, check) | Juntos: la casa se «pinta» mientras la hoja se va |

### Arrastrar la hoja (< 768 px)

Durante el arrastre, el panel sigue al dedo 1:1 sin transición; hacia arriba tiene resistencia creciente (*rubber-banding*). Al soltar, cierra si la velocidad es > 0.11 px/ms o si se arrastró más del 25 % de su altura, y la salida parte de la posición actual. Si no cierra, vuelve con `ease-drawer`. El velo no sigue al dedo.

### Diálogo centrado (confirmar, formulario, renombrar, sesión caducada)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Diálogo | Protagonista | 0 ms | 220 / 150 ms | fundido + escala 0.95 → 1 | — |
| Velo | Decoración | 0 ms | 220 / 150 ms | fundido | Juntos |
| Campos y botones | Estático | — | — | viajan con el diálogo | *Carry* |
| Hoja de debajo (renombrar desde la hoja) | Estático | — | — | — | No retrocede ni se oscurece aparte |

### Visor de fotos (admin)

Solo fundido al abrir (220 ms). El cierre es instantáneo a propósito: el contenido se vacía al cerrar y una salida animada mostraría un fondo negro vacío.

### Menú ⋯ y menú «Añadir»

Un solo elemento en movimiento: el menú (180 / 120 ms, desde su botón). Las opciones aparecen todas a la vez y el resaltado es instantáneo (se recorre con teclado). Al elegir «Eliminar», la tarjeta desaparece y el menú con ella: no hay salida que coordinar.

### Toasts

Los gestiona Sonner por dentro (entrada, apilado, deslizar para descartar, `prefers-reduced-motion`). No se tocan sus animaciones; solo las duraciones de permanencia: éxito 3 s, deshacer 5 s, error hasta cerrarlo (`docs/screens.md:279`, plan 006).

### Carga

- **Páginas y listas**: skeleton con la forma del contenido y `animate-pulse` (el de Tailwind, 2 s, el único bucle permitido además de los spinners). Nunca un spinner a pantalla completa (`docs/screens.md:18`).
- **Acción en curso dentro de un botón**: spinner `animate-spin` (lineal).
- **Paso de skeleton a contenido**: sin animación de entrada. Se cargan pantallas a diario, y un fundido en cada carga se sentiría lento (principio 5).

### Pulsar

Escala a 0.97 en 160 ms `ease-out`, y vuelta con la misma curva. Solo en `Button` (todas las variantes menos `link`) y en las paredes del editor (0.95). Tarjetas, navegación y pestañas no se encogen: tienen hover y selección de color.

### Momentos raros (no aprobados aún; la regla queda fijada)

Al terminar la solicitud y al confirmarse la señal: el check se dibuja (*line drawing*, ~600 ms) y el punto amarillo aparece al final, solapado con el último tramo (secuencia con solape: causa → efecto). El título y los botones no animan: la pantalla ya ha llegado.

## 6. Comportamiento responsive

| Anchura | Hoja | Toasts | Pulsar | Hover |
|---|---|---|---|---|
| < 768 px | Hoja inferior, desliza en vertical, se puede arrastrar para cerrar | Arriba, centrados | Sí | No (Tailwind v4 limita `hover:` a dispositivos con hover) |
| 768–1023 px | Panel lateral derecho, desliza solo en horizontal, sin arrastre | Arriba, centrados | Sí | Según dispositivo |
| ≥ 1024 px | No hay hoja: editor en línea (D2), sin movimiento | Abajo a la derecha | Sí | Sí |

Cada eje de la hoja solo se activa en su rango (`max-md:` / `md:`). Mezclarlos es lo que hoy produce la diagonal.

## 7. Accesibilidad

- `motion-fade-only` (clase en `globals.css`, fuera de `@layer` para ganar a las utilidades): con movimiento reducido anula el desplazamiento y la escala de `tw-animate-css` y fuerza el fundido. Va en toda capa que se desplace o escale.
- La escala al pulsar desaparece con `motion-reduce:active:scale-100`; el cambio de color se mantiene.
- Los skeletons dejan de latir (`motion-reduce:animate-none`), pero siguen visibles.
- El arrastre sigue funcionando (lo controla el usuario); la vuelta a su sitio es instantánea.
- Ninguna animación cambia el orden de foco ni lo retrasa; los textos de carga se anuncian una vez (`role="status"` + `sr-only`).
- Fuera de esta tanda: el scroll suave (`scrollIntoView`) debe pasar a instantáneo con movimiento reducido, y los spinners deben seguir una sola regla.

## 8. Rendimiento

- Solo `opacity`, `transform`, `translate` y `scale`. La barra de subida (`width`) queda anotada para una tanda posterior.
- Sin `transition-all`, sin `will-change` global y sin animaciones de layout.
- El arrastre escribe `translate` en línea sobre un único elemento, nunca una variable CSS en un padre (recalcularía estilos de todos los hijos).
- Sin `filter: blur` en transiciones.
- En Tailwind v4, `scale-*` y `translate-*` usan las propiedades `scale` y `translate`, no `transform`. Cualquier `transition-[…]` que deba animarlas tiene que nombrarlas: `transition-[…,scale]`. Por no hacerlo, las paredes del editor saltan hoy al pulsar.

## 9. Cómo añadir movimiento nuevo (lista de comprobación)

1. ¿Qué propósito tiene: orientar, dar respuesta o evitar un cambio brusco? Si es «queda bonito» en algo frecuente, no se anima.
2. ¿Cuántas veces al día se ve? Decenas: solo color. Ocasional: capa estándar. Raro: puede tener deleite.
3. ¿Qué es lo único que se mueve? El resto viaja con ello o se queda quieto.
4. Usa una constante de `motion.ts` o los tokens; no escribas curvas ni duraciones a mano.
5. Comprueba la interrupción: abre y cierra rápido, pulsa dos veces o cierra a mitad.
6. Comprueba el movimiento reducido (Rendering → `prefers-reduced-motion`) y 390 / 800 / 1440 px.
7. Graba la interacción y revísala fotograma a fotograma (DevTools → Animations al 10 %).

## 10. Decisiones técnicas que se piden aprobar

| Decisión | Alternativa descartada | Por qué |
|---|---|---|
| CSS + `tw-animate-css` + Radix, sin librería JS | Motion (Framer) o `vaul` para la hoja | Todo lo aprobado se expresa con keyframes y una transición; el arrastre son ~40 líneas. Añadir 30–60 kB no está justificado. |
| Redefinir `--ease-out` de Tailwind | Un nombre nuevo (`--ease-pinta-out`) | La utilidad `ease-out` pasa a ser la curva fuerte en toda la app; solo la usan hoy dos líneas del editor, y les sienta mejor. |
| Duraciones como variables de `:root` | Tokens de tema | Tailwind v4 no tiene espacio de tema para duraciones; `duration-(--x)` fija también `--tw-duration`, que lee `tw-animate-css`. Comprobado compilando con el Tailwind del proyecto. |
| `motion.ts` con cadenas de clases | Componentes envoltorio (`<AnimatedDialog>`) | Evita una abstracción nueva sobre Radix; los cinco diálogos siguen como están y solo cambian sus clases. |
| `motion-fade-only` fuera de capas | Variantes `motion-reduce:` en cada clase | Una regla para todas las capas, sin duplicar cada eje y breakpoint. |
| Hoja montada durante la salida con `setState` condicional en el render | `useEffect` + estado, o `onAnimationEnd` | Patrón documentado de React; sin efecto ni render extra visible. Si `eslint-plugin-react-hooks` 7 lo rechaza, el plan 002 para e informa. |
| Error en toast sin auto-cierre, mediante un `toast` propio en `@/lib/toast` | Fijar la duración en cada llamada | Sonner no admite duración por tipo en el `Toaster`; un único módulo y una regla de ESLint lo mantienen. |
| Sin animación del skeleton al contenido | Fundido de entrada | Frecuencia diaria (principio 5). |
| Sin transiciones de página en esta tanda | `<ViewTransition>` (React 19.3 / Next 16) | Es la herramienta correcta para los pasos del cotizador, pero es una oportunidad, no un hallazgo aprobado. Queda para después de comprobar que el `Link` de next-intl pasa `transitionTypes`. |
