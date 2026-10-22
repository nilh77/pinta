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
| Almacenamiento | S3-compatible con el SDK de AWS para .NET. MinIO en local; proveedor de producción a elegir al desplegar (cambio por configuración) |
| Subida | URL firmada de subida válida 10 min, con tipo y tamaño máximo; el móvil sube directo y luego confirma a la API |
| Límites | Fotos comprimidas en el navegador a unos 2 MP; vídeos de hasta 100 MB (unos 60 s); 10 fotos y 2 vídeos por estancia |
| Organización | `trabajos/{id}/{cliente\|visita\|resultado}/{uuid}.ext` en un bucket privado |
| Visualización | URLs firmadas de lectura de corta duración; ninguna foto es accesible sin permiso |
| Limpieza | Tarea nocturna que borra las subidas nunca confirmadas |

## Emails, idiomas y direcciones

**Emails.** Se envían a través de una interfaz `IEmailSender`: en local hacia Mailpit (se ven todos sin enviar nada real) y en producción hacia un proveedor transaccional. Las plantillas son componentes Razor renderizados a HTML, en el idioma del usuario, y todos los envíos pasan por Hangfire con reintentos.

| Email | Destinatario | Cuándo |
| --- | --- | --- |
| Confirmación de solicitud y de email | Cliente | Al enviar la solicitud |
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

## Tareas programadas y observabilidad

| Tarea (Hangfire) | Frecuencia |
| --- | --- |
| Expirar precios no aceptados y avisar al cliente | Diaria |
| Recordatorio de precio a punto de expirar | Diaria |
| Avisos de documentos de pintor a punto de caducar | Semanal |
| Limpieza de subidas sin confirmar | Diaria |
| Envío de emails con reintentos | Bajo demanda |

**Observabilidad:**

- Logs estructurados con Serilog.
- *Health checks* de API, PostgreSQL y almacenamiento.
- Panel de Hangfire, solo para admin, para ver tareas fallidas.
- En local, el panel de Aspire reúne logs y trazas. En producción se añade Sentry para errores y alertas (ver [Fase 5.1](https://claude.ai/artifact/RZqFKj34TBu1z85af6DUei)).

## Fuentes

Consultadas el 22 de octubre de 2026.

- [Stripe: MB WAY payments](https://docs.stripe.com/payments/mb-way)
- [Stripe: Multibanco payments](https://docs.stripe.com/payments/multibanco)
