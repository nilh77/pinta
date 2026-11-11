# Sistema de movimiento

Reglas de movimiento del frontend. Los tokens viven en `web/src/app/globals.css` y las clases compartidas en `web/src/components/ui/motion.ts`.

## 1. Personalidad

Pinta vende confianza: un precio claro, una persona que revisa y un pintor elegido con cuidado. El movimiento tiene que ser **preciso y vivo, con una pincelada**: se nota, pero nunca estorba.

- **Preciso**: responde enseguida y nunca hace esperar. Es una herramienta que el cliente usa para describir su casa y el admin para trabajar todo el día.
- **Vivo**: las capas llegan con un resorte que se asienta (rebote 0.3) y los botones responden con cuerpo: se hunden al tocarlos y vuelven con un pequeño rebote al soltar. Nada oscila en bucle, gira ni cae en cascada dentro de la interfaz de trabajo; los cambios de estado se siguen contando con color y opacidad.
- **Una pincelada**: el amarillo #F2B705 marca el gesto de «pintar» (paredes que se activan, el check de «solicitud enviada» y de «señal pagada»). Es el único sitio con margen para algo de deleite, y por eso se reserva a momentos raros.

## 2. Principios

1. **Primero la respuesta.** Todo lo que el usuario toca responde en ≤ 120 ms. Ninguna animación retrasa la acción ni el foco. Radix mueve el foco al montar, sin esperar a la animación.
2. **Cada capa viene de un sitio y vuelve a él** (*origin-aware animation*). Un menú crece desde su botón, una hoja sale del borde del que vino y vuelve a él, y un diálogo aparece en el centro. La salida es la inversa de la entrada, más corta.
3. **Una entrada por contenedor.** Si la capa se mueve, su contenido viaja con ella (*carry*). Nunca hay escalonado dentro de hojas, diálogos, menús, listas ni pestañas.
4. **Estado con color y opacidad, no con movimiento.** Seleccionar, activar o marcar un valor desactualizado cambia color u opacidad (`transition-colors`, opacidad 50–60 %). Así se hace ya hoy y se mantiene.
5. **La frecuencia manda.** Lo que se usa decenas de veces al día (navegación, pestañas, idioma, filas, resaltado de menú, teclado) no anima o solo cambia de color. Los momentos raros pueden durar más.
6. **Movimiento reducido = fundido.** Con `prefers-reduced-motion: reduce` se quita el desplazamiento y la escala y se conserva la opacidad. No se apaga todo (*reduced motion*, no *no motion*).
7. **Solo propiedades del compositor.** Se animan `opacity`, `transform`, `translate` y `scale`. Nunca `transition-all` ni propiedades de layout (`width`, `height`, `top`, `margin`).
8. **Sin dependencias nuevas.** CSS + `tw-animate-css` (ya instalado) + la presencia de Radix (mantiene montado el contenido hasta que acaba `animate-out`). Una librería JS solo entraría por un gesto o una secuencia que CSS no pueda expresar, y hoy no hay ninguna.

## 3. Tokens

Viven en `web/src/app/globals.css`. Las curvas son tokens de tema de Tailwind: generan `ease-out`, `ease-in-out`, `ease-drawer` y `ease-spring`, que fijan `--tw-ease`, la variable que lee `tw-animate-css`. Las duraciones son variables de `:root` y se usan con `duration-(--duration-…)`.

### Curvas

| Token | Valor | Uso |
|---|---|---|
| `--ease-out` | `cubic-bezier(0.23, 1, 0.32, 1)` | Salidas, velos, el hundimiento al pulsar. Curva por defecto. |
| `--ease-spring` | `linear(0, 0.0337, 0.1179, 0.2308, 0.3562, 0.482, 0.6001, 0.7056, 0.7957, 0.8698, 0.9283, 0.9726, 1.0043, 1.0255, 1.0383, 1.0445, 1.046, 1.044, 1.0399, 1.0346, 1.0288, 1.023, 1.0177, 1.0129, 1.0089, 1.0056, 1.003, 1.001, 0.9996, 0.9987, 0.9982, 0.9979, 1)` | Entradas de menús y diálogos, y la vuelta al soltar un botón. Resorte críticamente amortiguado al 70 % (rebote 0.3): alcanza el destino al 38 % de la duración, se pasa un 4,6 % y se asienta. Es CSS puro (`linear()`), sin librería. |
| `--ease-in-out` | `cubic-bezier(0.77, 0, 0.175, 1)` | Algo que ya está en pantalla y se desplaza de A a B (p. ej. una futura píldora deslizante en pestañas). |
| `--ease-drawer` | `cubic-bezier(0.32, 0.72, 0, 1)` | Hojas: entrada, salida y vuelta tras arrastrar. Sin resorte: un panel de altura completa que rebota dejaría un hueco en el borde. |
| Tailwind por defecto | `cubic-bezier(0.4, 0, 0.2, 1)`, 150 ms | Cambios de color (`transition-colors`). Se mantiene: ya está en ~20 sitios y es correcto para color. |
| `linear` | — | Solo spinners y progreso continuo. |

`ease-in` está prohibido en la interfaz.

### Duraciones

| Token | Valor | Uso |
|---|---|---|
| `--duration-press` / `--duration-release` | 120 / 360 ms | Hundirse al pulsar (`ease-out`) / volver al soltar (`ease-spring`). |
| `--duration-menu-in` / `-out` | 260 / 140 ms | Menús desplegables. |
| `--duration-layer-in` / `-out` | 340 / 180 ms | Diálogos centrados y su velo. |
| `--duration-drawer-in` / `-out` | 380 / 240 ms | Hojas y su velo. |
| (local) | 600 ms | Momentos raros (check dibujado). Se define donde se usa, no como token. |

Con `ease-spring`, la duración CSS incluye el asentamiento: el elemento **llega** al 38 % (menú ≈ 100 ms, diálogo ≈ 130 ms, soltar ≈ 140 ms) y el resto es el pequeño rebote, que no bloquea nada. Por eso esas duraciones superan los 300 ms sin hacer esperar. La hoja (380 ms) está dentro de los 200–500 ms que el catálogo admite para cajones. Las salidas son siempre más cortas y con `ease-out`: el usuario ya decidió irse.

### Distancias y escalas

| Qué | Valor |
|---|---|
| Entrada de menús | escala 0.9 + 8 px desde el lado del botón (`zoom-in-90`, `slide-in-from-*-2` según `data-side`). Salida: escala 0.95 + fundido. |
| Entrada de diálogos | escala 0.9 + 16 px de subida (`zoom-in-90`, `slide-in-from-bottom-4`). Salida: escala 0.95 + fundido, sin desplazamiento. |
| Escala al pulsar | 0.96 en botones, 0.92 en las paredes (objetivo pequeño). Nunca `scale(0)`. |
| Desplazamiento de hojas | 100 % de su propio tamaño (`slide-*`): nunca píxeles fijos. |

## 4. Capas compartidas

Las cadenas de clases viven en `web/src/components/ui/motion.ts`. Es la única abstracción: cinco constantes de texto y un helper, `overlayStyle(open)`, que deja pasar los toques a través del velo mientras se desvanece (Radix fija `pointer-events: auto` en línea y solo cede ante el `style` recibido). Sin componente envoltorio ni hook.

| Constante | Para | Relación entre velo y contenido |
|---|---|---|
| `overlayMotion` | Velo de los diálogos | Juntos (*together*): mismo inicio y duración que `dialogMotion`. |
| `dialogMotion` | Contenido de diálogos centrados | Fundido + escala 0.9 + subida de 16 px con resorte, desde el centro (el centro es el origen correcto en un modal). |
| `menuMotion` | `DropdownMenu.Content` | Fundido + escala 0.9 + 8 px desde el lado del botón, con resorte, origen en `--radix-dropdown-menu-content-transform-origin`. |
| `drawerOverlayMotion` | Velo de la hoja | Juntos con `drawerMotion`: misma curva y duración. |
| `drawerMotion` | Contenido de la hoja | Desliza en un solo eje por anchura (ver §6). |

Todas incluyen `motion-fade-only` (§7), salvo los velos, que ya solo hacen fundido.

### Material de cristal

La cabecera, la barra lateral del admin y las capas (menús, diálogos, hoja) son de cristal esmerilado sobre un fondo fijo con dos brillos suaves (`canvas-backdrop` en `body`: el verde azulado de la marca arriba a la izquierda y ese tono girado 60° abajo a la derecha). Sobre un fondo liso el cristal se vería gris; los brillos le dan algo que desenfocar.

- `glass` (utilidad en `globals.css`): `backdrop-filter: blur() saturate()`, borde transparente y el filo como **sombras interiores** (un brillo arriba y un hilo tono sobre tono), compatibles con `shadow-*` y `ring-*`.
- Siempre con un relleno translúcido de token: `bg-glass-chrome` (cabecera y barra lateral, 48 %), `bg-glass-control` (controles redondos con texto o icono, 55 %, nunca más transparente) y `bg-glass-fill` (capas, 64 %, con el blur de capa de 14 px). Sombra de la cabecera: `shadow-chrome`.
- El cristal no añade color propio: el que se ve viene de lo que hay detrás.
- Con `prefers-reduced-transparency: reduce` los rellenos suben al 92 % y el blur a 20 px: casi opacos.
- Contraste: el texto sobre cualquier cristal cumple 4,5:1 medido sobre la parte más clara del fondo.
- El proyecto solo tiene modo claro; los tokens de `.dark` están preparados para cuando exista.

## 5. Coreografía por evento

Línea temporal de cada evento con varias piezas en movimiento (formato de `choreograph`). Lo que no aparece en una tabla no se mueve.

### Abrir la hoja del editor (< 1024 px)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Panel de la hoja | Protagonista | 0 ms | 380 ms | desliza desde el borde (abajo < 768; derecha 768–1023) | — |
| Velo | Decoración | 0 ms | 380 ms | fundido | Juntos con el panel |
| Contenido (paredes, medidas, pinturas) | Estático | — | — | viaja con el panel | *Carry*: una entrada por contenedor |
| Tarjeta tocada en el esquema | Estático | — | — | anillo de selección inmediato (`ring`) | — |

### Cerrar la hoja (Guardar, Cancelar, X, Escape, velo o arrastre)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Panel | Protagonista | 0 ms (o desde donde quedó tras arrastrar) | 240 ms | vuelve al borde por el que entró | Inversa de la entrada |
| Velo | Decoración | 0 ms | 240 ms | fundido | Juntos |
| Tarjeta guardada | Apoyo | 0 ms | 150 ms | color (borde, fondo, check) | Juntos: la casa se «pinta» mientras la hoja se va |

### Arrastrar la hoja (< 768 px)

Durante el arrastre, el panel sigue al dedo 1:1 sin transición; hacia arriba tiene resistencia creciente (*rubber-banding*). Al soltar, cierra si la velocidad es > 0.11 px/ms o si se arrastró más del 25 % de su altura, y la salida parte de la posición actual. Si no cierra, vuelve con `ease-drawer`. El velo no sigue al dedo.

### Diálogo centrado (confirmar, formulario, renombrar, sesión caducada)

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Diálogo | Protagonista | 0 ms | 340 ms (`ease-spring`) / 180 ms (`ease-out`) | entra: fundido + escala 0.9 → 1 + sube 16 px; sale: fundido + escala → 0.95 | — |
| Velo | Decoración | 0 ms | 340 / 180 ms (`ease-out`) | fundido | Juntos |
| Campos y botones | Estático | — | — | viajan con el diálogo | *Carry* |
| Hoja de debajo (renombrar desde la hoja) | Estático | — | — | — | No retrocede ni se oscurece aparte |

### Visor de fotos (admin)

Solo fundido al abrir (340 ms, `ease-out`). El cierre es instantáneo a propósito: el contenido se vacía al cerrar y una salida animada mostraría un fondo negro vacío.

### Menú ⋯ y menú «Añadir»

Un solo elemento en movimiento: el menú (entra en 260 ms con resorte desde su botón; sale en 140 ms). Las opciones aparecen todas a la vez y el resaltado es instantáneo (se recorre con teclado). Al elegir «Eliminar», la tarjeta desaparece y el menú con ella: no hay salida que coordinar.

### Abrir un control de la cabecera (idioma, cuenta)

El panel no aparece como una caja aparte: **crece desde el círculo de su control** y, al cerrarse, **vuelve a él**. Los vecinos se apartan en el mismo movimiento (*continuity transition*).

| Elemento | Rol | Empieza | Duración | Movimiento | Relación |
|---|---|---|---|---|---|
| Panel | Protagonista | 0 ms | 380 ms (`ease-out`) + 640 ms de asentamiento (`ease-spring`) | `clip-path` desde el rectángulo y radio exactos del control hasta el panel completo (2 rem más allá, para no cortar la sombra); escala 0.94 → 1 | — |
| Control abierto | Apoyo | 0 ms | 200 ms | pierde su propia superficie (fondo, sombra y blur): queda como el icono de la esquina del panel | Juntos |
| Controles anteriores | Apoyo | 0 ms | 240 ms (`ease-out`) | se deslizan juntos a la izquierda hasta quedar a 8 px del panel | Juntos: llegan cuando el recorte parece terminado (~180 ms) |
| Controles posteriores | Apoyo | 0 ms | 240 ms | a la derecha si el panel los tapa y hay sitio | Juntos |
| Piezas tapadas sin sitio (móvil) | Decoración | 0 ms | 200 ms | se desvanecen (`data-morph-yield`) | Juntos |

Al cerrar, el panel vuelve en 220 ms y los vecinos regresan en esos mismos 220 ms. Pasar de un panel abierto a otro control abre el siguiente sin cerrar y reabrir (menús no modales); un control que aún vuelve a su sitio lleva el panel nuevo con él. Mientras un panel está abierto o volviendo, la cabecera sube por encima de las capas (`z-index: 60`).

Implementación: `MorphMenuContent` (`components/ui/dropdown-menu.tsx`) con el hook `useTriggerMorph` (`lib/use-trigger-morph.ts`); el CSS de `.morph-popover` y `[data-morph-cluster]` está en `globals.css`, fuera de capas. Con movimiento reducido: fundidos de 120 ms y vecinos sin transición.

### Toasts

Los gestiona Sonner por dentro (entrada, apilado, deslizar para descartar, `prefers-reduced-motion`). No se tocan sus animaciones; solo las duraciones de permanencia: éxito 3 s, deshacer 5 s, error hasta cerrarlo (`docs/screens.md`, estados globales).

### Carga

- **Páginas y listas**: skeleton con la forma del contenido y `animate-pulse` (el de Tailwind, 2 s, el único bucle permitido además de los spinners). Nunca un spinner a pantalla completa (`docs/screens.md:18`). Componentes en `web/src/components/ui/skeleton.tsx`: `CardListSkeleton` para listas y `DetailSkeleton` para páginas de detalle.
- **Acción en curso dentro de un botón**: `<Button pending>`. El texto se vuelve transparente sin moverse y el spinner aparece en su centro, así que el botón no cambia de tamaño. Con movimiento reducido el spinner gira más despacio (2 s) en vez de pararse: un spinner quieto ya no dice «cargando».
- **Procesos que no son una carga de página** (preparar o esperar la confirmación de un pago, confirmar el email, buscar pintores): spinner con texto que explica qué ocurre.
- **Paso de skeleton a contenido**: sin animación de entrada. Se cargan pantallas a diario, y un fundido en cada carga se sentiría lento (principio 5).

### Pulsar

Tiempo asimétrico: se hunde a 0.96 en 120 ms `ease-out` al tocar y vuelve en 360 ms con `ease-spring` al soltar (un pequeño rebote por encima de 1). Solo en `Button` (todas las variantes menos `link`) y en las paredes del editor (0.92). El color sigue su propia transición (150 ms), sin resorte. Se aplica con la utilidad CSS `pressable` de `globals.css`; la profundidad se ajusta con `--press-scale` (por defecto 0.96; `[--press-scale:0.92]` en las paredes, `[--press-scale:1]` en `link`). Con movimiento reducido no escala. Tarjetas, navegación y pestañas no se encogen: tienen hover y selección de color.

### Momentos raros (regla para cuando se implementen)

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
- Transparencia reducida: los cristales pasan a casi opacos (rellenos al 92 %).
- Ninguna animación cambia el orden de foco ni lo retrasa; los textos de carga se anuncian una vez (`role="status"` + `sr-only`).
- Fuera de esta tanda: el scroll suave (`scrollIntoView`) debe pasar a instantáneo con movimiento reducido, y los spinners deben seguir una sola regla.

## 8. Rendimiento

- Solo `opacity`, `transform`, `translate` y `scale`. La barra de subida (`width`) queda anotada para una tanda posterior.
- Sin `transition-all`, sin `will-change` global y sin animaciones de layout.
- El arrastre escribe `translate` directamente en `ref.current.style` de un único elemento: sin `setState` por fotograma (React re-renderizaría 60–120 veces por segundo) y nunca una variable CSS en un padre (recalcularía estilos de todos los hijos).
- Sin `filter: blur` en transiciones. El `backdrop-filter` del cristal es estático (no se anima) y solo está en la cabecera, la barra lateral y las capas abiertas.
- El fondo con brillos es un pseudo-elemento fijo, no `background-attachment: fixed`, para que el scroll no lo repinte.
- `ease-spring` usa `linear()` de CSS (Chrome 113+, Safari e iOS 17.2+, Firefox 112+). Sin soporte, la animación que lo usa queda inválida y la capa aparece sin animar: no se rompe nada.
- En Tailwind v4, `scale-*` y `translate-*` usan las propiedades `scale` y `translate`, no `transform`. Cualquier `transition-[…]` que deba animarlas tiene que nombrarlas: `transition-[…,scale]`. Por no hacerlo, las paredes del editor saltan hoy al pulsar.

## 9. Cómo añadir movimiento nuevo (lista de comprobación)

1. ¿Qué propósito tiene: orientar, dar respuesta o evitar un cambio brusco? Si es «queda bonito» en algo frecuente, no se anima.
2. ¿Cuántas veces al día se ve? Decenas: solo color. Ocasional: capa estándar. Raro: puede tener deleite.
3. ¿Qué es lo único que se mueve? El resto viaja con ello o se queda quieto.
4. Usa una constante de `motion.ts` o los tokens; no escribas curvas ni duraciones a mano.
5. Comprueba la interrupción: abre y cierra rápido, pulsa dos veces o cierra a mitad.
6. Comprueba el movimiento reducido (Rendering → `prefers-reduced-motion`) y 390 / 800 / 1440 px.
7. Graba la interacción y revísala fotograma a fotograma (DevTools → Animations al 10 %).
