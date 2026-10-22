# Pinta — Fase 5.5: Estructura del Proyecto y Entorno Local

> Fuente: documento 5.5 del PRD de Pinta (https://claude.ai/artifact/NJiGXfJg5U4JpgDNZufuDS). Si este archivo y el documento difieren, manda el documento.

Todo el código vive en un monorepo con la API .NET, el frontend Next.js y los tests. Con un solo comando, .NET Aspire levanta en local la base de datos, el almacenamiento, el correo, la API y la web, con datos de demo listos para presentar.

## Entorno local

El proyecto `Pinta.AppHost` (Aspire) orquesta todo con `dotnet run`: arranca los servicios, conecta las cadenas de conexión y abre un panel con logs, trazas y estado.

| Recurso | Tipo | Para qué |
| --- | --- | --- |
| PostgreSQL | Contenedor | Base de datos, un esquema por módulo |
| MinIO | Contenedor | Almacenamiento S3 de fotos y vídeos |
| Mailpit | Contenedor | Recibe y muestra los emails |
| Stripe CLI | Proceso | Reenvía los webhooks de Stripe test a la API |
| Pinta.Api | Proyecto .NET | API, módulos y Hangfire |
| web | App Next.js | Frontend PWA |

**Requisitos en tu máquina:** .NET 10 SDK, Node.js LTS, Docker Desktop, Stripe CLI y Git. Las versiones exactas se fijan al crear el proyecto.

## Estructura del repositorio

Un único repositorio Git; el código se escribe en inglés.

```
pinta/
├─ src/
│  ├─ Pinta.AppHost/                 Aspire: orquesta todo en local
│  ├─ Pinta.ServiceDefaults/         Logs, health checks, telemetría
│  ├─ Pinta.Api/                     Host: Program.cs, auth, registro de módulos
│  ├─ Pinta.Pricing/                 Motor de precios (sin dependencias)
│  ├─ Pinta.SharedKernel/            Money, IDs, eventos, Result, reloj
│  └─ Modules/
│     ├─ Identity/
│     ├─ Catalog/                    Precios: configuración del admin
│     ├─ Quoting/                    Cotización
│     ├─ Jobs/                       Trabajos y máquina de estados
│     ├─ Painters/
│     ├─ Payments/
│     ├─ Media/
│     ├─ Notifications/
│     └─ Finance/
├─ web/                              Next.js (PWA)
│  ├─ src/app/[locale]/(public)/     Landing con SSR
│  ├─ src/app/[locale]/(cliente)/    Cotizar, mis trabajos, cuenta
│  ├─ src/app/[locale]/pintor/
│  ├─ src/app/[locale]/admin/
│  ├─ src/components/ui/             shadcn/ui con la paleta de Pinta
│  ├─ src/components/constructor/    Esquema de vivienda y editor de estancia
│  ├─ src/lib/api/                   Cliente TypeScript generado desde OpenAPI
│  └─ messages/                      pt.json · es.json · en.json
└─ tests/
   ├─ Pinta.Pricing.Tests/           Unitarios del motor (cobertura total)
   ├─ Pinta.Api.IntegrationTests/    Testcontainers con PostgreSQL real
   └─ Pinta.ArchitectureTests/       Vigilan las fronteras entre módulos
```

## Estructura de un módulo

Cada módulo son dos proyectos: uno público con los contratos y uno privado con todo lo demás.

```
Modules/Jobs/
├─ Pinta.Modules.Jobs.Contracts/     Lo público: interfaces, DTOs, eventos
└─ Pinta.Modules.Jobs/               Lo privado
   ├─ Domain/                        Trabajo, máquina de estados, reglas
   ├─ Data/                          DbContext propio (esquema "jobs") y migraciones
   └─ Features/
      └─ ConfirmPrice/               Una carpeta por caso de uso
         ├─ ConfirmPriceEndpoint.cs  Minimal API
         ├─ ConfirmPriceRequest.cs   + validador
         └─ ConfirmPriceHandler.cs
```

**Regla de dependencias:** un módulo solo puede referenciar los `.Contracts` de otro módulo, `Pinta.SharedKernel` y, si lo necesita, `Pinta.Pricing`. Nunca el interior de otro módulo. `Pinta.ArchitectureTests` falla el build si se incumple.

## Base de datos, demo, secretos y flujo de trabajo

**Migraciones.** EF Core con un DbContext y sus migraciones por módulo, cada uno en su esquema. En desarrollo se aplican solas al arrancar.

**Semilla de demo** (solo en desarrollo):

- catálogo con las tarifas de ejemplo de la Fase 2;
- usuarios admin para los dos socios;
- 3–4 pintores con perfil completo;
- trabajos en todos los estados de la máquina;
- 6 meses de finanzas simuladas;
- fotos de muestra en MinIO.

Un comando de *reset* deja la demo como nueva antes de cada presentación.

**Secretos.** Claves de Stripe test y credenciales OAuth de Google en *user-secrets* de .NET, nunca en el repositorio.

**Flujo de trabajo.**

- Rama `main` siempre funcional; ramas cortas por funcionalidad.
- Commits pequeños y descriptivos.
- GitHub Actions desde el inicio: build y tests en cada push.
