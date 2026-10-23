# Changelog

Registro de los cambios de Pinta. Cada push añade sus cambios en **Sin publicar**; al fusionar un hito en `main`, esa sección pasa a llevar el nombre del hito, la fecha y el PR.

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). Secciones por entrada: **Añadido**, **Cambiado**, **Corregido**, **Eliminado**. Cada línea dice qué cambia para quien usa o desarrolla Pinta y, si aplica, la historia (`A1.8`) o el documento afectado.

## Sin publicar

### Corregido

- Un cuerpo JSON que no se puede leer (mal formado o con un valor de enumeración desconocido, p. ej. `"occupancy": "abandoned"`) responde 400 `bad_request` como ProblemDetails en lugar de 500, y ya no se registra como error del servidor. El frontend tiene texto para `bad_request` en los tres idiomas.

## H6b · Visita técnica del pintor y pagos al pintor — 2026-10-23, [PR #8](https://github.com/nilh77/pinta/pull/8)

### Añadido

- Visita técnica hecha por un pintor: «Pedir visita» ofrece los socios y los pintores activos que cubren el concelho; el pintor recibe un email sin el precio de la visita y la ve en «Próximos» con la etiqueta «Visita» (A2.6).
- P3 · `/pintor/visitas/{id}`: lo que indicó el cliente, las medidas reales corregidas con el editor de estancia del constructor (añadir o quitar estancias), trabajos extra con el pago al pintor, fotos directas a almacenamiento y una nota. El informe sin enviar se guarda en el móvil. Al enviarlo, el trabajo vuelve a «En revisión» con nuevo plazo, sus medidas pasan a ser el borrador `site_visit` y los admins reciben un email (P3.1).
- Admin: panel «Informe de la visita» con la nota y las fotos; las partidas libres propuestas en la visita llegan con el pago al pintor y el precio al cliente «por fijar», y confirmar el precio se bloquea (422 `extras`) hasta ponerlo.
- Pagos al pintor: al cerrarse un trabajo con el pago del resto se crea un pago pendiente por el importe de la oferta aceptada, uno por trabajo aunque el webhook se repita (`finance.painter_payouts`). `GET /api/painter/payouts` y `GET /api/admin/payouts` (solo lectura) (A5.4, primera mitad).
- P4 · `/pintor/pagos`: lo pendiente, lo pagado en el mes elegido, la lista por trabajo y el IBAN enmascarado, que se cambia con la contraseña (P4.1). Submenú Trabajos / Pagos en la zona del pintor.
- Ficha del pintor en el admin: sección «Pagos al pintor» de solo lectura.
- Test de contrato ampliado: `/api/painter/visits/{id}` no lleva ningún importe y `/api/painter/payouts` no lleva precio al cliente ni margen.

### Cambiado

- Documentos: decisiones de H6b en `docs/plan.md`, P3, P4 y A2 en `docs/screens.md`, rutas y cuerpos en `docs/api.md`, informe de visita y pago al pintor en `docs/data-model.md`, y la transición del pintor en `docs/states.md`.
- Las fotos de resultado (P2) y de visita (P3) usan el mismo selector de fotos.

### Corregido

- La caducidad diaria de precios ya no se detiene entera si un trabajo cambia mientras corre (por ejemplo, llega la señal): ese trabajo se deja y sigue con los demás (S3). Su test ya no adelanta el reloj sobre la base de datos compartida, lo que podía caducar precios de otros tests.

### Verificado

- En el navegador a 390 y 1440 px (todo en pt; «Mis pagos» también en en y la ficha del admin en es), con Stripe en modo test: el admin pide la visita de TC01 a Carla; Carla corrige el largo a 5 m, propone «Reparar fissura» por 40 € con 2 fotos y una nota (el borrador sobrevive a recargar la página) y envía; el admin ve el informe, no puede confirmar hasta poner 90 € al extra y confirma la versión 2. Tras la señal, la oferta, el trabajo y el resto pagados con webhooks reales, Carla ve 289,20 € pendientes en «Mis pagos» y el admin los ve en su ficha.

### Pendiente

- Marcar los pagos al pintor como pagados con referencia y la pantalla de Finanzas: H7 (A5.4).
- P3 propone solo partidas libres: el catálogo de partidas extra no es público; la API ya acepta `catalogKey`.

## H6a · Pintores: alta, asignación y ejecución — 2026-10-23, [PR #7](https://github.com/nilh77/pinta/pull/7)

### Añadido

- Admin › Pintores: lista con estado (invitado, activo, inactivo), zonas, trabajos hechos y valoración; alta con nombre, email e idioma y, si se quiere, teléfono, NIF, especialidades, concelhos, IBAN y notas, que envía la invitación (válida 72 h). Ficha para editar los datos (IBAN cifrado y siempre enmascarado, valoración interna), reenviar la invitación, activar o desactivar (un pintor desactivado no puede entrar ni recibir ofertas), documentos con caducidad y sus trabajos (A4.1–A4.4).
- `/pintor/alta`: el pintor elige la contraseña desde el enlace del email y confirma o completa la ficha que preparó el admin (teléfono, NIF, concelhos e IBAN); un enlace caducado pide una nueva invitación (A4.2).
- Asignación: pestaña «Asignación» del trabajo con los pintores activos que cubren el concelho, fecha de inicio y el historial de ofertas y respuestas; una oferta nueva retira la anterior. El pintor acepta (el trabajo pasa a «Programado» con esa fecha y el cliente recibe el email) o rechaza con motivo (aviso al admin); una oferta ya respondida o retirada da `assignment_taken`; sin respuesta en 48 h se avisa una vez a los admins (A3.2, P1.1, P1.2).
- P1 · `/pintor`: pestañas Ofertas, Próximos, En curso y Hechos; tras entrar, el pintor llega aquí.
- P2 · `/pintor/trabajos/{id}`: dirección con enlace al mapa, cliente con botón de llamar (según la configuración), qué pintar con medidas y litros, material por pintura, partidas extra con su pago y lo que cobra; «Empezar trabajo» desde el día programado; fotos del resultado directas a almacenamiento y «Marcar como terminado» con al menos 3; después queda en solo lectura (P1.3, P2.1, P2.2).
- Cliente: en la pantalla 10 ve el nombre del pintor con el sello de verificado al programarse y las fotos del resultado al terminar, con el email para pagar el resto (C5.4, C5.5).
- Test de contrato: ninguna respuesta de `/api/painter/*` lleva precio al cliente, IVA, señal, resto, tarifas ni margen.
- Datos de demo: Rui y Carla activos con zonas y valoración, João invitado.
- Emails: invitación del pintor, oferta nueva, oferta rechazada y sin respuesta (admins), trabajo programado y trabajo terminado (cliente).

### Cambiado

- Documentos: H6 en dos PR, al menos 3 fotos para terminar (P2.2), alta preparada por el admin, invitación de 72 h y el pintor que acepta como actor de «Señal pagada → Programado» en `docs/plan.md`, `docs/functional-requirements.md`, `docs/screens.md`, `docs/states.md`, `docs/api.md` y `docs/data-model.md`.

### Verificado

- En el navegador a 390 y 1440 px y en pt, es y en, con la señal de TC01 pagada en Stripe modo test: el admin invita a un pintor, el pintor se da de alta con el enlace de Mailpit, el admin le ofrece el trabajo, el pintor lo acepta, lo empieza el día programado, sube 3 fotos y lo termina; el cliente ve las fotos y el botón para pagar el resto.

### Pendiente

- H6b: informe de la visita técnica del pintor (P3.1), pagos al pintor pendientes al cerrar el trabajo y pantalla P4 «Mis pagos».
- H8: fotos de avance y «reportar incidencia» en P2.

## H5 · Pagos — 2026-10-23, [PR #6](https://github.com/nilh77/pinta/pull/6)

### Añadido

- Pantalla 11 · Pago (`/mis-trabajos/{id}/pagar?tipo=deposit|balance|site_visit`): Payment Element de Stripe con tarjeta, MB WAY y Multibanco según el importe (aviso del límite diario de MB WAY por encima de 1.000 €, sin MB WAY por encima de 5.000 €), cuenta atrás de 5 minutos para aceptar en la app MB WAY, referencia Multibanco con botones de copiar, «Confirmando el pago…» hasta el webhook y reintento tras un fallo. Volver atrás reutiliza el mismo pago (C5.3).
- `POST /api/jobs/{id}/payments` con el importe calculado en el servidor e `Idempotency-Key`; `GET` de pagos para el cliente y el admin; códigos `quote_expired`, `payment_already_made` y `payment_provider_error` (C5.3).
- Webhook de Stripe con firma verificada y eventos guardados por su id: repetirlo no repite nada. Un pago confirmado lleva el trabajo de «Precio confirmado» a «Señal pagada» o de «Finalizado» a «Cerrado»; un pago que llega para un trabajo cancelado o expirado se devuelve entero y se avisa al admin; disputas por email (S4).
- Reembolsos al cancelar según la política: toda la señal sin fecha o a 7 días o más del inicio, la mitad después; el admin puede devolver todo como excepción. Se envían a Stripe con Hangfire y el cliente recibe el email (C5.6, A3.4).
- Fecha de inicio: el admin la fija al pasar a «Programado» y se ve en el detalle del cliente y en el tablero (A3.3).
- Visita técnica de pago: el cliente la paga antes de la visita y, si es descontable, se resta del subtotal de la cotización siguiente; caso dorado TC15 (A2.6).
- Recibos PDF simulados por cada pago («Documento simulado, sin validez fiscal») en el idioma del cliente, con los datos de la empresa, nuevos en Precios › Reglas (C5.3, A1.6).
- Pantalla 10: pagar señal, visita y resto, estado de cada pago, reembolsos y recibos. Admin: pestaña Pagos.
- Emails de pago recibido (con la nota de «Stripe» en el extracto de MB WAY), referencia Multibanco, reembolso, devolución automática y disputa.

### Cambiado

- Documentos: decisiones de H5 en `docs/plan.md` (historias C5.3, C5.5, C5.6, A3.4 y S4), fórmula del descuento de la visita en `docs/pricing-engine.md`, nuevos endpoints y campos en `docs/api.md` y `docs/data-model.md`.

### Corregido

- El AppHost arranca el Stripe CLI con `--events`: las versiones recientes del CLI no reenvían nada sin esa lista.

### Verificado

- Con Stripe en modo test y el CLI: la señal de TC01 (147,68 €) pagada con la tarjeta 4242… pasa el trabajo a «Señal pagada» solo al llegar el webhook; reenviar el evento con `stripe events resend` no duplica ni la transición ni el email; cancelar 3 días antes del inicio reembolsa 73,84 € (50%) en Stripe y el pago queda «reembolsado en parte».

### Pendiente

- El pago del resto solo se puede probar con tests hasta que los pintores marquen «Finalizado» en H6.

## H4 · Revisión y máquina de estados — 2026-10-23, [PR #5](https://github.com/nilh77/pinta/pull/5)

### Añadido

- Admin · Revisión: cola «En revisión» de la más antigua a la más reciente con semáforo del plazo (verde, ámbar con menos de 24 h laborables, rojo vencido) y los «Pendiente de email» aparte; detalle con cliente, dirección, fechas, descripción y fotos a pantalla completa (A2.1, A2.2).
- Borrador de revisión que se recalcula en el servidor al editar: estancias y medidas, pintura, ocupación, añadir estancias, partidas extra del catálogo o libres, Premium a mano y subtotal fijado a mano con motivo obligatorio. Desglose interno con material, mano de obra, pago al pintor y margen con aviso por debajo del 20% (A2.3–A2.5, A2.7).
- Confirmar precio con nota para el cliente; con margen bajo pide confirmarlo expresamente. Válido 15 días; el cliente recibe el email (A2.8). Rechazar con motivo (A2.9). Visita técnica con un socio, fecha, hora y precio, y vuelta a revisión al terminarla (A2.6).
- Admin · Trabajos: lista paginada o tablero por estado, filtros por estado, fechas, concelho y Premium, búsqueda por código, nombre o email, exportación CSV; ficha con pestañas Cotización, Versiones, Archivos e Historial (estados con quién, cuándo y motivo, y emails enviados). Cambio de estado manual solo entre transiciones permitidas y cancelación con motivo (A3.1, A3.3–A3.5).
- Admin · Cobertura: prefijos postales por concelho con interruptor, alta de prefijos y lista de espera con exportación CSV (A1.7).
- Cliente · Mis trabajos (pantalla 9): activos e historial con estado, progreso y «Revisar y aceptar» destacado (C5.1).
- Cliente · Detalle del trabajo (pantalla 10): contenido por estado; con precio confirmado, una línea «Pintura de N elementos · m²» y una por partida extra con IVA, total, señal, resto, nota del revisor y validez con cuenta atrás. «Tengo dudas» (mensaje a los socios por email), rechazar el precio, cancelar con el reembolso calculado y pedir nuevo precio si expiró (C5.2, C5.6, C5.7).
- Caducidad diaria a las 03:00 de Lisboa: un precio confirmado el día D vale hasta D+15 y el día D+16 pasa a «Expirado» con email al cliente (S3).
- Recuperar contraseña: `/recuperar` y `/restablecer`, enlace válido 2 horas, misma respuesta exista o no el email (S1).
- Emails con registro y reintentos (hasta 5) en `notifications.outbox`; nuevos emails de precio confirmado, rechazo, caducidad, visita técnica, dudas y rechazo del cliente a los socios, y recuperación de contraseña (S2).
- URLs firmadas de lectura para fotos y vídeos, solo para admins y el cliente del trabajo (C5.5).
- Caso dorado TC14 (TC10 con subtotal fijado a mano) en el motor de precios.
- Enlace «Mis trabajos» o «Panel» en la cabecera y «Ver mi trabajo» en la confirmación.

### Cambiado

- Documentos: pagos (C5.3) y reembolsos pasan a H5, asignación de pintores a H6; en H4 la visita técnica la hace un socio. La pantalla 10 ofrece «Tengo dudas» en lugar de «Pedir cambios». Nuevos endpoints en `docs/api.md`: socios, exportación CSV y fin de visita.
- Toda transición pasa por la máquina de estados: las no permitidas devuelven `409 invalid_transition`; editar con una versión antigua del trabajo, `409 concurrency_conflict`.

### Corregido

- Cerrar sesión en el panel o en el área del cliente lleva al inicio en lugar de a «Entrar».

### Pendiente

- «Aceptar y pagar» muestra «Pago disponible en breve» hasta H5; la cancelación aún no envía email ni reembolsa porque no hay pagos.

## H3 · Envío de la solicitud — 2026-10-22, [PR #4](https://github.com/nilh77/pinta/pull/4)

### Añadido

- Pantalla 5 · Fotos y detalles: fotos y vídeos por estancia y generales, subida directa al almacenamiento con progreso, reintento y quitar; compresión a unos 2 MP en el dispositivo; descripción de hasta 2.000 caracteres (C3.1, C3.2).
- Pantalla 6 · Dirección y fecha: comprobación del código postal con su concelho, lista de espera fuera de cobertura, «Lo antes posible» o un rango de fechas a partir de 3 días laborables y de hasta 60 días (C4.1, C4.2).
- Pantalla 7 · Cuenta: crear cuenta o entrar dentro del flujo, login con Google si está configurado, factura a empresa con NIF validado (C4.3).
- Pantalla 8 · Confirmación con el código `PT-AAAA-NNNN`, próximos pasos, resumen y reenvío del email de confirmación (C4.4, C4.5).
- `POST /api/jobs` idempotente con `Idempotency-Key`: crea el trabajo, la versión de cotización «estimación» con la copia de precios y une las fotos del borrador. Queda «Pendiente de email» hasta confirmar el email y entonces pasa a «En revisión» con el plazo de revisión (C4.4, C4.5).
- Un solo email «Hemos recibido tu solicitud» en el idioma del cliente, con el enlace de confirmación cuando hace falta.
- Máquina de estados del trabajo con la tabla completa de `docs/states.md` e historial de cambios.
- Subidas firmadas a S3/MinIO con límites de tipo, tamaño y número; limpieza diaria de subidas sin confirmar.
- `GET /api/coverage/check`, `POST /api/coverage/waitlist` y el ajuste «antelación mínima de las fechas» en A1.

### Cambiado

- Documentos: descripción de 2.000 caracteres, comprobación de cobertura por `GET`, días laborables de lunes a viernes, un solo email al enviar y nuevo formato de `POST /api/jobs` con `draftId`.

### Pendiente

- La pantalla de admin de Cobertura llega en H4.
- «Ver mi trabajo» desde la confirmación llega con el área del cliente.
- Las cuentas creadas con Google no piden teléfono todavía.

## H2 · Constructor del cliente — 2026-10-22, [PR #3](https://github.com/nilh77/pinta/pull/3)

### Añadido

- Landing del board Inicio (y escritorio D1) con «Cotizar gratis»; el idioma elegido se recuerda en el dispositivo y la primera visita es siempre en portugués (C1.1).
- Paso 1 · Tipo de trabajo: pared o techo, habitación o vivienda completa; aviso de cotización a medias y confirmación antes de descartar estancias (C2.1).
- `RoomEditor`: vista cenital con paredes A–D y techo que se tocan, tallas S/M/L o medidas exactas, pintura sugerida, daños y m² al instante (C2.2, C2.3, C2.5, C2.6).
- Paso 2 · Esquema: ocupación, estancias con área y estado, renombrar, duplicar, eliminar con deshacer, máximo de 30 elementos y estimación provisional en vivo; editor en hoja inferior en móvil y constructor en tres columnas desde 1024 px (C2.4).
- Paso 4 · Estimación con rango con IVA, área, insignias, aviso de daños y resumen por estancia; sin tarifas, material ni margen (C2.8).
- Borrador en el dispositivo que sobrevive a recargas y caduca a los 30 días (C2.7).
- Cálculo de área en el navegador con las mismas reglas de redondeo que `Pinta.Pricing`, probado con los casos dorados.
- `GET /api/catalog` incluye el plazo de revisión y la validez del precio (solo días).

### Cambiado

- La vivienda completa empieza con sala, cocina, baño y dormitorio sin describir; la ocupación se elige en el esquema (documentos 6.2 y 6.4 actualizados).

### Pendiente

- El botón «Continuar para precio final» queda desactivado hasta H3 (fotos y envío).
- La landing aún no tiene el comprobador de cobertura ni el desde-precio (H3, C4.1).

## H1 · Precios y catálogo — 2026-10-22, [PR #2](https://github.com/nilh77/pinta/pull/2)

### Añadido

- Motor de precios `Pinta.Pricing`: librería pura que reproduce `docs/reference/pricing_reference.py` al céntimo, con validación de entradas y política de reembolso. Casos dorados TC01–TC13 como tests.
- Módulo Catalog con pinturas, tipos de superficie, tipos de estancia (tallas S/M/L y pintura sugerida), modificadores de ocupación, partidas extra, configuración global y zonas de cobertura. Semilla con la configuración del documento 6.3 y los 16 concelhos del distrito de Lisboa.
- `GET /api/catalog`: opciones del constructor en el idioma de la petición, sin tarifas.
- CRUD del admin en `/api/admin/catalog/*`, `/api/admin/settings` y `/api/admin/coverage`, con versión y `409 concurrency_conflict`; sin borrado (A1.1–A1.7).
- `POST /api/quotes/estimate`: área, rango y marcas para el cliente, con límite de 60 por minuto por IP (C2.8).
- `POST /api/admin/quotes/simulate`: desglose completo en céntimos, sin guardar nada (A1.8).
- Pantalla A1 `/{locale}/admin/precios` con pestañas editables, guardado conjunto, aviso de conflicto y de cambios sin guardar, y simulador lateral. Textos en pt, es y en.
- Tests de arquitectura que prohíben I/O, reloj, aleatoriedad y coma flotante en `Pinta.Pricing`.
- Este `CHANGELOG.md`.

### Cambiado

- La API solo acepta números JSON (no cadenas), así el cliente TypeScript los tipa como `number`.
- Glosario: tabla `catalog.modifiers` y claves de catálogo (`paintId: "matte"`) en las peticiones de cotización.

### Pendiente de revisar

- Los prefijos postales de la semilla de cobertura son aproximados.
- «Descuento de huecos» de la pantalla A1 queda fuera hasta que el documento 6.3 defina la regla.

## H0 · Cimientos — 2026-10-22, [PR #1](https://github.com/nilh77/pinta/pull/1)

### Añadido

- PRD en `docs/`, `CLAUDE.md` y `README.md` con requisitos, arranque, credenciales de demo y tests.
- Solución .NET 10 con los nueve módulos, un DbContext y un esquema por módulo, `Pinta.SharedKernel` y `Pinta.Pricing` vacía.
- API con Identity (cookie; roles client, painter y admin), registro, login, confirmación de email, ProblemDetails con `code` estable, antiforgery con `X-XSRF-TOKEN`, límites de peticiones y Hangfire.
- Orquestación local con Aspire: PostgreSQL, MinIO, Mailpit, API y web.
- Web Next.js con next-intl (pt, es, en), paleta B, cliente de API tipado y páginas de acceso.
- Tests de arquitectura, de integración con Testcontainers y Vitest; CI en cada push a `main` y en los PR.

### Cambiado

- Contraseña mínima de 10 caracteres (C4.3).
- Importes en PT y ES con el formato `1.270,00 €`.
