# Pinta — Fase 6.4: PRD · Pantallas

> Fuente: documento 6.4 del PRD de Pinta (https://claude.ai/artifact/KMqXsZTTvgDWKeqEQW7sBJ). Si este archivo y el documento difieren, manda el documento.

## Cómo leer este documento

Cada pantalla se describe con la misma ficha, para que Claude Code pueda implementarla sin interpretar el diseño:

- **Ruta y board**: la URL en Next.js (siempre bajo `/{locale}`) y el board del [canvas de diseño](https://claude.ai/artifact/QXmJ1wZgXMpmRUHx6pHXKz) que la dibuja.
- **Datos**: qué muestra y de qué endpoint del documento 6.5 lo obtiene.
- **Acciones**: qué puede hacer el usuario y a qué endpoint llama.
- **Validaciones**: reglas en cliente (Zod), que el servidor repite siempre.
- **Estados**: vacío, carga y error. Las historias de usuario que cubre (documento 6.2) van entre corchetes.

Reglas comunes a todas las pantallas:

- Mobile-first a 390 px; el escritorio se describe en la última sección.
- Carga: esqueletos con la forma del contenido, nunca un spinner a pantalla completa. Si una petición tarda más de 10 s, se muestra «Está tardando más de lo normal» con botón de reintentar.
- Error: se traduce el `code` de ProblemDetails; los errores por campo van bajo cada campo y el foco salta al primero.
- Los botones principales se desactivan mientras se envía y muestran un indicador dentro del botón.
- El borrador del constructor se guarda en `localStorage` (con try/catch) para no perder trabajo al recargar; al iniciar sesión o enviar, se limpia.
- Textos con next-intl en pt, es y en; importes con `Intl.NumberFormat('pt-PT', {style:'currency', currency:'EUR'})` adaptado al idioma; fechas en hora de Lisboa.
- Accesibilidad: objetivos táctiles de 44 px, contraste AA, etiquetas en todos los campos, el esquema tiene equivalente en lista para lectores de pantalla.

## Cliente · cotización

### 0 · Inicio

- **Ruta y board**: `/{locale}` · Inicio.dc.html. Pública, sin sesión. \[C1.1\]
- **Datos**: textos estáticos traducidos; desde-precio por m² tomado de `GET /api/catalog` (tarifa mínima de `interior_wall`); comprobador de cobertura.
- **Acciones**: «Calcular mi precio» → pantalla 1. Código postal → `POST /api/coverage/check`; si está fuera, formulario de lista de espera (`POST /api/coverage/waitlist`). Selector de idioma en la cabecera. «Entrar» para clientes con cuenta.
- **Validaciones**: código postal `^\d{4}-\d{3}$`.
- **Estados**: si el catálogo falla, se oculta el desde-precio y el resto funciona. Cobertura: «Sí llegamos a {localidad}» o «Aún no llegamos».
- **Alcance por hitos**: H2 construye la landing sin el comprobador de cobertura ni el desde-precio; ambos llegan en H3 con C4.1. Pendiente de alinear con `docs/api.md`, que define la comprobación como `GET /api/coverage/check?postalCode=`.

### 1 · Tipo de trabajo

- **Ruta y board**: `/{locale}/cotizar` · Tipo.dc.html. \[C2.1\]
- **Datos**: tres opciones, como el board: una pared o un techo (superficie suelta), una habitación y una vivienda completa.
- **Acciones**: elegir tipo → pantalla 2. Vivienda completa precarga sala, cocina, baño y un dormitorio en talla M, sin describir. Superficie y habitación empiezan con un elemento y abren su editor.
- **Validaciones**: hay que elegir un tipo para continuar. La ocupación se elige en la pantalla 2.
- **Estados**: si hay borrador guardado, aviso «Tienes una cotización a medias» con «Continuar» y «Empezar de nuevo».

### 2 · Esquema de la vivienda

- **Ruta y board**: `/{locale}/cotizar/esquema` · Main.dc.html. Es la pieza diferencial del producto. \[C2.2, C2.3\]
- **Datos**: ocupación (vacía, con muebles, habitada) con una línea que explica por qué cambia el precio; sin valor por defecto en habitación y vivienda (la estimación en vivo usa vacía hasta elegirla y «Ver mi precio» la pide); en superficie suelta, vacía por defecto. Tarjetas de estancia y superficie dispuestas como un esquema simple (no un plano a escala). Cada tarjeta muestra el nombre, las medidas o la talla, el m² y un punto de color con la pintura. Pie fijo con m² totales y rango estimado en vivo (`POST /api/quotes/estimate` con debounce de 400 ms).
- **Acciones**: añadir estancia (tipo de la lista) o superficie; tocar una tarjeta → pantalla 3; duplicar; eliminar con deshacer (toast 5 s); renombrar. «Ver estimación» → pantalla 4.
- **Validaciones**: de 1 a 30 elementos; no se avanza con elementos de área 0 (se marcan en rojo).
- **Estados**: vacío con ilustración y «Añade tu primera estancia». Si la estimación falla, el pie muestra «No pudimos calcular» con reintentar, y el esquema sigue editable.

### 3 · Editor de estancia

- **Ruta y board**: hoja inferior sobre la pantalla 2, con URL `?el={index}` · Estancia.dc.html. \[C2.4–C2.7\]
- **Datos**: vista cenital de la estancia con las paredes A, B, C y D y el techo, que se seleccionan tocando. Medidas: modo «Sé las medidas» (largo, ancho y alto, por defecto 2,60) o «No sé» (tallas S, M y L con su equivalencia en metros). Pintura con sugerencia según el tipo de estancia (baño → anti-humedad). Interruptor «Hay humedades, grietas o desconchones». Para superficies sueltas: tipo, largo y alto.
- **Acciones**: alternar paredes y techo; cambiar medidas y talla; elegir pintura; guardar (cierra la hoja) o cancelar.
- **Validaciones**: largo y ancho de 0,50 a 30 m; alto de 2 a 6 m; superficie suelta hasta 40 × 20 m; al menos una pared o el techo. Coma decimal aceptada en pt y es.
- **Estados**: el m² de la estancia se recalcula en el cliente al instante con la misma fórmula que el motor (solo área, nunca precio).

### 4 · Estimación

- **Ruta y board**: `/{locale}/cotizar/estimacion` · Estimacion.dc.html. \[C2.8\]
- **Datos**: rango «entre X € y Y € IVA incluido», m² totales, lista de elementos con su m², qué incluye (material, mano de obra, protección y limpieza) y qué sigue (revisión humana en 2 días laborables y precio cerrado válido 15 días). Insignias: «Mínimo aplicado», «Trabajo Premium» y «Rango amplio por tallas o daños».
- **Acciones**: «Añadir fotos y enviar» → pantalla 5 (en H2 se muestra desactivado con «Disponible en breve»; se activa en H3); «Editar» → pantalla 2.
- **Validaciones**: si `overMax`, no hay rango: se muestra «Este trabajo necesita un presupuesto a medida» y se puede enviar igualmente.
- **Estados**: el rango nunca se muestra desactualizado; durante el recálculo aparece atenuado.

## Cliente · detalles y envío

### 5 · Fotos y detalles

- **Ruta y board**: `/{locale}/cotizar/fotos` · Fotos.dc.html. \[C3.1, C3.2\]
- **Datos**: un bloque por elemento con miniaturas y contador («3/10 fotos · 0/2 vídeos»), un bloque de fotos generales y una descripción libre (máx. 1.000 caracteres).
- **Acciones**: añadir desde cámara o galería. Cada archivo pide `POST /api/media/upload-url`, sube directo a S3 con barra de progreso y confirma con `POST /api/media/confirm`. Se puede quitar un archivo antes de enviar. «Continuar» → pantalla 6. Las fotos son recomendadas, no obligatorias.
- **Validaciones**: imágenes JPEG, PNG, HEIC o WebP de hasta 10 MB; vídeos MP4 o MOV de hasta 100 MB; 10 fotos y 2 vídeos por estancia. Se rechazan en el cliente antes de subir.
- **Estados**: subida fallida → miniatura con icono de reintentar. «Continuar» espera a que terminen las subidas en curso y lo indica. Sin fotos, un aviso suave: «Con fotos el precio final se parece más a la estimación».

### 6 · Dirección y fecha

- **Ruta y board**: `/{locale}/cotizar/direccion` · Direccion.dc.html. \[C4.1, C4.2\]
- **Datos**: dirección, código postal, localidad (rellenada desde la cobertura), piso o puerta y notas de acceso (ascensor, aparcamiento). Calendario de fechas preferidas: «Lo antes posible» o un rango de inicio.
- **Acciones**: el código postal llama a `POST /api/coverage/check` al perder el foco. «Continuar» → pantalla 7, o directamente al envío si ya hay sesión.
- **Validaciones**: código postal con formato y en cobertura; dirección obligatoria; las fechas empiezan como mínimo 3 días laborables después de hoy (configurable) y el rango dura como mucho 60 días.
- **Estados**: fuera de cobertura → bloque con lista de espera; el borrador se conserva.

### 7 · Cuenta

- **Ruta y board**: `/{locale}/cotizar/cuenta` · Cuenta.dc.html. \[C4.3\]
- **Datos**: pestañas «Crear cuenta» y «Ya tengo cuenta». Campos: nombre, email, teléfono (+351 por defecto), contraseña, NIF y razón social opcionales («Necesito factura con NIF»), aceptación de términos y privacidad y consentimiento opcional de marketing.
- **Acciones**: `POST /api/auth/register` o `POST /api/auth/login`, y a continuación `POST /api/jobs` con `Idempotency-Key` generada al entrar en la pantalla. Google OAuth aparece solo si está configurado.
- **Validaciones**: email válido; contraseña de 10 o más caracteres; teléfono E.164; NIF portugués de 9 dígitos con dígito de control; términos obligatorios.
- **Estados**: `email_taken` → enlace a «Ya tengo cuenta» con el email precargado. Si el registro funciona pero el envío falla, se reintenta con la misma clave sin duplicar.

### 8 · Confirmación

- **Ruta y board**: `/{locale}/cotizar/enviado/{code}` · Confirmacion.dc.html. \[C4.4, C4.5\]
- **Datos**: código `PT-AAAA-NNNN`, resumen (m², rango, dirección y fechas) y línea de tiempo de lo que pasará: revisión en 2 días laborables, precio cerrado, pago de la señal y agenda.
- **Acciones**: «Ver mi trabajo» → pantalla 10. Si el estado es `pending_email`: «Confirma tu email para que revisemos tu solicitud» con reenvío (`POST /api/auth/resend-confirmation`, uno por minuto).
- **Estados**: el borrador local se borra al llegar aquí.

## Acceso (sin board)

Formularios sencillos con el estilo de la pantalla 7. Las rutas son las mismas en los tres idiomas; los `pathnames` traducidos llegan en H8.

### Entrar

- **Ruta**: `/{locale}/entrar?next=…`. \[S1\]
- **Acciones**: `POST /api/auth/login`; después vuelve a `next` (solo rutas internas) o al inicio. Enlace a «Crear cuenta».
- **Estados**: `invalid_credentials` como error general; cuenta bloqueada tras 5 intentos (mismo mensaje genérico).

### Crear cuenta

- **Ruta**: `/{locale}/registro`. Fuera del flujo de cotización (en el flujo, la pantalla 7). \[C4.3\]
- **Datos y validaciones**: los de la pantalla 7 sin NIF ni razón social (van con la solicitud).
- **Acciones**: `POST /api/auth/register`. La cuenta queda con sesión iniciada y el email sin confirmar; el inicio muestra «Confirma tu email» con reenvío.
- **Estados**: `email_taken` → enlace a «Entrar».

### Confirmar email

- **Ruta**: `/{locale}/confirmar-email?userId=…&token=…`, destino del enlace del email. \[C4.5\]
- **Acciones**: al abrirla llama a `POST /api/auth/confirm-email` y muestra éxito con enlace al inicio, o error (enlace caducado o no válido) con reenvío si hay sesión.

## Cliente · área privada

Requiere sesión con rol `client`. Sin sesión, redirige a `/{locale}/entrar?next=…`.

### 9 · Mis trabajos

- **Ruta y board**: `/{locale}/mis-trabajos` · MisTrabajos.dc.html. \[C5.1\]
- **Datos**: `GET /api/jobs` paginado. Tarjeta con código, localidad, m², insignia de estado con color y la próxima acción («Paga la señal antes del 6 nov», «Empieza el 12 nov»). Primero los activos, después los cerrados y cancelados.
- **Acciones**: abrir un trabajo → pantalla 10; «Nueva cotización» → pantalla 1.
- **Estados**: vacío → «Aún no tienes trabajos» con botón de cotizar.

### 10 · Detalle del trabajo

- **Ruta y board**: `/{locale}/mis-trabajos/{id}` · Trabajo.dc.html. \[C5.2–C5.6, C5.8\]
- **Datos**: `GET /api/jobs/{id}`. Cabecera con estado y línea de progreso (Revisión → Precio → Señal → Agenda → Ejecución → Terminado). Según el estado:
  - `in_review`: rango estimado y plazo de revisión.
  - `site_visit`: fecha propuesta, precio de la visita (0 € por defecto) y nombre del pintor.
  - `quoted`: precio cerrado frente a estimación, desglose para el cliente (material, mano de obra, extras e IVA, nunca margen ni pago al pintor), validez con cuenta atrás y notas del revisor.
  - `deposit_paid` y `scheduled`: fecha, pintor asignado (nombre, foto y teléfono si el ajuste lo permite) y saldo pendiente.
  - `in_progress`: fotos de avance.
  - `finished`: fotos del resultado, pago del saldo y valoración.
  - `expired`, `rejected` y `cancelled`: motivo y reembolso si lo hay.
- **Acciones**: «Pagar señal» → pantalla 11; «Pedir cambios» (texto libre, vuelve a revisión); «Cancelar» con diálogo que muestra el reembolso exacto según la política (100% hasta 7 días antes, 50% después); «Valorar» (1 a 5 estrellas y comentario) una vez terminado; descargar factura.
- **Validaciones**: la cancelación exige confirmación escribiendo el código o marcando una casilla; valoración con estrellas obligatorias y comentario opcional de hasta 500 caracteres.
- **Estados**: `quote_expired` → «El precio caducó» con «Pedir revisión de nuevo». Se refresca cada 30 s mientras esté en primer plano en estados que esperan al admin.

### 11 · Pago

- **Ruta y board**: `/{locale}/mis-trabajos/{id}/pagar?tipo=deposit|balance` · Pago.dc.html. \[C5.4, C5.7\]
- **Datos**: importe (señal del 30% o saldo), resumen del trabajo y Stripe Payment Element con tarjeta, MB WAY y Multibanco según `POST /api/jobs/{id}/payments`.
- **Acciones**: pagar. MB WAY: pide el teléfono y muestra «Acepta el pago en la app MB WAY» con cuenta atrás de 5 minutos. Multibanco: muestra entidad, referencia e importe con botón de copiar y la fecha límite; el trabajo queda en espera hasta el webhook.
- **Validaciones**: por encima de 1.000 €, aviso del límite diario habitual de MB WAY; por encima de 5.000 €, MB WAY no aparece.
- **Estados**: se considera pagado solo tras el webhook; mientras, «Confirmando el pago…» con consulta periódica. Pago fallido → mensaje de Stripe traducido y reintentar. Volver atrás no duplica pagos (mismo PaymentIntent).

### 12 · Cuenta del cliente

- **Ruta y board**: `/{locale}/cuenta` · Perfil.dc.html. \[C1.1, C4.3\]
- **Datos**: `GET /api/me`: nombre, email (con estado de confirmación), teléfono, NIF, razón social, idioma y consentimiento de marketing.
- **Acciones**: editar (`PUT /api/me`), cambiar contraseña, cambiar idioma (cambia también el de los emails), cerrar sesión y solicitar el borrado de la cuenta (lo gestiona el admin; RGPD).
- **Validaciones**: las mismas que en la pantalla 7. Cambiar el email exige confirmarlo de nuevo.

## Pintor

Zona `/{locale}/pintor`, rol `painter`. El pintor nunca ve el precio al cliente, el margen ni datos de otros pintores; solo su pago por trabajo.

### Alta por invitación

- **Ruta**: `/{locale}/pintor/alta?token=…` (sin board; formulario simple con el estilo de la pantalla 7). \[P1.1\]
- **Datos**: nombre y email precargados desde la invitación del admin.
- **Acciones**: definir contraseña, teléfono, foto, zonas en las que trabaja (concelhos) e IBAN; subir documentos informativos (seguro, NIF). `POST /api/painter/onboarding`.
- **Validaciones**: token vigente durante 7 días; IBAN portugués con dígito de control; documentos PDF o imagen de hasta 10 MB.
- **Estados**: token caducado → «Pide al equipo de Pinta una nueva invitación».

### P1 · Mis trabajos

- **Ruta y board**: `/{locale}/pintor` · PintorTrabajos.dc.html. \[P1.2, P1.3\]
- **Datos**: pestañas «Ofertas» (asignaciones `offered`), «Próximos», «En curso» y «Hechos» desde `GET /api/painter/assignments`. Tarjeta con localidad, fechas, m², tipo de trabajo y pago al pintor.
- **Acciones**: aceptar o rechazar una oferta (motivo opcional); abrir → P2 o P3 si es una visita.
- **Estados**: `assignment_taken` → «Este trabajo ya no está disponible» y la tarjeta desaparece. Vacío: «No tienes trabajos asignados ahora».

### P2 · Trabajo y ejecución

- **Ruta y board**: `/{locale}/pintor/trabajos/{id}` · PintorDetalle.dc.html. \[P2.1, P2.2\]
- **Datos**: dirección con enlace a mapas, notas de acceso, nombre y teléfono del cliente (botón de llamar), fecha, lista de elementos con medidas, paredes, pintura y litros previstos, fotos del cliente y notas del revisor. Material: lo entrega Pinta, con la cantidad por pintura.
- **Acciones**: «Empezar trabajo» (solo desde el día programado; `POST /api/painter/jobs/{id}/start`); subir fotos de avance y de resultado; «Marcar como terminado» con un mínimo de 3 fotos de resultado (`/finish`); reportar incidencia (texto y fotos, avisa al admin).
- **Validaciones**: terminar exige las fotos de resultado; no se puede empezar antes de la fecha.
- **Estados**: tras terminar queda en solo lectura con «Pendiente de cierre por Pinta».

### P3 · Visita técnica

- **Ruta y board**: `/{locale}/pintor/visitas/{id}` · PintorVisita.dc.html. \[P3.1\]
- **Datos**: lo mismo que P2 más la estimación original del cliente elemento por elemento.
- **Acciones**: corregir medidas, paredes, pintura y daños por elemento; añadir partidas extra propuestas (descripción y horas o importe al pintor); fotos; nota para el revisor. `POST /api/painter/site-visits/{id}/report`, que deja el trabajo listo para que el admin cierre el precio.
- **Validaciones**: las del editor de estancia (pantalla 3).
- **Estados**: borrador guardado localmente si se pierde la cobertura en la obra.

### P4 · Mis pagos

- **Ruta y board**: `/{locale}/pintor/pagos` · PintorPagos.dc.html. \[P4.1\]
- **Datos**: `GET /api/painter/payouts`: total del mes, pendiente y pagado, y lista por trabajo con importe y estado (`pending` o `paid` con fecha). IBAN enmascarado.
- **Acciones**: filtrar por mes; editar el IBAN (vuelve a pedir la contraseña).
- **Estados**: vacío → «Aún no hay pagos».

## Admin

Zona `/{locale}/admin`, rol `admin`, pensada para escritorio (usable en tablet). Navegación lateral: Trabajos, Revisión, Precios, Pintores, Cobertura, Finanzas, Ajustes y Trabajos en segundo plano (Hangfire). Los dos socios tienen los mismos permisos. Toda acción queda en el historial del trabajo con autor y fecha.

### A1 · Precios y simulador

- **Ruta y board**: `/{locale}/admin/precios` · AdminPrecios.dc.html. \[A1.1–A1.8\]
- **Datos**: pestañas Pinturas (precio por litro, rendimiento, manos, activa), Superficies (tarifa al cliente, pago al pintor y factor de dificultad), Ocupación, Tallas por tipo de estancia, Reglas (mínimo y máximo del trabajo, umbral Premium, desperdicio, IVA, rango, señal, margen mínimo, validez). Simulador lateral con un trabajo de ejemplo editable y el desglose completo de `POST /api/admin/quotes/simulate`.
- **Acciones**: editar en línea y «Guardar cambios» (`PUT /api/admin/catalog/*`, `PUT /api/admin/settings`). Los cambios solo afectan a las cotizaciones nuevas; las versiones existentes conservan su `snapshot_precios`. Nombres traducibles en pt, es y en.
- **Validaciones**: importes ≥ 0; porcentajes entre 0 y 100; mínimo < máximo; no se puede desactivar una pintura sugerida por defecto sin elegir otra.
- **Estados**: `concurrency_conflict` → «Otro socio cambió estos precios» con recargar. Cambios sin guardar → aviso al salir.
- **Pendiente**: el board menciona un «descuento de huecos» (puertas y ventanas), pero el documento 6.3 no define esa regla. No se implementa en H1; si se quiere, se define primero en `docs/pricing-engine.md` y en el script de referencia.

### A2 · Revisión de solicitudes

- **Ruta y board**: `/{locale}/admin/revision` y `/{locale}/admin/trabajos/{id}` · AdminRevision.dc.html. \[A2.1–A2.9\]
- **Datos**: cola de trabajos `in_review` ordenada por antigüedad, con semáforo del plazo de 2 días laborables. Detalle: datos del cliente, dirección, ocupación, fechas, elementos con fotos y vídeos (visor a pantalla completa), descripción y borrador de cotización con desglose interno (material, mano de obra, pago al pintor, margen y % con alerta por debajo del 20%).
- **Acciones**: ajustar elementos; añadir partidas extra (importe al cliente y al pintor); marcar Premium a mano; guardar borrador (`PUT …/draft-quote`); confirmar el precio y enviarlo al cliente (`POST …/confirm-quote`); pedir visita técnica eligiendo pintor, fecha y precio (`POST …/site-visit`); rechazar con motivo (`POST …/reject`); nota interna.
- **Validaciones**: confirmar con margen < 20% pide confirmación explícita; motivo obligatorio al rechazar.
- **Estados**: cola vacía → «Todo revisado».

### Tablero de trabajos (no dibujado)

- **Ruta**: `/{locale}/admin/trabajos`. \[A3.1–A3.5\]
- **Datos**: tabla filtrable por estado, fechas, localidad, pintor y Premium, con búsqueda por código, cliente o email. Columnas: código, cliente, localidad, estado, total, margen, pintor, fecha programada y última actividad. Exportar a CSV.
- **Acciones**: abrir el detalle (el mismo de A2 con pestañas Cotización, Versiones, Pagos, Asignación, Archivos e Historial); asignar u ofrecer a un pintor (`POST …/assignments`); programar fecha; forzar transición con motivo (`POST …/transition`, solo transiciones válidas de la tabla de estados); cancelar con reembolso calculado (`POST …/cancel`); cerrar el trabajo.
- **Estados**: transición inválida → `invalid_transition` traducido.

### Pintores (no dibujado)

- **Ruta**: `/{locale}/admin/pintores`. \[A4.1–A4.4\]
- **Datos**: lista con estado (invitado, activo, inactivo), zonas, trabajos hechos, valoración media y pagos pendientes. Ficha con datos, documentos, IBAN enmascarado y su historial.
- **Acciones**: invitar (nombre y email; envía el enlace de alta), reenviar la invitación, activar o desactivar, editar zonas.

### Cobertura (no dibujado)

- **Ruta**: `/{locale}/admin/cobertura`.
- **Datos**: concelhos del distrito de Lisboa con interruptor activo y prefijos postales asociados; lista de espera con exportación.
- **Acciones**: activar o desactivar zonas; añadir prefijos.

### Finanzas (no dibujado)

- **Ruta**: `/{locale}/admin/finanzas`. \[A5.1–A5.4\]
- **Datos**: por mes: cobrado, IVA, material, pagos a pintores, comisiones de Stripe, costes operativos por categoría, beneficio y reparto 50/50 entre socios. Lista de pagos a pintores pendientes.
- **Acciones**: añadir costes operativos (categoría cerrada, importe y nota); marcar un pago a pintor como hecho (fecha y referencia); cerrar el mes (`CIERRE_MENSUAL`, congela el reparto; exige confirmación); exportar CSV.
- **Validaciones**: un mes cerrado no admite cambios (`period_closed`).

### Ajustes (no dibujado)

- **Ruta**: `/{locale}/admin/ajustes`.
- **Datos y acciones**: datos de la empresa para facturas, teléfono del cliente visible al pintor (sí o no), plazo de revisión, días de antelación mínimos, plantillas de email por idioma (solo lectura en el MVP) y, solo en demo, «Restablecer datos de demo».

## Escritorio, componentes y estados globales

### Puntos de corte

| Ancho | Comportamiento |
| --- | --- |
| < 768 px | Una columna; barra inferior en el área del cliente y del pintor; hojas inferiores para editar. |
| 768–1023 px | Contenido centrado a 640 px; las hojas pasan a diálogos laterales. |
| ≥ 1024 px | Cliente: cabecera con navegación y sin barra inferior. Constructor en tres columnas (D2): esquema, editor de estancia y estimación fija, sin hojas inferiores. Detalle del trabajo en dos columnas (D3): estado y línea de tiempo a la izquierda, desglose y pago a la derecha. Landing en D1. Admin con navegación lateral. |

### Componentes compartidos

- `RoomSchema` y `RoomEditor`: el esquema y la vista cenital con paredes A–D seleccionables. Mismo componente en el cliente, en P3 y en A2 (en modo solo lectura o editable).
- `PriceRange`, `Money` y `Area`: formato de importes y m² por idioma.
- `JobStatusBadge` y `JobTimeline`: color e icono por `JobStatus`; un único mapa en el código.
- `MediaUploader` y `MediaGallery`: subida directa con URL firmada, progreso, límites y visor.
- `QuoteBreakdown`: con variantes `client`, `painter` (solo su pago) y `admin` (desglose completo y margen). La variante se decide en el servidor por rol; el cliente nunca recibe campos que no le tocan.
- `ConfirmDialog`, `EmptyState`, `ErrorState` y `Skeleton`.

### Estados globales

- **Toasts**: éxito (3 s), error (hasta cerrarlo) y deshacer (5 s). Arriba en móvil y abajo a la derecha en escritorio.
- **Sesión caducada**: un 401 en cualquier petición → diálogo «Tu sesión ha caducado» con «Entrar»; el borrador local se conserva.
- **Sin conexión**: banda superior «Sin conexión». El constructor sigue funcionando (el área se calcula en el cliente; la estimación queda pendiente). Las acciones que escriben quedan desactivadas.
- **PWA**: manifiesto con nombre, iconos y colores de la paleta B (`#0F766E` y `#F2B705`); el service worker cachea la estructura de la app y los textos, nunca las respuestas de la API con datos personales. Invitación a instalar tras enviar la primera solicitud.
- **Errores 404 y 500**: páginas propias traducidas con enlace a inicio.
- **Cookies**: solo técnicas en el MVP (sesión, antiforgery e idioma); banner informativo sin opciones de seguimiento.
