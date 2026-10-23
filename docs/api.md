# Pinta — Fase 6.5: PRD · Contrato de API

> Fuente: documento 6.5 del PRD de Pinta (https://claude.ai/artifact/PQxF74tqJ87WYdmoCYCmt8). Si este archivo y el documento difieren, manda el documento.

Contrato HTTP de la API de Pinta: convenciones comunes, todos los endpoints por rol con su historia de referencia, ejemplos de las peticiones clave y las enumeraciones. El OpenAPI generado por la API es la fuente final; este documento es lo que debe implementar.

## Convenciones

| Tema | Regla |
| --- | --- |
| Base | Todas las rutas bajo `/api`; el frontend las llama a través del proxy de Next.js en el mismo dominio |
| Formato | JSON con nombres en `camelCase`. Fechas y horas en ISO 8601 UTC (`2026-11-09T09:00:00Z`); fechas sin hora como `2026-11-09` |
| Importes | Enteros en **céntimos** con sufijo `Cents` (`totalCents: 49225`). Evita errores de coma flotante en el navegador y coincide con Stripe |
| Áreas y medidas | Números con 2 decimales en metros y m² |
| Idioma | Cabecera `Accept-Language` (`pt`, `es`, `en`); determina textos del catálogo y mensajes de error |
| Autenticación | Cookie de sesión de Identity. Peticiones que modifican datos llevan la cabecera `X-XSRF-TOKEN` obtenida de `GET /api/auth/antiforgery` |
| Errores | `application/problem+json` (RFC 9457) con `code` estable (tabla al final), `title` traducido y `errors` por campo en las validaciones |
| Estados HTTP | 200, 201 con `Location`, 204; 400 petición mal formada; 401 sin sesión; 403 sin permiso; 404 no existe o no es tuyo; 409 conflicto de estado; 422 validación; 429 límite de peticiones |
| Paginación | `?page=1&pageSize=25` (máximo 100). Respuesta `{ items, page, pageSize, total }` |
| Idempotencia | `POST /api/jobs` y la creación de pagos aceptan `Idempotency-Key` (UUID); repetirla devuelve el mismo resultado |
| Concurrencia | Recursos editables por el admin llevan `version`; una edición con versión antigua devuelve 409 |
| Identificadores | UUID en las rutas; el código visible (`PT-2026-0142`) solo como dato. En las peticiones de cotización, el catálogo se referencia por su clave estable (`paintId: "matte"`, `roomTypeId: "bedroom"`, `surfaceTypeId: "facade"`) |

## Públicos y cliente

| Método | Ruta | Acceso | Uso | Historia |
| --- | --- | --- | --- | --- |
| GET | `/api/auth/antiforgery` | Público | Obtiene el token antiforgery | S1 |
| POST | `/api/auth/register` | Público | Alta con email. `sendConfirmationEmail: false` en el flujo de cotización, donde el email de la solicitud lleva el enlace | C4.3 |
| POST | `/api/auth/login` | Público | Inicio de sesión | S1 |
| POST | `/api/auth/logout` | Sesión | Cierre de sesión | C5.8 |
| GET | `/api/auth/providers` | Público | Si el login con Google está configurado | C4.3 |
| GET | `/api/auth/google?returnUrl=` | Público | Inicia el login con Google (404 si no hay credenciales) | C4.3 |
| GET | `/api/auth/google/callback` | Público | Vuelta de Google | C4.3 |
| GET | `/api/auth/me` | Sesión | Usuario actual, rol e idioma | S1 |
| POST | `/api/auth/confirm-email` | Público | Confirma el email con token | C4.5 |
| POST | `/api/auth/resend-confirmation` | Sesión | Reenvía el email de confirmación | C4.5 |
| POST | `/api/auth/forgot-password` | Público | Pide enlace de recuperación | S1 |
| POST | `/api/auth/reset-password` | Público | Fija nueva contraseña con token | S1 |
| POST | `/api/auth/accept-invite` | Público | Pintor fija contraseña con la invitación (válida 72 h) y entra | A4.2 |
| GET | `/api/catalog` | Público | Tipos de estancia con tallas, pinturas, tipos de superficie y ocupaciones (sin tarifas) | C2.x |
| GET | `/api/coverage/check?postalCode=` | Público | Comprueba si el código postal está cubierto | C4.1 |
| POST | `/api/coverage/waitlist` | Público | Deja email para zonas sin cobertura | C4.1 |
| POST | `/api/quotes/estimate` | Público, límite 60/min | Calcula área, rango y marcas | C2.8 |
| POST | `/api/media/upload-url` | Público con límite / sesión | URL firmada de subida y `fileId` | C3.1 |
| POST | `/api/media/{fileId}/confirm` | Quien subió (`draftId`) | Confirma la subida | C3.1 |
| DELETE | `/api/media/{fileId}?draftId=` | Quien subió | Quita un archivo antes de enviar | C3.1 |
| POST | `/api/jobs` | Cliente | Envía la solicitud (idempotente) | C4.4 |
| GET | `/api/jobs` | Cliente | Mis trabajos, filtro `active` o `history` | C5.1 |
| GET | `/api/jobs/{id}` | Cliente dueño | Detalle con precio vigente, línea de tiempo, pintor y fotos (H3: resumen para la confirmación) | C5.2–C5.5 |
| POST | `/api/jobs/{id}/accept-quote` | Cliente dueño | Acepta el precio confirmado | C5.2 |
| POST | `/api/jobs/{id}/reject-quote` | Cliente dueño | Rechaza el precio, con motivo opcional | C5.2 |
| POST | `/api/jobs/{id}/questions` | Cliente dueño | "Tengo dudas": mensaje al admin | C5.2 |
| GET | `/api/jobs/{id}/cancellation-preview` | Cliente dueño | Importe a reembolsar si cancela | C5.6 |
| POST | `/api/jobs/{id}/cancel` | Cliente dueño | Cancela y lanza el reembolso | C5.6 |
| POST | `/api/jobs/{id}/requote` | Cliente dueño | Pide nuevo precio tras expirar | C5.7 |
| POST | `/api/jobs/{id}/payments` | Cliente dueño | Crea (o reutiliza si sigue abierto) el pago `deposit`, `balance` o `site_visit`; el importe lo calcula el servidor. Devuelve `clientSecret`, la clave publicable y los métodos permitidos por importe | C5.3 |
| GET | `/api/jobs/{id}/payments` | Cliente dueño | Pagos del trabajo y su estado | C5.3 |
| GET | `/api/jobs/{id}/payments/{paymentId}/receipt` | Cliente dueño o admin | Justificante PDF simulado de un pago hecho | C5.3 |
| GET | `/api/media/{fileId}/url` | Con acceso al trabajo | URL firmada de lectura | C5.5 |
| GET / PUT | `/api/me` | Cliente | Ver y editar nombre, email (cambiarlo pide confirmarlo de nuevo), teléfono, idioma, NIF y razón social por defecto y avisos | C5.8 |
| POST | `/api/me/password` | Cliente | Cambia la contraseña con la actual | C5.8 |
| POST | `/api/me/delete-request` | Cliente | Pide la supresión de la cuenta; avisa a los admins por email | C5.8 |
| POST | `/api/webhooks/stripe` | Stripe (firma) | Eventos de pago | S4 |

La estimación y la subida de archivos funcionan sin sesión para que el cliente pueda cotizar antes de registrarse; las subidas anónimas se asocian al trabajo al enviarlo.

## Pintor

Todas las rutas exigen rol Pintor y devuelven solo recursos asignados a él. Las respuestas no contienen precio al cliente ni margen.

| Método | Ruta | Uso | Historia |
| --- | --- | --- | --- |
| GET | `/api/painter/jobs` | Asignaciones por responder, próximos trabajos y visitas | P1.1 |
| GET | `/api/painter/jobs/{id}` | Detalle para ejecutar: dirección, contacto, estancias, litros, extras, fotos, importe a cobrar | P1.3 |
| POST | `/api/painter/assignments/{id}/accept` | Acepta la asignación | P1.2 |
| POST | `/api/painter/assignments/{id}/decline` | Rechaza con motivo | P1.2 |
| POST | `/api/painter/jobs/{id}/start` | Marca "En curso" | P2.1 |
| POST | `/api/painter/jobs/{id}/finish` | Marca "Finalizado" con `fileIds` del resultado | P2.2 |
| GET | `/api/painter/visits/{id}` | Datos de la visita (el id es el del trabajo) y lo declarado por el cliente, sin importes | P3.1 |
| POST | `/api/painter/visits/{id}/media/upload-url` | URL firmada para una foto de la visita (`site_visit`) | P3.1 |
| POST | `/api/painter/visits/{id}/report` | `{ elements, extras: [{ catalogKey?, description, painterPayCents }], fileIds, notes }`: vuelve a «En revisión» y crea el borrador `site_visit` | P3.1 |
| GET | `/api/painter/payouts?month=AAAA-MM` | Todo lo pendiente y lo pagado en el mes, con los pagos por trabajo (el IBAN enmascarado viene de `/api/painter/me`) | P4.1 |
| GET / PUT | `/api/painter/me` | Ficha propia para completar el alta: teléfono, zonas, NIF e IBAN (cambiar el IBAN pide la contraseña) | A4.2 |
| POST | `/api/painter/jobs/{id}/media/upload-url` | URL firmada para subir una foto del resultado (`result`) | P2.2 |

## Admin

Todas las rutas exigen rol Admin.

| Método | Ruta | Uso | Historia |
| --- | --- | --- | --- |
| GET / POST | `/api/admin/catalog/paints` | Listar y crear pinturas | A1.1 |
| PUT | `/api/admin/catalog/paints/{id}` | Editar o activar/desactivar | A1.1 |
| GET / POST / PUT | `/api/admin/catalog/surfaces[/{id}]` | Tipos de superficie | A1.2 |
| GET / POST / PUT | `/api/admin/catalog/room-types[/{id}]` | Tipos de estancia, tallas y pintura sugerida | A1.3 |
| GET / POST / PUT | `/api/admin/catalog/modifiers[/{id}]` | Modificadores de ocupación | A1.4 |
| GET / POST / PUT | `/api/admin/catalog/extras[/{id}]` | Catálogo de partidas extra | A1.5 |
| GET / PUT | `/api/admin/settings` | Configuración global, incluidos los datos de la empresa para los justificantes (nombre, NIF y dirección) | A1.6 |
| GET / POST / PUT | `/api/admin/coverage[/{id}]` | Zonas de cobertura | A1.7 |
| POST | `/api/admin/quotes/simulate` | Simulador con desglose completo | A1.8 |
| GET | `/api/admin/reviews` | Bandeja de revisión con semáforo | A2.1 |
| GET | `/api/admin/jobs` | Lista o tablero con filtros (`status` repetible en `snake_case`, `from`, `to`, `municipality`, `premium`) y búsqueda `q` por código, nombre o email | A3.1 |
| GET | `/api/admin/jobs/export` | Los mismos filtros en CSV (UTF-8) | A3.1 |
| GET | `/api/admin/partners` | Socios (admins) a los que asignar una visita técnica en H4 | A2.6 |
| GET | `/api/admin/jobs/{id}` | Detalle completo con desglose interno e historial | A2.2, A3.5 |
| PUT | `/api/admin/jobs/{id}/draft-quote` | Edita estancias, ocupación, extras, Premium y ajuste manual del borrador de revisión; devuelve el desglose | A2.3–A2.5, A2.7 |
| POST | `/api/admin/jobs/{id}/confirm-quote` | Confirma el precio con nota opcional; 422 `extras` si alguna partida libre del informe de visita sigue sin precio al cliente | A2.8 |
| POST | `/api/admin/jobs/{id}/reject` | Rechaza con motivo | A2.9 |
| GET | `/api/admin/jobs/{id}/site-visit-assignees` | Socios y pintores activos que cubren el concelho | A2.6 |
| POST | `/api/admin/jobs/{id}/site-visit` | Pide visita: asignado (socio o pintor, por su id de usuario) y fecha | A2.6 |
| POST | `/api/admin/jobs/{id}/site-visit/complete` | Visita hecha: vuelve a revisión; el siguiente borrador tiene origen `site_visit` | A2.6 |
| POST | `/api/admin/jobs/{id}/assignments` | Ofrece el trabajo a un pintor activo que cubre el concelho, con fecha `scheduledOn`; retira la oferta abierta anterior | A3.2 |
| POST | `/api/admin/jobs/{id}/transition` | Cambio de estado manual con motivo. `quoted` y `site_visit` usan su propia acción (422); `scheduled` exige `scheduledOn` (hoy o después) | A3.3 |
| GET | `/api/admin/jobs/{id}/payments` | Pagos, reembolsos y referencias Multibanco del trabajo | A3.5 |
| GET | `/api/admin/jobs/{id}/cancellation-preview` | Reembolso calculado | A3.4 |
| POST | `/api/admin/jobs/{id}/cancel` | Cancela, con opción de reembolso total y motivo | A3.4 |
| GET / POST | `/api/admin/painters` | Listar y dar de alta | A4.1 |
| GET / PUT | `/api/admin/painters/{id}` | Ficha y edición; activar o desactivar | A4.1–A4.4 |
| POST | `/api/admin/painters/{id}/invite` | Enviar o reenviar invitación | A4.2 |
| POST / DELETE | `/api/admin/painters/{id}/documents[/{docId}]` | Documentos con caducidad | A4.3 |
| GET | `/api/admin/finance/summary?month=AAAA-MM` | Panel del mes: trabajos cerrados y señales retenidas, material, pintores, margen, costes, beneficio y reparto 50/50; si el mes está cerrado, las cifras congeladas | A5.1 |
| GET / POST / PUT / DELETE | `/api/admin/finance/costs[/{id}]` | Costes de operación; en un mes cerrado, 422 `period_closed` | A5.2 |
| POST | `/api/admin/finance/closings` | `{ month }`: cierra un mes ya terminado y congela sus cifras (una vez) | A5.3 |
| GET | `/api/admin/finance/export?month=AAAA-MM` | CSV (UTF-8) del mes: trabajos, costes y totales | A5.1 |
| GET | `/api/admin/deletion-requests` | Clientes que pidieron la supresión de su cuenta | C5.8 |
| POST | `/api/admin/deletion-requests/{userId}/execute` | Anonimiza al cliente, cancela sus trabajos activos con la política y conserva la contabilidad | C5.8 |
| GET | `/api/admin/payouts?painterId=&status=` | Pagos a pintores (solo lectura desde H6b) | A5.4 |
| POST | `/api/admin/payouts/{id}/mark-paid` | `{ paidOn, reference }`: marca pagado con fecha (hoy o antes) y referencia de transferencia; ya pagado → 409 `invalid_transition`. Se permite aunque el mes esté cerrado (es caja) | A5.4 |
| GET | `/hangfire` | Panel de tareas (no es API) | — |

## Ejemplos

Los importes corresponden al caso TC01 del documento 6.3.

**Estimación pública** — `POST /api/quotes/estimate`

```json
{
  "occupancy": "empty",
  "elements": [
    {
      "kind": "room",
      "name": "Dormitorio",
      "roomTypeId": "bedroom",
      "length": 4.00, "width": 3.00, "height": 2.60,
      "walls": [true, true, true, true],
      "ceiling": true,
      "paintId": "matte",
      "damage": false
    }
  ]
}
```

Respuesta 200 (sin tarifas, material, pago al pintor ni margen):

```json
{
  "areaM2": 48.40,
  "elements": [ { "index": 0, "wallsAreaM2": 36.40, "ceilingAreaM2": 12.00, "areaM2": 48.40 } ],
  "rangeMinCents": 44000,
  "rangeMaxCents": 57000,
  "minimumApplied": false,
  "overMax": false,
  "premium": false,
  "rangeWidened": false
}
```

**Desglose del admin** — respuesta de `PUT /api/admin/jobs/{id}/draft-quote` y del simulador:

```json
{
  "lines": [
    { "element": 0, "surface": "interior_wall", "areaM2": 36.40, "litres": 8.01, "materialCents": 6408, "labourCents": 21840, "painterCents": 14560 },
    { "element": 0, "surface": "ceiling", "areaM2": 12.00, "litres": 2.64, "materialCents": 2112, "labourCents": 9660, "painterCents": 6624 }
  ],
  "extras": [],
  "materialCents": 8520,
  "labourCents": 31500,
  "subtotalCents": 40020,
  "vatCents": 9205,
  "totalCents": 49225,
  "painterTotalCents": 21184,
  "marginCents": 10316,
  "marginPct": 25.8,
  "belowMinMargin": false,
  "depositCents": 14768,
  "balanceCents": 34457,
  "rangeMinCents": 44000,
  "rangeMaxCents": 57000,
  "minimumApplied": false,
  "premium": false
}
```

**Envío de la solicitud** — `POST /api/jobs` con `Idempotency-Key`:

```json
{
  "draftId": "0193a1c0-…",
  "occupancy": "furnished",
  "elements": [ { "kind": "room", "name": "Sala", "roomTypeId": "living", "size": "M", "walls": [true, true, true, true], "ceiling": true, "paintId": "matte", "damage": false, "clientKey": "el-1" } ],
  "description": "Queremos la sala en blanco roto.",
  "address": { "line1": "Rua das Flores, 24", "floor": "2.º Esq.", "postalCode": "2710-123", "locality": "Sintra", "accessNotes": "Ascensor" },
  "preferredDates": { "asap": false, "from": "2026-11-09", "to": "2026-11-13" },
  "billing": { "taxId": null, "companyName": null }
}
```

Las fotos no viajan en la petición: se suben antes con `POST /api/media/upload-url` indicando el `draftId` y el `clientKey` del elemento (o ninguno para las generales), y al enviar se unen al trabajo las confirmadas de ese borrador.

Respuesta 201 con `Location: /api/jobs/{id}` y `{ "id": "…", "code": "PT-2026-0142", "status": "in_review" }` (o `pending_email`).

**Error de validación** — 422:

```json
{
  "type": "https://pinta.pt/errors/validation",
  "title": "Hay datos por corregir",
  "status": 422,
  "code": "validation_failed",
  "errors": { "elements[0].walls": ["Selecciona al menos una pared o el techo"] }
}
```

## Enumeraciones y códigos de error

Las enumeraciones viajan en JSON como cadenas en `snake_case`; en C# son `enum` en PascalCase con un conversor global. Añadir un valor es compatible; renombrarlo o quitarlo no.

| Enumeración | Valores |
| --- | --- |
| `JobStatus` | `pending_email`, `in_review`, `site_visit`, `quoted`, `expired`, `rejected`, `deposit_paid`, `scheduled`, `in_progress`, `finished`, `closed`, `cancelled` |
| `QuoteOrigin` | `estimate`, `admin_review`, `site_visit`, `revision` |
| `ElementKind` | `room`, `surface` |
| `RoomType` | `living`, `bedroom`, `kitchen`, `bathroom`, `hallway`, `other` |
| `RoomSize` | `S`, `M`, `L` (nulo si hay medidas exactas) |
| `SurfaceType` | `interior_wall`, `ceiling`, `facade`, `wood`, `metal` (códigos de catálogo; el admin puede añadir más) |
| `Occupancy` | `empty`, `furnished`, `inhabited` |
| `PaymentType` | `deposit`, `balance`, `site_visit` |
| `PaymentMethod` | `card`, `mb_way`, `multibanco` |
| `PaymentStatus` | `pending`, `requires_action`, `succeeded`, `failed`, `cancelled`, `refunded`, `partially_refunded` |
| `AssignmentStatus` | `offered`, `accepted`, `declined`, `withdrawn`, `completed` |
| `MediaContext` | `element`, `job_general`, `site_visit`, `result`, `painter_document` |
| `MediaKind` | `image`, `video`, `document` |
| `Role` | `client`, `painter`, `admin` |
| `OperatingCostCategory` | `infrastructure`, `payment_fees`, `accounting`, `insurance`, `marketing` (la lista cerrada del acuerdo de socios) |
| `PayoutStatus` | `pending`, `paid` |
| `Locale` | `pt`, `es`, `en` |

### Códigos de error

Toda respuesta de error es ProblemDetails con un `code` estable. El frontend traduce por `code`, nunca por `title`.

| HTTP | `code` | Cuándo |
| --- | --- | --- |
| 400 | `bad_request` | JSON mal formado o parámetro ilegible |
| 400 | `idempotency_key_required` | Falta `Idempotency-Key` en un POST que la exige |
| 401 | `unauthenticated` | Sin sesión o sesión caducada |
| 401 | `invalid_credentials` | Email o contraseña incorrectos (mensaje genérico) |
| 403 | `forbidden` | Rol sin permiso o recurso de otro usuario |
| 403 | `email_not_confirmed` | Acción que exige email confirmado |
| 403 | `antiforgery_failed` | Falta o no coincide `X-XSRF-TOKEN` |
| 404 | `not_found` | Recurso inexistente o no visible para el usuario |
| 409 | `invalid_transition` | Acción no permitida en el estado actual del trabajo |
| 409 | `concurrency_conflict` | Otro usuario modificó el recurso (versión distinta) |
| 409 | `idempotency_conflict` | Misma `Idempotency-Key` con un cuerpo distinto |
| 409 | `email_taken` | Email ya registrado |
| 409 | `assignment_taken` | La asignación ya fue aceptada o retirada |
| 410 | `quote_expired` | Precio confirmado fuera de su validez (15 días por defecto) |
| 413 | `file_too_large` | Imagen > 10 MB o vídeo > 100 MB |
| 415 | `unsupported_media_type` | Tipo de archivo no permitido |
| 422 | `validation_failed` | Datos inválidos; `errors` por campo |
| 422 | `media_limit_reached` | Más de 10 fotos o 2 vídeos por estancia |
| 422 | `out_of_coverage` | Código postal fuera de la zona |
| 422 | `over_max_amount` | Trabajo por encima del máximo; requiere presupuesto a medida |
| 422 | `payment_method_not_allowed` | Método no válido para el importe (p. ej. MB WAY > 5.000 €) |
| 422 | `payment_already_made` | El pago de ese tipo ya está completado |
| 422 | `refund_not_allowed` | Reembolso fuera de política o de plazo |
| 422 | `period_closed` | El mes ya está cerrado; los repartos no se editan |
| 429 | `rate_limited` | Demasiadas peticiones; incluye `Retry-After` |
| 502 | `payment_provider_error` | Stripe devolvió un error no recuperable |
| 500 | `internal_error` | Error inesperado; se registra con `traceId` |

Todas las respuestas incluyen `traceId` para cruzarlas con los logs de Serilog.
