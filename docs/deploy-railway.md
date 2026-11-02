# Pinta — Entorno Demo en Railway

El entorno **Demo** es la demo de `docs/plan.md` accesible en internet: la misma app que en local, con datos de demo, Stripe en modo test y los emails en un buzón de pruebas. No es producción: nada se cobra ni se entrega a nadie de verdad.

## Servicios

Un proyecto de Railway con cuatro servicios en la red privada (`<servicio>.railway.internal`) y un buzón SMTP externo.

| Servicio | Origen | Dominio público | Para qué |
| --- | --- | --- | --- |
| `postgres` | Plantilla PostgreSQL de Railway | No | Esquemas de los módulos, Hangfire y las claves de Data Protection (`identity`), así sesiones e IBAN cifrados sobreviven a cada despliegue |
| `minio` | Imagen `cgr.dev/chainguard/minio:latest`, volumen en `/data` | Sí, puerto 9000 | Fotos y vídeos. El navegador sube y lee con URLs firmadas, así que necesita dominio propio |
| `api` | Repo, raíz `/`, config `/src/Pinta.Api/railway.json` (Dockerfile `src/Pinta.Api/Dockerfile`) | **No** | Toda la lógica. Solo la alcanza la web |
| `web` | Repo, raíz `/web`, config `/web/railway.json` (Dockerfile `web/Dockerfile`) | Sí, puerto 3000 | Páginas y proxy de `/api` y `/hangfire` hacia la API |
| Email | Mailtrap Email Testing (u otro buzón SMTP de pruebas) | — | Los emails se ven en el buzón; no llegan a ningún destinatario |

```
Navegador ──https──► web (Next.js) ──http, red privada──► api ──► postgres
    │                                                       ├────► minio (red privada)
    └──https, URLs firmadas──► minio (dominio público)       └────► SMTP de pruebas · Stripe (test)
Stripe (test) ──webhook──► https://<web>/api/webhooks/stripe ──► api
```

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
| `MINIO_ROOT_PASSWORD` | `${{secret(32)}}` |
| Comando de arranque | `server /data --address :9000` |

**api**

| Variable | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Demo` |
| `ConnectionStrings__pinta` | `Host=${{postgres.PGHOST}};Port=${{postgres.PGPORT}};Database=${{postgres.PGDATABASE}};Username=${{postgres.PGUSER}};Password=${{postgres.PGPASSWORD}}` |
| `ConnectionStrings__minio` | `Endpoint=http://minio.railway.internal:9000;AccessKey=${{minio.MINIO_ROOT_USER}};SecretKey=${{minio.MINIO_ROOT_PASSWORD}}` |
| `Storage__PublicEndpoint` | `https://${{minio.RAILWAY_PUBLIC_DOMAIN}}` |
| `ConnectionStrings__mailpit` | `Endpoint=smtp://sandbox.smtp.mailtrap.io:2525;Username=…;Password=…` (credenciales SMTP del buzón de Mailtrap) |
| `Web__BaseUrl` | `https://${{web.RAILWAY_PUBLIC_DOMAIN}}` (enlaces de los emails) |
| `Seed__DemoPassword` | Contraseña común de las cuentas de demo; **no** la de local, porque la URL es pública |
| `Stripe__SecretKey` | `sk_test_…` |
| `Stripe__PublishableKey` | `pk_test_…` |
| `Stripe__WebhookSecret` | `whsec_…` del endpoint del paso 5 |

**web**

| Variable | Valor |
| --- | --- |
| `API_URL` | `http://${{api.RAILWAY_PRIVATE_DOMAIN}}:8080`. Next.js fija los rewrites al construir, así que también es argumento de build: un cambio pide redesplegar la web |

## Desplegar desde cero

1. **Cuentas (usuario).** Railway, Mailtrap (Email Testing → un inbox → credenciales SMTP) y el Dashboard de Stripe en **modo test**. En la terminal: `railway login` (o `RAILWAY_TOKEN` en el entorno).
2. **Proyecto.** `railway init` (nombre `pinta`), entorno `demo`. Conectar el repo `nilh77/pinta` de GitHub.
3. **postgres y minio.** Añadir la plantilla PostgreSQL. Añadir `minio` desde la imagen, con volumen en `/data`, sus variables, el comando de arranque y un dominio público al puerto 9000.
4. **api y web.** Dos servicios desde el repo con la raíz y el config de la tabla de servicios; variables de arriba (sin `Stripe__WebhookSecret` aún). Dominio público solo para `web`, al puerto 3000. Primero `api` (espera a `/alive`), luego `web`.
5. **Webhook de Stripe.** Developers → Webhooks → endpoint `https://<dominio de web>/api/webhooks/stripe` con los eventos `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.requires_action`, `charge.refunded`, `refund.updated` y `charge.dispute.created` (los mismos que la Stripe CLI en `src/Pinta.AppHost/AppHost.cs`). Copiar su `whsec_` a `Stripe__WebhookSecret` de `api`; Railway la redespliega.
6. **Comprobar** (abajo).

Cada push a la rama conectada redespliega el servicio cuyos archivos cambian (`watchPatterns` de cada `railway.json`).

## Comprobar

- `https://<web>/pt` abre a 390 px y a 1440 px; `https://<web>/api/catalog` responde JSON.
- Cotizar con fotos (suben a MinIO), crear la cuenta y confirmar el email desde Mailtrap.
- Entrar como `nathan@demo.pinta.pt` con `Seed__DemoPassword`, revisar la solicitud y confirmar el precio.
- Como cliente, aceptar y pagar la señal con la tarjeta `4242 4242 4242 4242`: el webhook aparece con 200 en Stripe y el trabajo pasa a señal pagada; el email del pago está en Mailtrap.
- Ajustes → «Restablecer datos de demo» deja el guion listo otra vez; `/hangfire` solo abre como admin.

## Diferencias con local

| | Local (Aspire) | Demo (Railway) |
| --- | --- | --- |
| Emails | Mailpit | Buzón SMTP de pruebas con usuario y STARTTLS |
| Webhooks de Stripe | Stripe CLI | Endpoint público con su propio `whsec_` |
| Secretos | `dotnet user-secrets` | Variables de Railway |
| Contraseña de demo | `Pinta-Demo-2026` | `Seed__DemoPassword` |
| Logs | Panel de Aspire | Logs de cada servicio en Railway |
