# Pinta — Fase 6.6: PRD · Plan de construcción y demo

> Fuente: documento 6.6 del PRD de Pinta (https://claude.ai/artifact/Ua7VQ9ouDSmyPeeMoWwE3e). Si este archivo y el documento difieren, manda el documento.

## Cómo trabajar con Claude Code

El objetivo de este plan es que Claude Code construya Pinta en local, hito a hito, hasta la demo a los socios. Nathan trabaja solo; Claude Code es el equipo de desarrollo y Nathan es el revisor.

**Antes de empezar.** El repositorio lleva en la raíz un `CLAUDE.md` con las convenciones (arquitectura, reglas de módulos, código en inglés, cómo ejecutar y probar) y una carpeta `docs/` con este PRD en Markdown, un archivo por tema, más `docs/reference/pricing_reference.py`. Claude Code lee `CLAUDE.md` siempre; los documentos de `docs/` se le indican en cada prompt.

**Ritmo.** Sin fecha fija: se avanza hito a hito. Cada hito es una rama (`h1-pricing`, `h2-constructor`…) y se fusiona a `main` solo cuando cumple su criterio de salida y la definición de hecho.

**Ciclo por hito:**

1. **Prompt inicial**: el sugerido en el detalle del hito, que nombra los documentos de `docs/` que debe leer.
2. **Plan**: Claude Code propone el plan en modo plan; Nathan lo revisa y lo ajusta antes de escribir código.
3. **Código y tests**: por tareas pequeñas, cada una con sus tests y un commit. Nada se da por hecho sin `dotnet test` y `pnpm test` en verde.
4. **Revisión**: Nathan prueba el flujo en local con Aspire, revisa el diff y pide cambios. Si algo del PRD cambia, se actualiza primero el documento y después el código.
5. **Cierre**: se marca el hito en el Índice de Documentación y se fusiona.

**Reglas para no perder el rumbo:**

- Una sesión de Claude Code por hito, o por mitad de hito si es grande; al empezar una sesión nueva, se le pide leer `CLAUDE.md` y el estado del hito.
- El motor de precios no se toca sin que los casos dorados TC01–TC13 sigan en verde.
- Las migraciones de EF Core son una por cambio, con nombre descriptivo; nunca se editan migraciones ya fusionadas.
- Los secretos (claves de prueba de Stripe, Google OAuth) van en `dotnet user-secrets`, nunca en el repositorio.

## Hitos

H0 Cimientos → H1 Precios y catálogo → H2 Constructor → H3 Envío → H4 Revisión y estados → H5 Pagos → H6 Pintores → H7 Finanzas y área del cliente → H8 Pulido para la demo.

El orden sigue el recorrido de la demo: primero lo que ve el cliente al cotizar, después lo que hace Pinta con la solicitud y al final el dinero y el pintor. Cada hito deja algo que se puede enseñar en local. H5 y H6 son independientes entre sí y pueden intercambiarse; el resto depende del anterior. Al cerrar H2 ya se puede enseñar el constructor al socio, aunque el resto no exista.

## Detalle por hito

Las historias remiten al documento 6.2 y las rutas al 6.5. Cada prompt sugerido se pega tal cual al empezar la sesión del hito.

### H0 · Cimientos

- **Alcance**: solución .NET 10 con la estructura del documento 5.5 (AppHost de Aspire, API, SharedKernel, Pricing y los 9 módulos con su `.Contracts`); PostgreSQL, MinIO, Mailpit y Stripe CLI en Aspire; app Next.js con next-intl (pt, es, en), Tailwind, shadcn/ui y la paleta B; ProblemDetails, Serilog, health checks; Identity con cookie, antiforgery y roles; tests de arquitectura que hacen cumplir las reglas de módulos; GitHub Actions con build y tests; generación del cliente TypeScript desde OpenAPI.
- **Historias**: base técnica de S1–S4; registro y login mínimos.
- **Criterio de salida**: `dotnet run --project src/Pinta.AppHost` levanta todo; la web en `/pt` muestra una página con la paleta; un usuario se registra, confirma el email en Mailpit y entra; los tests de arquitectura fallan si un módulo referencia a otro que no sea su `.Contracts`.
- **Prompt**: «Lee CLAUDE.md, docs/architecture.md y docs/structure.md. Monta el hito H0 del docs/plan.md. Propón primero el plan.»

### H1 · Precios y catálogo

- **Alcance**: librería `Pinta.Pricing` pura y determinista; módulo Catalog con pinturas, superficies, ocupación, tallas y ajustes, con seed; endpoints `GET /api/catalog`, `POST /api/quotes/estimate`, `POST /api/admin/quotes/simulate` y CRUD de catálogo; pantalla A1 con simulador.
- **Historias**: A1.1–A1.8, C2.8 (solo API).
- **Criterio de salida**: los 13 casos dorados TC01–TC13 pasan con los números exactos de `docs/reference/pricing_reference.py`; cambiar una tarifa en A1 cambia el resultado del simulador.
- **Prompt**: «Lee CLAUDE.md, docs/pricing-engine.md y docs/reference/pricing\_reference.py. Implementa Pinta.Pricing con tests dorados primero (TDD) y después el módulo Catalog y la pantalla A1 de docs/screens.md.»

### H2 · Constructor del cliente

- **Alcance**: pantallas 0 a 4 (inicio, tipo, esquema, editor de estancia y estimación) con borrador local; componentes `RoomSchema` y `RoomEditor`; cálculo de área en el cliente; estimación en vivo; versión de escritorio D2. Es la pieza que tiene que enamorar: animaciones suaves al alternar paredes, respuesta inmediata y nada de pantallas en blanco.
- **Historias**: C1.1, C2.1–C2.8.
- **Criterio de salida**: se cotiza TC01 y TC09 de principio a fin en móvil y escritorio y el rango coincide con el del documento 6.3; recargar no pierde el borrador; funciona en los tres idiomas.
- **Prompt**: «Lee CLAUDE.md, docs/screens.md (pantallas 0–4 y escritorio) y docs/functional-requirements.md (C1–C2). Abre el canvas de diseño como referencia visual. Construye el constructor del hito H2.»

### H3 · Envío de la solicitud

- **Alcance**: módulo Media (URL firmada para subir, confirmación y límites); cobertura por código postal y lista de espera; pantallas 5 a 8; `POST /api/jobs` con idempotencia y código `PT-AAAA-NNNN`; estado `pending_email` hasta confirmar el email; emails de confirmación en los tres idiomas con Hangfire.
- **Historias**: C3.1–C3.2, C4.1–C4.5.
- **Criterio de salida**: un cliente nuevo envía una solicitud con 3 fotos y un vídeo de 80 MB, recibe el email en Mailpit, lo confirma y el trabajo pasa a `in_review`; enviar dos veces con la misma clave crea un solo trabajo.
- **Prompt**: «Lee CLAUDE.md, docs/api.md, docs/states.md y docs/screens.md (5–8). Implementa el hito H3.»

### H4 · Revisión y máquina de estados

- **Alcance**: máquina de estados de `Job` con la tabla de transiciones del documento 5.3 como única fuente; versiones de cotización con `snapshot_precios`; pantallas A2 y tablero de trabajos; borrador, confirmación con validez de 15 días, rechazo y visita técnica; historial; pantalla 10 del cliente con precio cerrado; caducidad automática con Hangfire.
- **Historias**: A2.1–A2.9, A3.1–A3.5, C5.1–C5.3, S1–S2.
- **Reparto acordado en H4**: lo que necesita módulos posteriores va a su hito. El pago de C5.3 llega en H5 (el botón «Aceptar y pagar» muestra «Pago disponible en breve»); asignar pintor (A3.2) y la visita técnica hecha por un pintor llegan en H6; el reembolso de A3.4 llega en H5 (en H4 se cancela y el reembolso calculado es 0 € porque aún no hay pagos). En H4 la visita técnica se asigna a un socio admin.
- **Criterio de salida**: el admin revisa TC10 (con partida extra), confirma y el cliente ve el precio cerrado con su desglose sin margen; cualquier transición no listada devuelve `invalid_transition`; un precio sin pagar caduca al día 16.
- **Prompt**: «Lee CLAUDE.md, docs/states.md, docs/data-model.md y docs/screens.md (A2, tablero, pantalla 10). Implementa el hito H4, con tests de cada transición válida e inválida.»

### H5 · Pagos

- **Alcance**: módulo Payments con Stripe Payment Element (tarjeta, MB WAY y Multibanco) en modo test; webhooks firmados e idempotentes; señal del 30% y saldo; reembolsos según la política de cancelación; reglas de importe para MB WAY; pantalla 11; facturas simuladas en PDF.
- **Historias**: C5.3, C5.5 (pago del resto), C5.6, A3.4 y S4.
- **Decisiones de H5**: la fecha programada la fija el admin al pasar «Señal pagada → Programado» (en H6 vendrá de la asignación del pintor); un justificante PDF simulado por cada pago (señal, resto y visita), marcado «Documento simulado, sin validez fiscal», con los datos de la empresa de la configuración y el NIF del cliente si lo dio; la visita técnica con precio se cobra en H5 y, si es descontable, se descuenta del subtotal del trabajo; la política de reembolso se aplica a la señal (la visita hecha no se reembolsa); un pago que llega para un trabajo cancelado o expirado se reembolsa entero y se avisa al admin. El pago del resto se prueba con tests de integración: «Finalizado» llega con los pintores en H6.
- **Criterio de salida**: con Stripe CLI, se paga la señal de TC01 con tarjeta de prueba y el trabajo pasa a `deposit_paid` solo tras el webhook; repetir el webhook no duplica nada; cancelar 3 días antes reembolsa el 50%.
- **Prompt**: «Lee CLAUDE.md, docs/integrations.md y docs/api.md (pagos y webhooks). Implementa el hito H5.»

### H6 · Pintores

- **Alcance**: invitación y alta; asignación con oferta y aceptación; pantallas P1 a P4; visita técnica con informe que vuelve a la revisión; empezar y terminar con fotos; pantalla de pintores del admin; IBAN cifrado.
- **Historias**: P1.1–P4.1, A4.1–A4.4.
- **Criterio de salida**: el admin invita a un pintor, este se da de alta, acepta TC01, lo empieza el día programado, lo termina con 3 fotos y el cliente las ve; el pintor no recibe en ninguna respuesta el precio al cliente ni el margen (test de contrato).
- **Prompt**: «Lee CLAUDE.md, docs/functional-requirements.md (P1–P4, A4) y docs/screens.md (Pintor). Implementa el hito H6.»
- **Reparto acordado en H6**: dos PR. H6a: alta e invitación de pintores, pantallas de pintores del admin, asignación con oferta, aceptación y rechazo, empezar y terminar con fotos y el cliente viéndolas (el criterio de salida). H6b: visita técnica hecha por un pintor con su informe (P3.1), pagos pendientes al pintor creados al cerrarse un trabajo y pantalla P4. Marcar esos pagos como hechos sigue en H7 (A5.4).
- **Decisiones de H6**: terminar exige al menos 3 fotos del resultado; el admin invita con nombre y email y puede rellenar el resto (NIF, teléfono, especialidades, zonas, IBAN, notas), y el pintor lo completa o corrige al darse de alta; la invitación vale 72 h; al aceptar la oferta, el pintor pasa el trabajo a «Programado» con la fecha de la oferta; las fotos de avance y «reportar incidencia» pasan a H8.
- **Decisiones de H6b**: el informe de la visita crea el borrador del admin (origen `site_visit`) con las medidas del pintor; las partidas extra que propone llegan con su pago al pintor y, si no son del catálogo, con el precio al cliente «por fijar», y confirmar se rechaza hasta que el admin lo pone. La visita no se paga al pintor en el MVP. Al cerrarse un trabajo se crea un pago pendiente al pintor por el importe fijado en la oferta aceptada; el admin lo ve en la ficha del pintor y marcarlo como pagado sigue en H7. El admin puede pedir la visita a un socio o a un pintor activo que cubra el concelho; «Visita hecha» del admin queda para las visitas de un socio.

### H7 · Finanzas y área del cliente

- **Alcance**: módulo Finance con costes operativos por categoría, pagos a pintores y cierre mensual con reparto 50/50; pantalla de finanzas; pantallas 9 y 12 del cliente; valoraciones; recordatorios con Hangfire; exportaciones CSV.
- **Historias**: A5.1–A5.4, C5.8, S3–S4.
- **Criterio de salida**: con los trabajos del seed, el cierre de octubre cuadra con un cálculo a mano en una hoja de cálculo; un mes cerrado no admite cambios.
- **Prompt**: «Lee CLAUDE.md, docs/functional-requirements.md (A5, S3–S4) y docs/screens.md (Finanzas, 9 y 12). Implementa el hito H7.»

### H8 · Pulido para la demo

- **Alcance**: seed de demo y botón de restablecer (las fechas se calculan desde hoy); repaso de escritorio D1–D3; PWA instalable; repaso de accesibilidad y de los tres idiomas; estados vacío, carga y error en todas las pantallas; ensayo del guion de la demo.
- **Criterio de salida**: el guion de la demo se hace dos veces seguidas, sin errores, tras un restablecimiento; Lighthouse ≥ 90 en rendimiento y accesibilidad en la pantalla 2 en móvil.
- **Prompt**: «Lee CLAUDE.md y docs/plan.md (seed y guion). Prepara el hito H8 y ensáyame el guion paso a paso.»

## Definición de hecho

Una historia está hecha cuando cumple todo esto; un hito, cuando todas sus historias lo cumplen y además su criterio de salida.

- [ ] Cumple los criterios de aceptación de su historia en el documento 6.2.
- [ ] Tiene tests: unitarios para reglas de dominio, de integración con Testcontainers para endpoints y, en el motor de precios, los casos dorados.
- [ ] `dotnet test`, `pnpm lint`, `pnpm typecheck` y `pnpm test` pasan en local y en CI; los tests de arquitectura siguen en verde.
- [ ] El endpoint está en OpenAPI, el cliente TypeScript se ha regenerado y los errores usan los `code` del documento 6.5.
- [ ] La pantalla tiene sus estados vacío, carga y error y funciona a 390 px y a 1440 px.
- [ ] Todos los textos visibles están en pt, es y en; ningún texto en duro en los componentes.
- [ ] Accesible: etiquetas, foco visible, navegación con teclado y contraste AA.
- [ ] Seguridad: el endpoint comprueba rol y propiedad del recurso; el pintor y el cliente no reciben campos que no les tocan; sin secretos en el código.
- [ ] Si cambia el esquema, hay migración de EF Core y el seed sigue funcionando.
- [ ] Si el comportamiento difiere del PRD, se ha actualizado antes el documento correspondiente.

### Calidad que se revisa en cada hito

| Aspecto | Comprobación |
| --- | --- |
| Dinero | Nunca `double`; importes en céntimos en la API; redondeo `AwayFromZero` por línea. |
| Fechas | Guardadas en UTC; mostradas y programadas en hora de Lisboa. |
| Concurrencia | Token de concurrencia en `Job` y en el catálogo; conflicto → `concurrency_conflict`. |
| Idempotencia | POST de creación y de pagos con `Idempotency-Key`; webhooks con tabla `EVENTO_STRIPE`. |
| Logs | Serilog estructurado con `traceId`; sin datos personales ni IBAN en los logs. |
| Rendimiento | Estimación < 200 ms en local; listas paginadas; imágenes con miniaturas. |

## Datos de demo

El seed vive en `src/Pinta.Api/Seed/` y se ejecuta al arrancar en `Development` si la base está vacía, o desde Ajustes → «Restablecer datos de demo». Todas las fechas se calculan relativas a hoy (`D` = día del restablecimiento), así la demo siempre está al día. Las personas son ficticias. Contraseña común de demo: `Pinta-Demo-2026`, solo en local.

### Catálogo

El de la configuración semilla del documento 6.3 (cuatro pinturas, cinco superficies, ocupación, tallas y reglas), con nombres en pt, es y en. Cobertura: los 16 concelhos del distrito de Lisboa activos. Los prefijos postales de cada concelho de la semilla son aproximados (los principales de cada uno) y están **pendientes de revisar** antes de la demo.

### Usuarios

| Rol | Nombre | Email | Notas |
| --- | --- | --- | --- |
| admin | Nathan | `nathan@demo.pinta.pt` | Socio |
| admin | Socio pintor | `socio@demo.pinta.pt` | Mismos permisos |
| painter | Rui Almeida | `rui@demo.pinta.pt` | Zonas Lisboa, Oeiras y Amadora; valoración 4,8 |
| painter | Carla Santos | `carla@demo.pinta.pt` | Zonas Sintra y Cascais; valoración 4,6 |
| painter | João Ferreira | — | Invitado, sin alta (para enseñar la invitación) |
| client | Ana Costa | `ana@demo.pinta.pt` | Cliente particular |
| client | Miguel Rocha | `miguel@demo.pinta.pt` | Particular, varios trabajos |
| client | Lisboa Rent, Lda. | `gestao@demo.pinta.pt` | B2B con NIF en factura |

### Trabajos

Uno por cada estado relevante, para poder enseñar cualquier pantalla sin preparar nada. Los importes salen del motor con los casos del documento 6.3.

| Código | Cliente | Caso | Estado | Fechas y detalle |
| --- | --- | --- | --- | --- |
| PT-2026-0101 | Ana | TC01 | `in_review` | Enviado D−1, con 4 fotos; listo para revisar en vivo |
| PT-2026-0102 | Miguel | TC09 | `in_review` | Enviado D−2; el semáforo de plazo en ámbar |
| PT-2026-0103 | Lisboa Rent | TC11 | `site_visit` | Premium; visita con Rui en D+2 |
| PT-2026-0104 | Ana | TC04 | `quoted` | Precio confirmado en D−1, válido hasta D+14; para pagar la señal en vivo |
| PT-2026-0105 | Miguel | TC07 | `deposit_paid` | Señal con MB WAY; pendiente de asignar |
| PT-2026-0106 | Lisboa Rent | TC10 | `scheduled` | Asignado a Carla; empieza en D (para «Empezar trabajo» en vivo) |
| PT-2026-0107 | Miguel | TC06 | `in_progress` | Rui, empezó en D−1, fotos de avance |
| PT-2026-0108 | Ana | TC02 | `finished` | Rui, con fotos del resultado; saldo pendiente y valoración por hacer |
| PT-2026-0091 | Lisboa Rent | TC08 | `closed` | Mes anterior; pagado y valorado con 5 estrellas |
| PT-2026-0092 | Miguel | TC05 | `closed` | Mes anterior; mínimo aplicado |
| PT-2026-0093 | Ana | TC03 | `cancelled` | Cancelado 3 días antes: reembolso del 50% |
| PT-2026-0094 | Miguel | TC01 | `expired` | Precio sin pagar en 15 días |
| PT-2026-0095 | Lisboa Rent | TC12 | `rejected` | Fachada fuera del máximo; motivo «requiere presupuesto a medida» |

### Finanzas

Mes anterior con dos trabajos cerrados, costes operativos de ejemplo (alojamiento 25 €, dominio y correo 6 €, comisiones de Stripe reales de los pagos de prueba) y los pagos a Rui marcados como hechos, para enseñar un mes cerrado. Mes actual abierto, con los pagos a pintores pendientes.

## Guion de la demo

Unos 25 minutos para el socio, con el móvil del cliente (navegador en modo móvil o un teléfono en la misma red) y el portátil con el admin. Antes: restablecer los datos de demo, abrir Mailpit y la consola de Stripe en modo test, y tener tres pestañas con sesiones de cliente, admin y pintor.

| Min. | Quién y dónde | Qué se enseña | Mensaje clave |
| --- | --- | --- | --- |
| 0–2 | Cliente · pantalla 0 | Landing en portugués, cambio a español, comprobar el código postal de Sintra | Mobile-first y en tres idiomas |
| 2–9 | Cliente · pantallas 1–4 | Vivienda con muebles: sala por talla, dormitorio con medidas, quitar una pared, baño con humedad. El rango se mueve en vivo | El constructor: cualquiera cotiza en dos minutos sin saber de m² |
| 9–12 | Cliente · pantallas 5–8 | Fotos desde el móvil, dirección y fecha, cuenta nueva, código de solicitud y email en Mailpit | La solicitud llega completa: menos visitas y menos llamadas |
| 12–16 | Admin · A2 | Abrir la solicitud recién enviada: fotos, desglose interno y margen. Añadir una partida extra, confirmar el precio | Revisión humana en minutos, con el margen siempre a la vista |
| 16–18 | Admin · A1 | Subir la tarifa de techos, ver el simulador y volver a dejarla | Los precios son del negocio, no del código |
| 18–20 | Cliente · pantallas 10–11 | Precio cerrado frente a estimación; pagar la señal con MB WAY de prueba y ver el estado cambiar | Cobramos antes de mover a nadie |
| 20–23 | Admin y pintor · P1–P2 | Asignar a Rui; Rui acepta en su móvil. Con PT-2026-0106: «Empezar trabajo» y terminar con fotos | El pintor solo ve lo que necesita y su pago |
| 23–25 | Admin · Finanzas | Mes anterior cerrado: cobrado, material, pintores, costes y reparto 50/50 | Cuentas claras entre socios desde el primer día |

**Preguntas para cerrar con el socio:** ¿las tarifas y los factores se parecen a lo que él cobra hoy?, ¿qué le falta al panel del pintor para que su equipo lo use?, ¿qué trabajos reales podríamos pasar por Pinta en el piloto?

**Plan B:** si falla Stripe o la red, se enseña el pago con PT-2026-0105, ya pagado, y el webhook se reenvía después con Stripe CLI. Si falla la subida de vídeo, se sigue solo con fotos.
