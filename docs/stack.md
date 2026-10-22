# Pinta — Fase 5.1: Stack Tecnológico

> Fuente: documento 5.1 del PRD de Pinta (https://claude.ai/artifact/RZqFKj34TBu1z85af6DUei). Si este archivo y el documento difieren, manda el documento.

Pinta se construye como un monolito modular en ASP.NET Core sobre .NET 10 LTS con PostgreSQL, y un frontend Next.js + React + TypeScript que funciona como PWA. El stack está elegido para una sola persona que domina .NET.

## Stack elegido

Las versiones exactas de cada paquete se fijan al crear el proyecto (Fase 6).

| Pieza | Elección | Motivo |
| --- | --- | --- |
| Backend | ASP.NET Core sobre .NET 10 LTS | Dominio previo, rendimiento, soporte largo |
| Endpoints | Minimal APIs | Encajan con la organización por casos de uso |
| Organización | Monolito modular con *vertical slices* | Un solo despliegue, fronteras claras entre módulos |
| Base de datos | PostgreSQL + EF Core (Npgsql) | Barata de alojar; JSONB para la copia de precios |
| Frontend | Next.js + React + TypeScript, como PWA | SEO de la landing en 3 idiomas; app instalable |
| UI | Tailwind CSS + shadcn/ui | Traslado directo de los mockups; componentes accesibles |
| Datos en el front | TanStack Query, React Hook Form, Zod | Caché, formularios y validación |
| Contrato front–back | OpenAPI generado por .NET → cliente TypeScript tipado | Un cambio de API rompe la compilación del front, no la producción |
| Idiomas | next-intl (front), recursos .NET (emails) | PT por defecto, ES y EN |
| Autenticación | ASP.NET Core Identity + Google, propia | Sin depender de un servicio externo |
| Pagos | Stripe: Payment Element, Payment Intents, webhooks | Tarjeta, MB WAY y Multibanco con un solo proveedor |
| Archivos | Almacenamiento S3-compatible (MinIO en local) | Subida directa desde el móvil con URL firmada |
| Tareas programadas | Hangfire sobre PostgreSQL | Expiraciones, recordatorios, reintentos; panel incluido |
| Emails | Mailpit en local; proveedor transaccional en producción | Ver todos los emails sin enviar nada real |
| Entorno local | .NET Aspire | Un comando levanta todo; panel con logs y trazas |
| Tests | xUnit, Testcontainers, tests de arquitectura | Motor de precios 100% cubierto |
| Idioma del código | Inglés | Estándar; PT/ES/EN solo en la interfaz |

## Decisiones clave

**El precio se calcula solo en el backend.** El frontend calcula al instante los m², que son pura geometría, y pide el precio a la API con un pequeño retardo mientras el cliente toca paredes. Así hay una única implementación del motor y las tarifas internas nunca llegan al navegador.

**Monolito modular en lugar de microservicios.** Los microservicios resuelven problemas que Pinta no tiene (equipos grandes, escalar partes por separado, varios lenguajes) y añaden costes desde el primer día:

- operar 8–10 servicios con sus bases de datos;
- llamadas por red con fallos, timeouts y reintentos;
- consistencia entre servicios (sagas, *outbox*);
- depuración con trazas distribuidas;
- más infraestructura y coste.

El monolito modular mantiene las fronteras limpias (esquema, contratos y eventos por módulo) sin distribuir el sistema. Si un módulo necesita escalar aparte (Media y Notificaciones son los candidatos naturales), se extrae siguiendo sus fronteras ya definidas.

**Una sola app Next.js** con cuatro áreas: pública, cliente, pintor y admin. Cada área se protege por rol en un *middleware* y, sobre todo, en la API.

## Alternativas descartadas

| Alternativa | Por qué no |
| --- | --- |
| Blazor | Peor para una PWA móvil rápida y para el SEO de la landing; ecosistema de UI menor que React |
| Microservicios | Demasiada complejidad operativa para una persona y un MVP |
| Todo en TypeScript (Node) | Viable, pero pierde la productividad en .NET sin ventaja decisiva |
| React + Vite sin Next.js | Más simple de desplegar, pero la landing quedaría peor indexada |
| SQL Server | Más caro de alojar que PostgreSQL |
| Autenticación externa (Auth0, Clerk…) | Dependencia y coste mensual; Identity cubre lo necesario |

El coste asumido de esta elección son dos piezas que desplegar (API y frontend) en lugar de una; Aspire lo compensa en local.

## Observabilidad: Aspire y Sentry

Aspire y Sentry no compiten: cubren momentos distintos y son complementarios.

|  | .NET Aspire | Sentry |
| --- | --- | --- |
| Para qué | Orquestar y observar el sistema **mientras desarrollas** | Detectar errores y lentitud **en producción** |
| Dónde corre | En tu máquina | Servicio en la nube |
| Qué aporta | Levanta PostgreSQL, MinIO, Mailpit, API y web con un comando; panel con logs, trazas y estado | Avisa de cada error con contexto (usuario, petición, línea de código), en backend y frontend; alertas por email |
| Cuándo | Desde el primer día | Al desplegar para el piloto |

**Decisión:** Aspire desde la Fase 6. Sentry (o una alternativa equivalente) se añade al desplegar; la demo local no lo necesita. Como Aspire usa OpenTelemetry, la instrumentación ya estará hecha cuando llegue ese momento.
