# Pinta — Fase 6.2: PRD · Requisitos Funcionales

> Fuente: documento 6.2 del PRD de Pinta (https://claude.ai/artifact/N7aXoZESRjfv7xcVedtuFb). Si este archivo y el documento difieren, manda el documento.

Cada historia tiene un identificador estable y criterios de aceptación en formato Dado / Cuando / Entonces, pensados para convertirse en tests. Los identificadores se usan en ramas, commits y tests (por ejemplo, `C2.3`). Las fórmulas están en el documento 6.3 y las pantallas en el 6.4.

## Cliente: inicio y constructor

**C1.1 · Landing.** Como visitante quiero entender la oferta y empezar a cotizar.

- Dado un visitante sin idioma guardado, cuando abre la web, entonces la ve en portugués.
- Dado cualquier página, cuando cambia el idioma, entonces la URL pasa a `/es` o `/en`, el contenido cambia y la elección se recuerda en el dispositivo.
- Cuando pulsa "Cotizar gratis", entonces llega al paso 1 del asistente.

**C2.1 · Tipo de trabajo.** Como cliente quiero elegir qué voy a pintar.

- Dado el paso 1, cuando elige superficie suelta, habitación o vivienda y pulsa Continuar, entonces llega al constructor de ese tipo.
- Dado un borrador con datos, cuando cambia de tipo, entonces se le pide confirmación antes de descartarlos.

**C2.2 · Superficie suelta.** Como cliente quiero cotizar una pared, un techo o una fachada.

- Dado el constructor de superficie, cuando elige el tipo e introduce largo y alto válidos, entonces ve el área al instante y el rango de precio tras un máximo de 500 ms.
- Cuando introduce una medida fuera de rango (6.3), entonces ve el error junto al campo y no se calcula el precio.

**C2.3 · Habitación.** Como cliente quiero describir una habitación tocando sus paredes y su techo.

- Dado el editor de estancia, entonces por defecto están marcadas las 4 paredes y el techo, con talla M y la pintura sugerida para el tipo.
- Cuando toca una pared o el techo, entonces cambia su estado, se anuncia al lector de pantalla y el área se recalcula al instante.
- Cuando cambia entre talla y medidas exactas, entonces el área y el precio se recalculan; las medidas exactas parten de las de la talla elegida.
- Dado que desmarca todas las paredes y el techo, cuando intenta guardar, entonces ve "Selecciona al menos una pared o el techo".

**C2.4 · Vivienda.** Como cliente quiero describir mi casa como un conjunto de estancias.

- Dado el esquema vacío, entonces ve una sala y un dormitorio pendientes de describir y el botón "Añadir estancia".
- Cuando añade una estancia, entonces elige su tipo y se crea con nombre por defecto ("Dormitorio 2").
- Cuando duplica, renombra o elimina una estancia, entonces el esquema y el total se actualizan.
- Dado el esquema, entonces cada estancia muestra si está descrita y su área, y la barra inferior muestra estancias descritas, área total y rango provisional.
- Cuando elige la ocupación (vacía, con muebles, habitada), entonces el precio se recalcula.
- Dado un máximo de 30 estancias, cuando intenta añadir la 31.ª, entonces el botón está deshabilitado con una explicación.

**C2.5 · Pintura.** Como cliente quiero elegir la pintura de cada estancia.

- Dado un tipo de estancia, entonces aparece preseleccionada y marcada como "Sugerida" la pintura de la tabla de 6.3.
- Cuando elige otra, entonces el precio se recalcula.

**C2.6 · Daños.** Como cliente quiero avisar de humedad o grietas.

- Cuando marca "Hay humedad, grietas o desconchados" en una estancia, entonces esa estancia muestra la etiqueta "Humedad", el rango se amplía y la estimación muestra un aviso de posible trabajo extra.

**C2.7 · Borrador.** Como cliente no quiero perder lo que llevo.

- Dado un borrador en curso, cuando cierra o recarga la página y vuelve, entonces recupera tipo, estancias, ocupación y paso.
- Dado un borrador de más de 30 días, cuando vuelve, entonces empieza de cero.

**C2.8 · Estimación.** Como cliente quiero ver un precio antes de dar mis datos.

- Dado al menos un elemento válido, cuando pulsa "Ver mi precio", entonces ve el rango con IVA, el área total y el resumen por estancia.
- Dado que aplica el mínimo, entonces ve la etiqueta "Trabajo mínimo".
- Dado que supera el máximo, entonces no ve rango sino "Te enviaremos una cotización personalizada" y puede continuar igualmente.
- Entonces nunca ve tarifas, desglose de material ni margen.

## Cliente: detalles, cuenta y envío

La numeración de C3–C4 se ha revisado respecto a la Fase 3, porque la estimación se movió antes de los detalles (C2.8).

**C3.1 · Fotos y vídeos.** Como cliente quiero enseñar el estado de mi casa.

- Dado el paso de detalles, entonces ve una tarjeta por estancia con sus fotos y vídeos y botones Foto y Vídeo.
- Cuando sube una foto, entonces se comprime en el dispositivo a unos 2 MP, se sube directamente al almacenamiento y aparece su miniatura con una barra de progreso.
- Dado un límite de 10 fotos y 2 vídeos por estancia y 100 MB por vídeo, cuando lo supera, entonces ve el error y el archivo no se sube.
- Dado un tipo de archivo no permitido, cuando lo elige, entonces ve "Formato no admitido".
- Dada una estancia con daños, entonces ve la indicación de añadir una foto de cerca de la zona afectada.
- Puede continuar sin fotos; ve cuántas estancias tienen al menos una.

**C3.2 · Descripción.** Cuando escribe una descripción libre (máximo 2.000 caracteres), entonces se guarda con el borrador.

**C4.1 · Dirección.** Como cliente quiero indicar dónde es el trabajo.

- Cuando introduce un código postal con formato `NNNN-NNN` de un concelho cubierto, entonces ve "Trabajamos en tu zona".
- Dado un código postal fuera de cobertura, entonces ve el aviso, no puede continuar y puede dejar su email para cuando lleguemos a su zona.
- Dirección, código postal y localidad son obligatorios.

**C4.2 · Fecha preferida.** Cuando elige "Lo antes posible" o un rango de fechas futuras en el calendario, entonces queda guardado; no puede elegir fechas pasadas ni rangos de más de 60 días.

**C4.3 · Cuenta.** Como cliente quiero crear mi cuenta en el último paso.

- Cuando elige Google, entonces crea la cuenta o entra con ella y vuelve al envío con el borrador intacto.
- Cuando elige email, entonces debe indicar nombre, email, teléfono con prefijo y contraseña de al menos 10 caracteres, y aceptar términos y privacidad.
- Cuando marca "Factura a nombre de una empresa", entonces NIF (9 dígitos, válido) y nombre fiscal pasan a ser obligatorios.
- Dado un email ya registrado, entonces ve la opción de entrar con su cuenta.
- Dado un cliente con sesión iniciada, entonces este paso solo pide confirmar sus datos.

**C4.4 · Envío.** Como cliente quiero enviar mi solicitud.

- Cuando envía, entonces se crea el trabajo con su código (`PT-AAAA-NNNN`), la versión de cotización de origen "estimación" con la copia de precios, las estancias, las fotos confirmadas y la dirección; y el borrador del dispositivo se borra.
- Dado un email confirmado, entonces el trabajo queda "En revisión". Si no, queda "Pendiente de email" y recibe el email de confirmación.
- Entonces ve la pantalla de confirmación con el código, los próximos pasos y el plazo de 2 días laborables.
- Dado un fallo de red, cuando reintenta, entonces no se crea un trabajo duplicado (clave de idempotencia).

**C4.5 · Confirmación de email.** Cuando abre el enlace del email, entonces su cuenta queda confirmada y sus trabajos "Pendiente de email" pasan a "En revisión". El enlace caduca a las 48 horas y puede pedir otro.

## Cliente: mis trabajos, pagos y cancelación

**C5.1 · Mis trabajos.** Como cliente quiero ver el estado de mis trabajos.

- Dado que tiene trabajos, entonces los ve en "Activos" (no cerrados ni cancelados) e "Historial", del más reciente al más antiguo, cada uno con estado y barra de progreso.
- Dado un trabajo que espera su respuesta, entonces aparece destacado con el botón "Revisar y aceptar".
- Dado que no tiene trabajos, entonces ve una invitación a cotizar.

**C5.2 · Precio confirmado.** Como cliente quiero decidir sobre el precio final.

- Dado un trabajo en "Precio confirmado", entonces ve el total con IVA, la estimación inicial, las partidas extra con su precio, la nota del revisor, la fecha límite de validez, la señal y el resto.
- Cuando acepta, entonces pasa a la pantalla de pago de la señal.
- Cuando rechaza, entonces indica un motivo opcional, el trabajo pasa a "Cancelado" y el admin recibe aviso.
- Cuando pulsa "Tengo dudas", entonces envía un mensaje al admin (por email) sin cambiar el estado.

**C5.3 · Pago.** Como cliente quiero pagar la señal y el resto.

- Dada la pantalla de pago, entonces ve los métodos según el importe (regla de 6.3 / Integraciones): tarjeta, MB WAY y Multibanco; MB WAY con aviso por encima de 1.000 € y oculto por encima de 5.000 €.
- Cuando paga con tarjeta o MB WAY y Stripe confirma, entonces ve "Pago recibido" y el trabajo pasa a "Señal pagada" (o "Cerrado" si era el resto) solo tras el webhook.
- Cuando elige Multibanco, entonces ve entidad, referencia, importe y fecha límite, y los recibe por email; el estado cambia cuando llega el webhook.
- Dado un pago fallido o cancelado, entonces ve el motivo y puede reintentar con otro método.
- Dado un precio expirado, entonces no puede pagar.

**C5.4 · Programación.** Dado un trabajo "Programado", entonces ve la fecha y el pintor (nombre, foto y sello de verificado).

**C5.5 · Final del trabajo.** Dado un trabajo "Finalizado", entonces ve las fotos del resultado y el botón para pagar el resto.

**C5.6 · Cancelación.** Como cliente quiero poder cancelar antes de que empiece.

- Dado un trabajo antes de "En curso", cuando pulsa Cancelar, entonces ve el importe que se le devolverá según la política antes de confirmar.
- Cuando confirma, entonces el trabajo pasa a "Cancelado", se lanza el reembolso en Stripe y recibe el email.
- Dado un trabajo "En curso" o posterior, entonces la opción no aparece.

**C5.7 · Recotización.** Dado un trabajo "Expirado", cuando pulsa "Pedir nuevo precio", entonces vuelve a "En revisión" y se creará una nueva versión de cotización con los precios vigentes.

**C5.8 · Cuenta.** Como cliente quiero gestionar mis datos.

- Puede editar nombre, teléfono, idioma, datos de facturación y avisos por email.
- Puede cerrar sesión.
- Puede pedir la supresión de su cuenta: sus datos personales se anonimizan, sus trabajos activos se cancelan con la política habitual y los registros contables se conservan.

## Admin: precios, simulador y revisión

**A1.1–A1.5 · Catálogo.** Como admin quiero mantener pinturas, superficies, tallas de estancia, modificadores y partidas extra.

- Cuando crea o edita un elemento, entonces se valida (importes ≥ 0 con 2 decimales, rendimiento > 0, factores entre 0,5 y 3, nombres en PT obligatorios y ES/EN opcionales con PT como respaldo) y se registra quién y cuándo.
- Cuando desactiva un elemento, entonces deja de ofrecerse en nuevas cotizaciones pero las existentes lo conservan. No se puede borrar.
- Dado un elemento usado como sugerencia de un tipo de estancia, cuando intenta desactivarlo, entonces se le pide elegir otra sugerencia.

**A1.6 · Configuración global.** Cuando edita cualquier parámetro global de 6.3 (mínimo, máximo, Premium, rango, señal, validez, margen mínimo, visita, cancelación, plazo, límites de archivos, visibilidad del teléfono, IVA), entonces aplica a las cotizaciones nuevas desde ese momento.

**A1.7 · Cobertura.** Cuando añade o desactiva un prefijo de código postal con su concelho, entonces la validación de direcciones lo refleja al instante.

**A1.8 · Simulador.** Como admin quiero probar tarifas antes de cambiarlas.

- Cuando introduce elementos de prueba, entonces ve el desglose completo (líneas, litros, material, mano de obra, pintor, subtotal, IVA, total, margen y rango) con la configuración actual.
- El simulador no guarda nada.

**A2.1 · Bandeja de revisión.** Dados trabajos "En revisión", entonces los ve del más antiguo al más reciente con un semáforo: verde si quedan más de 24 h laborables del plazo, ámbar si menos, rojo si está vencido. Los "Pendiente de email" aparecen aparte, sin semáforo.

**A2.2 · Detalle.** Cuando abre una solicitud, entonces ve cliente, dirección, fechas, ocupación, estancias con medidas, fotos y vídeos, descripción, daños declarados y el desglose interno de la versión vigente.

**A2.3 · Corrección.** Cuando edita estancias, medidas, pintura u ocupación, entonces se recalcula todo al instante como borrador de una nueva versión de origen "revisión"; la versión del cliente no cambia.

**A2.4 · Partidas extra.** Cuando añade una partida del catálogo o una libre (descripción, precio al cliente, pago al pintor), entonces el total y el margen se recalculan.

**A2.5 · Premium.** Dada un área mayor que el umbral, entonces aparece marcado automáticamente; el admin puede marcarlo o desmarcarlo a mano.

**A2.6 · Visita técnica.** Cuando pide visita, asigna a un pintor activo o a sí mismo y fija fecha y hora, entonces el trabajo pasa a "Visita técnica" y el asignado y el cliente reciben aviso.

**A2.7 · Ajuste manual.** Cuando fija el subtotal a mano, entonces debe escribir un motivo; si el margen queda por debajo del mínimo, ve un aviso pero puede confirmar.

**A2.8 · Confirmar precio.** Cuando confirma, entonces se guarda la versión con la copia de precios, el trabajo pasa a "Precio confirmado" con validez de 15 días, la nota del revisor (opcional, visible para el cliente) se guarda y el cliente recibe el email.

**A2.9 · Rechazar.** Cuando rechaza con motivo obligatorio, entonces el trabajo pasa a "Rechazado" y el cliente recibe el email con el motivo.

## Admin: trabajos, pintores y finanzas

**A3.1 · Lista y tablero.** Como admin quiero ver todos los trabajos.

- Entonces puede alternar entre lista paginada y tablero por estado, filtrar por estado, fechas, concelho, pintor y Premium, y buscar por código, nombre o email del cliente.

**A3.2 · Asignar y programar.** Dado un trabajo "Señal pagada", cuando elige un pintor activo cuya zona incluye el concelho y una fecha, entonces se crea una asignación "pendiente" y el pintor recibe el email. Cuando el pintor acepta, el trabajo pasa a "Programado" y el cliente recibe el email. Si rechaza, el admin recibe aviso y puede asignar a otro.

**A3.3 · Cambio de estado manual.** Cuando fuerza una transición permitida (tabla de 5.3) con motivo obligatorio, entonces se aplica con sus efectos y queda en el historial. Las transiciones no permitidas no se ofrecen.

**A3.4 · Cancelar.** Cuando cancela un trabajo antes de "En curso", entonces ve el reembolso calculado y puede elegir reembolso total como excepción, con motivo.

**A3.5 · Historial.** Dado un trabajo, entonces ve la línea de tiempo completa: estados, quién, cuándo, motivo, versiones de cotización, pagos y emails enviados.

**A4.1 · Alta de pintor.** Cuando da de alta un pintor con nombre, NIF, teléfono, email, especialidades, zonas (concelhos), IBAN y notas, entonces se guarda con el IBAN cifrado y se envía la invitación.

**A4.2 · Invitación y activación.** Cuando el pintor abre la invitación (válida 72 h) y fija su contraseña, entonces su cuenta queda activa con rol Pintor. El admin puede reenviar la invitación y desactivar o reactivar pintores; uno desactivado no recibe asignaciones ni puede entrar.

**A4.3 · Documentos.** Cuando sube un documento (seguro, actividad, contrato) con fecha de caducidad, entonces queda en la ficha; son informativos y no bloquean la activación.

**A4.4 · Ficha del pintor.** Entonces ve sus trabajos, su valoración interna (1 a 5, solo admin) y sus pagos pendientes y pagados.

**A5.1 · Panel de finanzas.** Dado un periodo (mes por defecto), entonces ve ingresos sin IVA de trabajos cerrados, material, pagos a pintores, margen bruto, costes de operación, beneficio y la parte de cada socio (50/50).

**A5.2 · Costes de operación.** Cuando registra un coste con fecha, categoría de la lista cerrada, descripción e importe, entonces se suma al periodo. No se pueden añadir costes a un mes cerrado.

**A5.3 · Cierre mensual.** Cuando cierra un mes, entonces sus cifras se congelan y ya no cambian.

**A5.4 · Pagos a pintores.** Dado un trabajo "Cerrado", entonces existe un pago pendiente al pintor por su importe. Cuando el admin lo marca como pagado con referencia de transferencia, entonces el pintor lo ve como pagado.

## Pintor

**P1.1 · Mis trabajos.** Como pintor quiero ver lo que tengo asignado.

- Entonces ve primero las asignaciones por responder y después sus próximos trabajos y visitas por fecha.
- Cada trabajo muestra fecha, concelho, área y lo que va a cobrar. Nunca el precio al cliente ni el margen.

**P1.2 · Responder a una asignación.** Cuando acepta, entonces la asignación queda "aceptada" y el trabajo pasa a "Programado". Cuando rechaza con motivo obligatorio, entonces la asignación queda "rechazada" y el admin recibe aviso. Una asignación sin respuesta en 48 h genera un aviso al admin.

**P1.3 · Detalle.** Dado un trabajo aceptado, entonces ve dirección con enlace al mapa, nombre y teléfono del cliente (según configuración), estancias con áreas y lo que se pinta, pintura y litros por tipo, partidas extra con su pago, notas y fotos del cliente.

**P2.1 · Empezar.** Dado un trabajo "Programado" asignado a él, cuando pulsa "Empezar trabajo", entonces pasa a "En curso". Solo es posible desde el día programado.

**P2.2 · Finalizar.** Dado un trabajo "En curso", cuando sube al menos una foto del resultado y pulsa "Marcar como finalizado", entonces pasa a "Finalizado" y el cliente recibe el email para pagar el resto. Sin foto, ve el error.

**P3.1 · Visita técnica.** Dada una visita asignada, cuando registra medidas reales por estancia, marca trabajos extra detectados, sube fotos y escribe observaciones y pulsa Enviar, entonces el trabajo vuelve a "En revisión" con la información visible para el admin, que la usa para la nueva versión de cotización.

**P4.1 · Mis pagos.** Entonces ve el total pendiente y lo pagado en el mes, la lista de pagos pendientes por trabajo y la de pagados con fecha y referencia.

## Sistema

**S1 · Autenticación.**

- Dado un usuario sin sesión, cuando accede a un área protegida, entonces va al login y después vuelve a la página pedida.
- Dados 5 intentos fallidos, entonces la cuenta se bloquea 15 minutos.
- Cuando pide recuperar la contraseña, entonces recibe un enlace válido 2 horas; la respuesta es la misma exista o no el email.
- Dado un rol sin permiso o un recurso ajeno, entonces la API responde 403 o 404 sin revelar si el recurso existe.

**S2 · Emails.** Cuando ocurre cada evento de la tabla de emails (Integraciones, 5.4), entonces se encola un email en el idioma del destinatario, con reintentos hasta 5 veces, y queda registrado. Un fallo de envío nunca deshace la operación que lo originó.

**S3 · Tareas programadas.**

- Cada día a las 03:00 (Lisboa): los trabajos "Precio confirmado" vencidos pasan a "Expirado" con email al cliente.
- Cada día a las 09:00: recordatorio a los clientes cuyo precio vence en 3 días.
- Cada día: avisos al admin de asignaciones sin respuesta en 48 h y de solicitudes con plazo de revisión vencido.
- Cada lunes: aviso de documentos de pintor que caducan en 30 días.
- Cada noche: borrado de subidas sin confirmar de más de 24 h.

**S4 · Webhooks de Stripe.**

- Dado un evento con firma inválida, entonces se responde 400 y no se procesa.
- Dado un evento ya procesado (mismo ID), entonces se responde 200 sin repetir efectos.
- `payment_intent.succeeded` marca el pago como pagado y aplica la transición ("Señal pagada" o "Cerrado").
- `payment_intent.payment_failed` marca el pago como fallido.
- `charge.refunded` marca el reembolso como completado.
- `charge.dispute.created` avisa al admin.
- Si un pago llega para un trabajo cancelado o expirado, se reembolsa automáticamente y se avisa al admin.
