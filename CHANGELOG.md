# Changelog

Registro de los cambios de Pinta. Cada push añade sus cambios en **Sin publicar**; al fusionar un hito en `main`, esa sección pasa a llevar el nombre del hito, la fecha y el PR.

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). Secciones por entrada: **Añadido**, **Cambiado**, **Corregido**, **Eliminado**. Cada línea dice qué cambia para quien usa o desarrolla Pinta y, si aplica, la historia (`A1.8`) o el documento afectado.

## Sin publicar

Nada todavía.

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
