# Changelog

Registro de los cambios de Pinta. Cada push añade sus cambios en **Sin publicar**; al fusionar un hito en `main`, esa sección pasa a llevar el nombre del hito, la fecha y el PR.

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). Secciones por entrada: **Añadido**, **Cambiado**, **Corregido**, **Eliminado**. Cada línea dice qué cambia para quien usa o desarrolla Pinta y, si aplica, la historia (`A1.8`) o el documento afectado.

## Sin publicar

### H3 · Envío de la solicitud — rama `h3-envio`

#### Añadido

- Pantalla 5 · Fotos y detalles: fotos y vídeos por estancia y generales, subida directa al almacenamiento con progreso, reintento y quitar; compresión a unos 2 MP en el dispositivo; descripción de hasta 2.000 caracteres (C3.1, C3.2).
- Pantalla 6 · Dirección y fecha: comprobación del código postal con su concelho, lista de espera fuera de cobertura, «Lo antes posible» o un rango de fechas a partir de 3 días laborables y de hasta 60 días (C4.1, C4.2).
- Pantalla 7 · Cuenta: crear cuenta o entrar dentro del flujo, login con Google si está configurado, factura a empresa con NIF validado (C4.3).
- Pantalla 8 · Confirmación con el código `PT-AAAA-NNNN`, próximos pasos, resumen y reenvío del email de confirmación (C4.4, C4.5).
- `POST /api/jobs` idempotente con `Idempotency-Key`: crea el trabajo, la versión de cotización «estimación» con la copia de precios y une las fotos del borrador. Queda «Pendiente de email» hasta confirmar el email y entonces pasa a «En revisión» con el plazo de revisión (C4.4, C4.5).
- Un solo email «Hemos recibido tu solicitud» en el idioma del cliente, con el enlace de confirmación cuando hace falta.
- Máquina de estados del trabajo con la tabla completa de `docs/states.md` e historial de cambios.
- Subidas firmadas a S3/MinIO con límites de tipo, tamaño y número; limpieza diaria de subidas sin confirmar.
- `GET /api/coverage/check`, `POST /api/coverage/waitlist` y el ajuste «antelación mínima de las fechas» en A1.

#### Cambiado

- Documentos: descripción de 2.000 caracteres, comprobación de cobertura por `GET`, días laborables de lunes a viernes, un solo email al enviar y nuevo formato de `POST /api/jobs` con `draftId`.

#### Pendiente

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
