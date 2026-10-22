# Pinta — Fase 5.2: Arquitectura y Módulos

> Fuente: documento 5.2 del PRD de Pinta (https://claude.ai/artifact/FHgNUvyWyDD2XxKhJXu5rx). Si este archivo y el documento difieren, manda el documento.

Pinta es un monolito modular: una API ASP.NET Core con nueve módulos y una librería pura para el motor de precios, detrás de una única app Next.js que comparte dominio con la API.

## Vista general

```
Navegador ──► web (Next.js, PWA) ──/api/*──► Pinta.Api (ASP.NET Core, Minimal APIs)
                                              ├─ Modules: Identity · Catalog · Quoting · Jobs · Painters
                                              │           Payments · Media · Notifications · Finance
                                              ├─ Pinta.Pricing (librería pura)
                                              └─► PostgreSQL (un esquema por módulo) · S3/MinIO · SMTP/Mailpit · Stripe
```

El navegador solo habla con Next.js, que sirve las páginas y reenvía `/api/*` a la API. La API concentra toda la lógica y es la única que toca la base de datos y los servicios externos.

## Módulos del backend

Cada módulo tiene su propio esquema en PostgreSQL (nombre del código en inglés entre paréntesis).

| Módulo | Responsabilidad |
| --- | --- |
| Identidad (Identity) | Usuarios, login con email y Google, roles, invitaciones a pintores |
| Precios (Catalog) | Configuración del admin: pinturas, superficies, estancias S/M/L, modificadores, extras, globales y zona de cobertura |
| Cotización (Quoting) | Estimación en vivo, copia congelada de precios al enviar, simulador del admin |
| Trabajos (Jobs) | Solicitud, revisión, visita técnica, asignación, máquina de estados e historial |
| Pintores (Painters) | Perfiles, especialidades, zonas, documentos, IBAN cifrado |
| Pagos (Payments) | Stripe (señal, resto, visita), webhooks, reembolsos, pagos pendientes a pintores |
| Media | URLs firmadas para subir y ver fotos y vídeos |
| Notificaciones (Notifications) | Emails en PT/ES/EN desde plantillas, con cola y reintentos |
| Finanzas (Finance) | Costes de operación, margen por periodo, reparto 50/50 y cierres mensuales |
| Motor de precios (Pricing) | Librería pura, sin dependencias: estancias + configuración → desglose. La usan Cotización y el simulador |

## Reglas entre módulos

Estas reglas evitan que el monolito se convierta en una maraña; un test de arquitectura falla si se incumplen.

1. **Cada módulo es dueño de sus tablas,** en su propio esquema. Ningún módulo lee ni escribe las tablas de otro.
2. **Sin claves foráneas entre esquemas.** Las referencias entre módulos son por ID y su integridad la garantiza el código. Así un módulo se puede extraer algún día sin romper la base de datos.
3. **Un módulo solo usa los `.Contracts` de otro** (interfaces, DTOs y eventos públicos), nunca su interior.
4. **Eventos internos para los efectos secundarios.** Ejemplo: `JobFinished` hace que Pagos solicite el resto y que Notificaciones envíe el email. Lo lento o lo que puede fallar va por Hangfire, con reintentos.
5. **La máquina de estados vive en el dominio de Trabajos.** Una transición no permitida la rechaza el código, no solo la interfaz.
6. **Respuestas distintas por rol.** El pintor recibe objetos sin precio al cliente ni margen: esos datos nunca salen de la API para él.

## Flujos clave

**Sesión.** Next.js reenvía `/api/*` a la API, de modo que navegador, páginas y API comparten origen. La sesión va en una cookie HttpOnly de ASP.NET Core Identity, sin tokens en el navegador y sin CORS.

**Subida de fotos y vídeos.**

1. El móvil pide a Media una URL firmada de subida (válida 10 min, con tipo y tamaño máximo).
2. Sube el archivo directamente al almacenamiento; nunca pasa por la API.
3. Confirma la subida a la API, que guarda la referencia en el trabajo.
4. Una tarea nocturna borra las subidas que nunca se confirmaron.

**Pago.**

1. La API calcula el importe (señal, resto o visita) y crea el Payment Intent en Stripe.
2. El cliente paga con tarjeta, MB WAY o Multibanco en el Payment Element.
3. Stripe envía el webhook; la API verifica la firma y descarta eventos repetidos.
4. Solo entonces el trabajo cambia de estado (por ejemplo, a "Señal pagada") y se envía el email.
