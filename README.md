# Pinta

Plataforma mobile-first (PWA) para contratar trabajos de pintura en el distrito de Lisboa. Software de NHIT-DEV.

- `CLAUDE.md`: convenciones y reglas para desarrollar con Claude Code.
- `docs/`: PRD completo (empezar por `docs/prd-overview.md` y `docs/plan.md`).

## Requisitos

| Herramienta | Versión | Notas |
| --- | --- | --- |
| .NET SDK | 10.0.401 o superior (10.0.x) | Fijado en `global.json` |
| Node.js | 24 LTS | |
| pnpm | 12.15.0 | `corepack enable pnpm` (la versión la fija `web/package.json`) |
| Docker Desktop | Reciente | Aspire y los tests de integración (Testcontainers) lo necesitan **arrancado** |
| Stripe CLI | Opcional hasta H5 | Sin él, el AppHost lo omite con un aviso |

## Arranque

```bash
dotnet run --project src/Pinta.AppHost
```

La consola muestra el enlace de acceso al panel de Aspire (`http://localhost:15234/login?t=…`). Desde el panel:

| Recurso | Qué es |
| --- | --- |
| `web` | App Next.js. Abrir su URL: redirige a `/pt` |
| `api` | API ASP.NET Core. `/health`, `/openapi/v1.json` |
| `mailpit` | Bandeja con todos los emails enviados |
| `minio` | Almacenamiento S3 (se usa desde H3) |
| `postgres` / `pinta` | Base de datos, un esquema por módulo |

El navegador solo habla con `web`, que reenvía `/api/*` y `/hangfire` a la API. En desarrollo, las migraciones se aplican al arrancar y, con la base vacía, se crean los admins de demo.

El primer arranque descarga las imágenes de Docker y puede tardar unos minutos. En ese primer arranque la API registra unos errores de EF Core al buscar las tablas de migraciones, que aún no existen; son normales.

### Credenciales de demo (solo local)

| Rol | Email | Contraseña |
| --- | --- | --- |
| admin | `nathan@demo.pinta.pt` | `Pinta-Demo-2026` |
| admin | `socio@demo.pinta.pt` | `Pinta-Demo-2026` |

El panel de tareas está en `/hangfire` (a través de `web`) y solo entra un admin.

### Secretos

Nunca en el repositorio. Para reenviar webhooks de Stripe (desde H5):

```bash
dotnet user-secrets set "Parameters:stripe-secret-key" "sk_test_..." --project src/Pinta.AppHost
```

## Tests

```bash
dotnet test
```

```bash
cd web && pnpm install && pnpm lint && pnpm typecheck && pnpm test
```

- `tests/Pinta.ArchitectureTests`: fallan si un módulo referencia algo distinto de los `.Contracts` de otro módulo, `Pinta.SharedKernel` o `Pinta.Pricing`.
- `tests/Pinta.Api.IntegrationTests`: API real contra PostgreSQL en Docker.

## Cliente TypeScript de la API

La API escribe `src/Pinta.Api/openapi/Pinta.Api.json` al compilar. Tras cambiar un endpoint:

```bash
dotnet build src/Pinta.Api
```

```bash
cd web && pnpm api:generate
```

CI falla si el documento OpenAPI o el cliente generado no están al día.

## Frontend solo

```bash
cd web && pnpm dev
```

Sin Aspire, `/api` apunta a `http://localhost:5080` (cambiable con `API_URL`), así que la API tiene que estar en marcha con su base de datos.

## Notas

- MinIO dejó de publicar imágenes en Docker Hub y quay.io; el AppHost usa la imagen gratuita de Chainguard (`cgr.dev/chainguard/minio:latest`).
