# Pinta — Entorno Demo en Railway

El entorno **Demo** es la demo de `docs/plan.md` accesible en internet: la misma app que en local, con datos de demo, Stripe en modo test y los emails en un buzón de pruebas. No es producción: nada se cobra ni se entrega a nadie de verdad.

## Servicios

Un proyecto de Railway (`pinta`) con cuatro servicios en la red privada (`<servicio>.railway.internal`), todos en la región `europe-west4` (Países Bajos), y un buzón de emails de pruebas externo. El entorno de Railway se llama `production` porque es el que crea por defecto; lo que cuenta para la app es `ASPNETCORE_ENVIRONMENT=Demo`.

| Servicio | Origen | Dominio público | Para qué |
| --- | --- | --- | --- |
| `Postgres` | Plantilla PostgreSQL de Railway | No | Esquemas de los módulos, Hangfire y las claves de Data Protection (`identity`), así sesiones e IBAN cifrados sobreviven a cada despliegue |
| `minio` | Repo, raíz `/deploy/minio` (Dockerfile sobre `cgr.dev/chainguard/minio`), volumen en `/data` | Sí, puerto 9000 | Fotos y vídeos. El navegador sube y lee con URLs firmadas, así que necesita dominio propio |
| `api` | Repo, raíz `/`, Dockerfile `src/Pinta.Api/Dockerfile` | **No** | Toda la lógica. Solo la alcanza la web |
| `web` | Repo, raíz `/web`, Dockerfile `Dockerfile` | Sí, puerto 3000 | Páginas y proxy de `/api` y `/hangfire` hacia la API |
| Email | Mailtrap Email Sandbox, por su API HTTPS | — | Los emails se ven en el buzón; no llegan a ningún destinatario. Railway bloquea el SMTP saliente en los planes Free, Trial y Hobby, así que no se usa SMTP |

```
Navegador ──https──► web (Next.js) ──http, red privada──► api ──► postgres
    │                                                       ├────► minio (red privada)
    └──https, URLs firmadas──► minio (dominio público)       └────► API de Mailtrap (https) · Stripe (test)
Stripe (test) ──webhook──► https://<web>/api/webhooks/stripe ──► api
```

### Ajustes de cada servicio

Railway ya no usa `railway.json` (config as code); estos ajustes están en Settings de cada servicio. Todos: builder Dockerfile, región `europe-west4`, reinicio `ON_FAILURE` (5 intentos), rama `railway-demo` hasta fusionar el PR y después `main`.

| Servicio | Raíz | Dockerfile | Healthcheck | Watch paths |
| --- | --- | --- | --- | --- |
| `minio` | `/deploy/minio` | `Dockerfile` | `/minio/health/live` | `/deploy/minio/**` |
| `api` | `/` | `src/Pinta.Api/Dockerfile` | `/alive` | `/src/**`, `/Directory.Build.props`, `/Directory.Packages.props`, `/global.json` |
| `web` | `/web` | `Dockerfile` | `/pt` | `/web/**` |

## Qué hace la API en `Demo`

`ASPNETCORE_ENVIRONMENT=Demo` (`docs/architecture.md`, «Entornos»):

- Aplica las migraciones de todos los módulos al arrancar y siembra los datos de demo si la base está vacía.
- Ajustes → «Restablecer datos de demo» funciona igual que en local.
- Cookies `Secure`; la API toma `https` de la cabecera `X-Forwarded-Proto` que el borde de Railway pone y la web reenvía.
- `/alive` responde siempre (healthcheck de Railway) y `/health` comprueba PostgreSQL y el resto.
- Hangfire corre sobre el mismo PostgreSQL; su panel, en `https://<web>/hangfire`, solo para admin.
- Google queda desactivado mientras no haya `Authentication__Google__ClientId`: el botón no aparece.

## Variables

Los valores secretos los pega el usuario en Railway (Variables del servicio); nunca van al repo ni al chat. `${{servicio.VARIABLE}}` son referencias de Railway.

**minio**

| Variable | Valor |
| --- | --- |
| `MINIO_ROOT_USER` | Un nombre de usuario |
| `MINIO_ROOT_PASSWORD` | Aleatoria (`openssl rand -hex 24`), sin pasar por el chat |
| `PORT` | `9000` (el healthcheck de Railway usa `PORT`) |
| `RAILWAY_RUN_UID` | `0`: el volumen se monta como root y la imagen corre con otro usuario |
| `MINIO_BROWSER` | `off`: sin consola web |

**api**

| Variable | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Demo` |
| `PORT` | `8080` (el puerto del Dockerfile) |
| `ConnectionStrings__pinta` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}` |
| `ConnectionStrings__minio` | `Endpoint=http://${{minio.RAILWAY_PRIVATE_DOMAIN}}:9000;AccessKey=${{minio.MINIO_ROOT_USER}};SecretKey=${{minio.MINIO_ROOT_PASSWORD}}` |
| `Storage__PublicEndpoint` | `https://${{minio.RAILWAY_PUBLIC_DOMAIN}}` |
| `Email__Provider` | `MailtrapApi` |
| `Email__MailtrapApi__InboxId` | El número del inbox (está en su URL de Mailtrap: `…/inboxes/<número>/messages`) |
| `Email__MailtrapApi__Token` | Un API token de Mailtrap con acceso a ese inbox (Settings › API Tokens) |
| `Web__BaseUrl` | `https://${{web.RAILWAY_PUBLIC_DOMAIN}}` (enlaces de los emails) |
| `Seed__DemoPassword` | Contraseña común de las cuentas de demo; **no** la de local, porque la URL es pública |
| `Stripe__SecretKey` | `sk_test_…` |
| `Stripe__PublishableKey` | `pk_test_…` |
| `Stripe__WebhookSecret` | `whsec_…` del endpoint del paso 5 |

**web**

| Variable | Valor |
| --- | --- |
| `PORT` | `3000` |
| `API_URL` | `http://${{api.RAILWAY_PRIVATE_DOMAIN}}:8080`. Next.js fija los rewrites al construir, así que también es argumento de build: un cambio pide redesplegar la web |

## Desplegar desde cero

1. **Cuentas (usuario).** Railway, Mailtrap (Email Sandbox → un inbox; Settings › API Tokens → un token con acceso a ese inbox) y el Dashboard de Stripe en **modo test**. En la terminal: `railway login` (o `RAILWAY_TOKEN` en el entorno).
2. **Proyecto.** `railway init --name pinta`. Railway necesita acceso de su app de GitHub al repo `nilh77/pinta`.
3. **Postgres y minio.** `railway add --database postgres`; `railway add --service minio --repo nilh77/pinta` con sus variables, un volumen en `/data` (`railway volume add --mount-path /data` con el servicio enlazado) y un dominio público al puerto 9000 (`railway domain -s minio -p 9000`).
4. **api y web.** `railway add --service api --repo nilh77/pinta` y lo mismo para `web`, con las variables de arriba (sin `Stripe__WebhookSecret` aún) y los ajustes de la tabla. Dominio público solo para `web`, al puerto 3000. Primero `api` (espera a `/alive`), luego `web`. Los ajustes pueden ponerse en el panel o con un parche de entorno por la API (`railway api` → `environmentPatchCommit`), porque `railway environment edit` de la CLI 5.63 no los aplica.
5. **Webhook de Stripe.** Developers → Webhooks → endpoint `https://<dominio de web>/api/webhooks/stripe` con los eventos `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.requires_action`, `charge.refunded`, `refund.updated` y `charge.dispute.created` (los mismos que la Stripe CLI en `src/Pinta.AppHost/AppHost.cs`). Copiar su `whsec_` a `Stripe__WebhookSecret` de `api`; Railway la redespliega.
6. **Comprobar** (abajo).

Cada push a la rama conectada redespliega el servicio cuyos archivos cambian (watch paths de la tabla).

## Comprobar

- `https://<web>/pt` abre a 390 px y a 1440 px; `https://<web>/api/catalog` responde JSON.
- Cotizar con fotos (suben a MinIO), crear la cuenta y confirmar el email desde Mailtrap.
- Entrar como `nathan@demo.pinta.pt` con `Seed__DemoPassword`, revisar la solicitud y confirmar el precio.
- Como cliente, aceptar y pagar la señal con la tarjeta `4242 4242 4242 4242`: el webhook aparece con 200 en Stripe y el trabajo pasa a señal pagada; el email del pago está en Mailtrap.
- Ajustes → «Restablecer datos de demo» deja el guion listo otra vez; `/hangfire` solo abre como admin.

## Diferencias con local

| | Local (Aspire) | Demo (Railway) |
| --- | --- | --- |
| Emails | Mailpit por SMTP | Inbox del Sandbox de Mailtrap por su API HTTPS |
| Webhooks de Stripe | Stripe CLI | Endpoint público con su propio `whsec_` |
| Secretos | `dotnet user-secrets` | Variables de Railway |
| Contraseña de demo | `Pinta-Demo-2026` | `Seed__DemoPassword` |
| Logs | Panel de Aspire | Logs de cada servicio en Railway |
