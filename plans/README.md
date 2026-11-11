# Planes de animación

Salen de la auditoría `improve-animations` (modo `deep`) del 2026-11-11, sobre el commit `02c3f9b`. Cada plan es autocontenido: lo puede ejecutar cualquier agente sin el contexto de la conversación. Si el código ha cambiado desde el commit indicado, el plan dice que hay que parar e informar.

**Empieza por [000 — Sistema de movimiento](000-motion-system.md)**: principios, tokens, capas compartidas, coreografía por evento, comportamiento responsive, accesibilidad, rendimiento y las decisiones técnicas que hay que aprobar. Los planes 001–009 lo implementan y no pueden contradecirlo. El plan 001 lo publica como `docs/motion.md`.

Dirección: **preciso y vivo, con una pincelada** (revisión 2 del sistema: se nota, pero nunca estorba).

| # | Plan | Severidad | Depende de | Estado |
|---|---|---|---|---|
| 000 | [Sistema de movimiento (especificación)](000-motion-system.md) | — | — | APROBADO |
| 001 | [Base de movimiento: tokens, `motion-fade-only`, `motion.ts`](001-motion-foundation.md) | LOW (requisito) | — | DONE |
| 002 | [Hoja del editor: se ve su salida](002-sheet-exit-animation.md) | HIGH | — | DONE |
| 003 | [Hoja: un eje por breakpoint, curva de cajón, fundido reducido](003-sheet-axis-and-curve.md) | HIGH | 001 | DONE |
| 004 | [Menús desde su botón, con salida](004-dropdown-menu-origin.md) | MEDIUM | 001 | DONE |
| 005 | [Diálogos con entrada y salida](005-dialogs-enter-exit.md) | MEDIUM | 001 | TODO |
| 006 | [Toasts con las duraciones del documento](006-toast-durations.md) | MEDIUM | — | TODO |
| 007 | [Skeletons en área admin y listas](007-skeleton-loading.md) | MEDIUM | — (alcance confirmado: admin y listas) | TODO |
| 008 | [Feedback de pulsación en botones y paredes](008-press-feedback.md) | MEDIUM | 001 | TODO |
| 009 | [Arrastrar para cerrar la hoja](009-sheet-drag-to-dismiss.md) | MEDIUM | 001, 002, 003 | TODO |

> **Revisión 2 del sistema (2026-11-11, «vivo y funcional»)**: 000 y 001 ya están actualizados. Los valores citados en el texto de 002–009 (duraciones, escalas) se actualizan a la revisión 2 justo antes de ejecutar cada plan; el código usa siempre las constantes de `motion.ts` y los tokens, así que manda el sistema.

## Orden recomendado

0. ~~Aprobar 000~~: aprobado el 2026-11-11.
1. **001**: desbloquea 003, 004, 005, 008 y 009, y publica `docs/motion.md`.
2. **002** y **003**: la hoja es la superficie más usada del constructor. 002 no depende de 001 y puede ir en paralelo. Tocan archivos distintos (`schema-step.tsx` y `sheet.tsx`).
3. **004**, **005** y **008**: archivos distintos; pueden ir en paralelo tras 001. Ojo: 005 también toca `schema-step.tsx` (`RenameDialog`), así que no lo ejecutes a la vez que 002 ni que 006.
4. **006**: independiente, pero cambia una importación en `schema-step.tsx`; ejecútalo después de 002.
5. **007**: independiente; alcance confirmado el 2026-11-11 (área admin y las seis listas).
6. **009**: el último. Necesita que la hoja ya salga bien.

Todo va en una rama propia (propuesta: `motion-system`), no en `main`. Cada plan es un commit propio con su ID de historia si aplica (`CLAUDE.md`: tareas pequeñas, cada una con tests y su commit) y una entrada en `CHANGELOG.md` bajo **Sin publicar**.

## Fuera de esta tanda (hallazgos LOW y oportunidades)

- Scroll suave sin respetar `prefers-reduced-motion`: `web/src/components/quote/schema-step.tsx:97`, `web/src/components/admin/review/review-screen.tsx:30`.
- Un único `Spinner` con una sola regla de movimiento reducido (hoy hay 20 de 60 sin `motion-reduce:`).
- Barra de subida con `scaleX` en vez de `width`: `web/src/components/quote/photos-step.tsx:251`.
- Skeletons en las pantallas de detalle (continuación de 007).
- Oportunidades: dibujar el check en «enviado» (`sent-step.tsx:50-55`) y en «señal pagada» (`payment-screen.tsx:286-288`); entrada y salida de tarjetas del esquema; pasos del cotizador con `<ViewTransition>` (antes, comprobar que el `Link` de next-intl pasa `transitionTypes`); píldora deslizante en los selectores de pestañas.
