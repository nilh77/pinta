# Pinta — Fase 5.4: Integraciones

> Fuente: documento 5.4 del PRD de Pinta (https://claude.ai/artifact/FMiKCjg3oxgDnhCRWP9Prw). Si este archivo y el documento difieren, manda el documento.

Pinta se integra con cuatro servicios externos (Stripe, almacenamiento de objetos, email y Google para el login) y resuelve en casa la autenticación, las tareas programadas y los idiomas. Todo funciona en local con equivalentes de prueba.

## Autenticación y permisos

La autenticación es propia, con ASP.NET Core Identity, para no depender de un servicio externo.

| Pieza | Diseño |
| --- | --- |
| Sesión | Cookie HttpOnly, Secure, SameSite=Lax; token antiforgery en las peticiones que modifican datos |
| Login | Email y contraseña, o Google (login externo de Identity) |
| Confirmación de email | Se puede enviar la solicitud sin confirmar; el admin no la revisa hasta que el email está confirmado |
| Recuperación | Restablecer contraseña por email |
| Roles | Cliente (al registrarse), Pintor (solo por invitación, enlace válido 72 h), Admin (semilla: los dos socios, mismos permisos) |
| Protección | Bloqueo tras 5 intentos fallidos; claves de Data Protection guardadas en PostgreSQL |
| Autorización | Políticas por rol y comprobación por recurso: un cliente solo ve sus trabajos; un pintor solo los asignados |
| Datos por rol | Respuestas distintas por rol: el pintor nunca recibe el precio al cliente ni el margen |

## Pagos con Stripe

Un solo proveedor cubre los tres métodos de pago de la demo: tarjeta, MB WAY y Multibanco, en modo test.

| Pieza | Diseño |
| --- | --- |
| Interfaz | Payment Element de Stripe incrustado en la pantalla de pago |
| Importe | Calculado siempre en el servidor, en céntimos y en EUR |
| Confirmación | Solo por webhook (`payment_intent.succeeded` / `payment_failed`), con firma verificada y tabla de eventos contra repeticiones |
| Webhooks en local | Stripe CLI (`stripe listen`), arrancado por Aspire |
| Reembolsos | Por API con la política de cancelación aplicada en el servidor |
| Disputas | Se escucha `charge.dispute.created` y se avisa al admin |

**Límites de MB WAY** ([Stripe: MB WAY](https://docs.stripe.com/payments/mb-way)):

- Pagos de 0,50 € a 5.000 € por transacción.
- Límite diario por usuario de 1.000 € por defecto, ampliable hasta 10.000 € en su app.
- Reembolsos totales y parciales hasta 365 días después del pago.
- En el extracto del cliente aparece "Stripe" en lugar de "Pinta", porque MB WAY no admite descriptor personalizado. Se menciona en el email de confirmación.

**Reglas en la pantalla de pago:**

| Importe | Métodos ofrecidos |
| --- | --- |
| Hasta 1.000 € | Tarjeta, MB WAY, Multibanco |
| 1.000 € – 5.000 € | Tarjeta, MB WAY (con aviso del límite diario), Multibanco |
| Más de 5.000 € | Tarjeta, Multibanco |

**Multibanco** ([Stripe: Multibanco](https://docs.stripe.com/payments/multibanco)) genera una referencia que el cliente paga después en su banco o cajero. El pago se confirma por webhook cuando llega; mientras tanto, el trabajo sigue en "Precio confirmado" con el pago pendiente.

## Fotos y vídeos

Los archivos van directamente del móvil al almacenamiento; la API solo firma los permisos y guarda las referencias.

| Pieza | Diseño |
| --- | --- |
| Almacenamiento | S3-compatible con el SDK de AWS para .NET. MinIO en local y en el entorno Demo (servicio de Railway con dominio público, porque el navegador sube y lee con URLs firmadas: `Storage:PublicEndpoint`); proveedor de producción a elegir al desplegar (cambio por configuración) |
| Subida | Formulario POST firmado (política SigV4) válido 10 min: S3 rechaza otra clave, otro tipo o un archivo mayor del máximo; el móvil sube directo y luego confirma a la API |
| Límites | Fotos comprimidas en el navegador a unos 2 MP; vídeos de hasta 100 MB (unos 60 s); 10 fotos y 2 vídeos por estancia |
| Organización | Bucket privado. Las subidas del cliente van a `uploads/{borrador}/{uuid}.ext` y no se mueven al enviar: el trabajo se enlaza en la tabla de archivos. Visitas y resultados (H6) seguirán `jobs/{id}/{visita\|resultado}/{uuid}.ext` |
| Visualización | URLs firmadas de lectura de corta duración; ninguna foto es accesible sin permiso |
| Limpieza | Tarea nocturna que borra las subidas nunca confirmadas |

## Emails, idiomas y direcciones

**Emails.** Se envían a través de un transporte que elige `Email:Provider`:

- `Smtp` (por defecto): en local hacia Mailpit (se ven todos sin enviar nada real). La cadena de conexión `mailpit` es `Endpoint=smtp://host:puerto` con `Username` y `Password` opcionales; si el servidor ofrece STARTTLS se usa, y si hay usuario se autentica.
- `MailtrapApi`: por HTTPS a la API de envío del Sandbox de Mailtrap (`POST https://sandbox.api.mailtrap.io/api/send/{inbox}` con token Bearer), que deja los emails en un buzón de pruebas sin entregarlos a nadie. Es el del entorno Demo, porque Railway bloquea el SMTP saliente en sus planes de entrada. Se configura con `Email:MailtrapApi:Token` y `Email:MailtrapApi:InboxId`; cada envío espera como máximo 30 s y una respuesta que no sea 2xx cuenta como fallo, así que Hangfire reintenta igual que con SMTP.

En producción irá un proveedor transaccional. Las plantillas son componentes Razor renderizados a HTML, en el idioma del usuario, y todos los envíos pasan por Hangfire con reintentos.

| Email | Destinatario | Cuándo |
| --- | --- | --- |
| Confirmación de solicitud y de email | Cliente | Al enviar la solicitud: un solo email con el código y, si el email no está confirmado, el enlace de confirmación (el registro dentro del flujo no envía otro) |
| Precio confirmado | Cliente | Al confirmar el admin |
| Precio a punto de expirar | Cliente | 3 días antes del vencimiento |
| Pago recibido | Cliente | Webhook de pago correcto |
| Referencia Multibanco | Cliente | Al elegir Multibanco |
| Trabajo programado | Cliente | Fecha y pintor asignados |
| Trabajo finalizado, pagar el resto | Cliente | Al finalizar el pintor |
| Reembolso | Cliente | Al cancelar con señal pagada |
| Invitación | Pintor | Alta por el admin |
| Nueva asignación | Pintor | Al ofrecerle un trabajo |
| Nueva solicitud y rechazos | Admin | Nueva solicitud; pintor rechaza |
| Recuperar contraseña | Todos | A petición |

**Idiomas.**

- Rutas con idioma (`/pt`, `/es`, `/en`) y etiquetas `hreflang` para el SEO; PT por defecto.
- El idioma elegido se guarda en el usuario y se usa en los emails.
- La API devuelve los nombres del catálogo ya traducidos según el idioma de la petición.
- Números y monedas con el formato de cada idioma (`1.270,00 €` en PT y ES).

**Direcciones y cobertura.**

- Validación del código postal portugués (`NNNN-NNN`) y búsqueda de su concelho en la tabla de cobertura del admin.
- Fuera de cobertura: aviso y opción de dejar el email.
- Sin Google Maps en el MVP: el pintor tiene un enlace "Abrir en el mapa" con la dirección.

## Avisos y notificaciones push

Desde 2026-11-17, además de los emails, cada evento importante crea un **aviso** para quien le afecta. Se ve en la campanita de la app y, si el usuario lo activó en ese dispositivo, llega también como notificación push del navegador, aunque Pinta esté cerrada (`docs/screens.md`, «Avisos»).

- **Guardado y envío.**
  - El aviso se guarda en la misma operación que lo origina (`notifications.notifications`).
  - El push se envía después, en Hangfire, con 5 reintentos. Un fallo nunca deshace la operación.
  - Las suscripciones que el servicio de push da por caducadas (404 o 410) se borran.
- **Web Push estándar.**
  - Con claves VAPID: `Push__PublicKey`, `Push__PrivateKey` y `Push__Subject` (un `mailto:` o `https:` de contacto; un email sin `mailto:` se completa solo, y un valor no válido se cambia por `mailto:hello@pinta.pt`). En local van en `dotnet user-secrets`; en el entorno Demo, en las variables de Railway.
  - Sin claves, el push queda desactivado y la campanita funciona igual.
  - El texto del push va en el idioma del usuario.
  - La notificación lleva el icono de la app (la «p» blanca sobre verde) y, en la barra de Android, un icono pequeño monocromo: la «p» con el punto, en blanco sobre transparente (`web/public/icons/badge-96.png`, generado por `tools/pwa_icons.py`). Android solo usa la silueta de ese icono, así que uno a color sale como un cuadrado blanco. Vibra dos veces.
- **Dónde funciona.**
  - Chrome, Edge y Firefox en escritorio y Android, y Safari en macOS.
  - En iPhone y iPad solo con la PWA instalada en la pantalla de inicio (iOS 16.4 o posterior).
  - El service worker solo se registra en el build de producción, así que en `pnpm dev` no hay push.
- **Sin filtros por tipo** en el MVP: cada usuario activa o desactiva el push por dispositivo.
- **El push sigue a la sesión** (decisión de 2026-11-18): al cerrar sesión, la web borra en el servidor la suscripción de ese dispositivo antes de salir, así que deja de recibir los avisos de esa cuenta. El navegador conserva su suscripción y el permiso, y al volver a entrar, con la misma cuenta o con otra, el dispositivo se registra de nuevo a nombre de quien entra. Una sesión que caduca sin cerrarse mantiene el vínculo hasta que alguien entre o salga en ese dispositivo.

| Tipo (`NotificationKind`) | Admin | Pintor | Cliente | Cuándo |
| --- | --- | --- | --- | --- |
| `request_received` | ✓ | | | Una solicitud pasa a «En revisión» |
| `client_question` | ✓ | | | El cliente pulsa «Tengo dudas» |
| `quote_confirmed` | | | ✓ | El admin confirma el precio |
| `quote_rejected` | ✓ | | | El cliente rechaza el precio |
| `quote_expiring` | | | ✓ | Faltan 3 días para que caduque el precio |
| `quote_expired` | | | ✓ | El precio caduca |
| `request_rejected` | | | ✓ | El admin rechaza la solicitud |
| `site_visit_scheduled` | | | ✓ | Se programa la visita técnica |
| `site_visit_assigned` | | ✓ | | Se le asigna una visita técnica |
| `site_visit_reported` | ✓ | | | El pintor envía el informe de la visita |
| `multibanco_reference` | | | ✓ | Se genera una referencia Multibanco |
| `payment_received` | | | ✓ | Un pago se confirma (señal, resto o visita) |
| `deposit_paid` | ✓ | | | Se paga la señal |
| `balance_paid` | ✓ | | | Se paga el resto y el trabajo se cierra |
| `offer_received` | | ✓ | | Le ofrecen un trabajo |
| `offer_withdrawn` | | ✓ | | Se retira su oferta |
| `offer_accepted` | ✓ | | | El pintor acepta |
| `offer_declined` | ✓ | | | El pintor rechaza, con el motivo |
| `offer_unanswered` | ✓ | | | Una oferta lleva 48 h sin respuesta |
| `job_scheduled` | | | ✓ | Fecha y pintor fijados |
| `job_started` | ✓ | | | El pintor empieza el trabajo |
| `progress_photos` | ✓ | | ✓ | El pintor envía fotos de avance |
| `incident_reported` | ✓ | | | El pintor reporta una incidencia |
| `job_finished` | ✓ | | | El pintor marca el trabajo como terminado |
| `balance_due` | | | ✓ | Trabajo terminado: falta pagar el resto |
| `job_cancelled` | ✓ | ✓ | | El cliente cancela (admin), o se cancela un trabajo asignado (pintor) |
| `refund_issued` | | | ✓ | Se lanza un reembolso |
| `refund_completed` | ✓ | | | Stripe confirma el reembolso |
| `payment_dispute` | ✓ | | | Stripe abre una disputa |
| `payout_paid` | | ✓ | | El admin marca su pago como pagado |
| `account_deletion_requested` | ✓ | | | Un cliente pide la supresión de su cuenta |
| `reviews_overdue` | ✓ | | | Hay solicitudes con el plazo de revisión vencido |
| `painter_documents_expiring` | ✓ | | | Documentos de pintor que caducan en 30 días |

## Tareas programadas y observabilidad

| Tarea (Hangfire) | Frecuencia |
| --- | --- |
| Expirar precios no aceptados y avisar al cliente | Diaria |
| Recordatorio de precio a punto de expirar | Diaria |
| Avisos de documentos de pintor a punto de caducar | Semanal |
| Limpieza de subidas sin confirmar | Diaria |
| Envío de emails con reintentos | Bajo demanda |
| Envío de cada notificación push con reintentos (5) | Bajo demanda |
| Borrado de avisos leídos de más de 90 días | Diaria |

**Observabilidad:**

- Logs estructurados con Serilog.
- *Health checks* de API, PostgreSQL y almacenamiento: `/alive` (solo que el proceso responde) en todos los entornos, que Railway usa para dar por bueno un despliegue; `/health` (todas las comprobaciones) en `Development` y `Demo`.
- Panel de Hangfire, solo para admin, para ver tareas fallidas.
- En local, el panel de Aspire reúne logs y trazas. En producción se añade Sentry para errores y alertas (ver [Fase 5.1](https://claude.ai/artifact/RZqFKj34TBu1z85af6DUei)).

## Fuentes

Consultadas el 22 de octubre de 2026.

- [Stripe: MB WAY payments](https://docs.stripe.com/payments/mb-way)
- [Stripe: Multibanco payments](https://docs.stripe.com/payments/multibanco)
