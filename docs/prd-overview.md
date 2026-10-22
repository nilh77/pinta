# Pinta — Fase 6.1: PRD · Visión, Roles y Requisitos Generales

> Fuente: documento 6.1 del PRD de Pinta (https://claude.ai/artifact/VneZFpSh4M3JLsdraL5qWt). Si este archivo y el documento difieren, manda el documento.

Este es el documento de entrada del PRD del MVP demo de Pinta: fija qué debe demostrar la demo, quién puede hacer qué y los requisitos que aplican a todo el sistema. Sustituye a las fases 1–5 como referencia para programar cuando haya diferencias.

## Mapa del PRD

El PRD son siete documentos. Juntos con los de la fase 5 (arquitectura técnica) bastan para construir el MVP sin más decisiones de producto. Cada uno se copia al repositorio como Markdown en `docs/`, que es lo que lee Claude Code.

| Doc | Contenido | Archivo en el repo |
| --- | --- | --- |
| [6.1 Visión, roles y requisitos no funcionales](https://claude.ai/artifact/VneZFpSh4M3JLsdraL5qWt) | Este documento: objetivo de la demo, roles y permisos, alcance, RNF y riesgos | `docs/prd-overview.md` |
| [6.2 Requisitos funcionales](https://claude.ai/artifact/N7aXoZESRjfv7xcVedtuFb) | Historias de usuario con criterios de aceptación (C, A, P y S) | `docs/functional-requirements.md` |
| [6.3 Motor de cotización](https://claude.ai/artifact/JkEwoPhH4KPJDXFmdA7UxA) | Entradas, fórmulas, reglas y casos dorados TC01–TC13 | `docs/pricing-engine.md` + `docs/reference/pricing_reference.py` |
| [6.4 Pantallas](https://claude.ai/artifact/KMqXsZTTvgDWKeqEQW7sBJ) | Ficha por pantalla: ruta, datos, acciones, validaciones y estados | `docs/screens.md` |
| [6.5 Contrato de API](https://claude.ai/artifact/PQxF74tqJ87WYdmoCYCmt8) | Convenciones, endpoints, ejemplos, enumeraciones y códigos de error | `docs/api.md` |
| [6.6 Plan de construcción y demo](https://claude.ai/artifact/Ua7VQ9ouDSmyPeeMoWwE3e) | Hitos H0–H8, definición de hecho, seed y guion de la demo | `docs/plan.md` |
| [6.7 Glosario](https://claude.ai/artifact/WKG3pB9oaJryLXa6ydDLRK) | Un nombre por concepto en documentos, código y UI | `docs/glossary.md` |

De la fase 5 se copian también: [5.1 Stack](https://claude.ai/artifact/RZqFKj34TBu1z85af6DUei) → `docs/stack.md`, [5.2 Arquitectura](https://claude.ai/artifact/FHgNUvyWyDD2XxKhJXu5rx) → `docs/architecture.md`, [5.3 Modelo de datos](https://claude.ai/artifact/2kit1XZofSVHccZArkik82) → `docs/data-model.md` y `docs/states.md`, [5.4 Integraciones](https://claude.ai/artifact/FMiKCjg3oxgDnhCRWP9Prw) → `docs/integrations.md` y [5.5 Estructura](https://claude.ai/artifact/NJiGXfJg5U4JpgDNZufuDS) → `docs/structure.md`.

**Cómo usarlo con Claude Code.** El `CLAUDE.md` de la raíz resume las reglas que se aplican siempre y apunta a `docs/`. Cada hito del documento 6.6 trae su prompt con los archivos que hay que leer. Si se cambia una decisión, se cambia primero aquí, después en `docs/` y por último en el código. Si un documento y el código no coinciden, manda el documento hasta que se actualice.

## Objetivo de la demo

La demo debe convencer a los socios de que Pinta funciona de punta a punta: un cliente cotiza su casa en minutos, un experto confirma un precio rentable, el cliente paga y un pintor completa el trabajo, con las finanzas a la vista.

**La demo está lista cuando se puede recorrer, en local y sin pasos manuales en la base de datos, este circuito:**

1. Un visitante cotiza una vivienda de 6 estancias con el esquema visual y ve un rango de precio en menos de 2 minutos.
2. Sube fotos, indica dirección y fechas, crea su cuenta y envía la solicitud.
3. El admin revisa la solicitud, añade una partida extra, ve el margen en vivo y confirma el precio.
4. El cliente acepta y paga la señal con MB WAY en modo test; el pago se confirma por webhook.
5. El admin asigna un pintor y una fecha; el pintor acepta, inicia y finaliza el trabajo con fotos.
6. El cliente paga el resto y el trabajo queda cerrado.
7. El panel de finanzas muestra el margen del trabajo, los costes y el reparto 50/50 del mes.
8. Todo lo anterior funciona en PT, ES y EN, en móvil y en escritorio.
9. Un comando restablece los datos de demo para repetir la presentación.

## Roles y permisos

Cuatro roles. La API comprueba siempre el rol y, además, la pertenencia del recurso: un cliente solo accede a sus trabajos y un pintor solo a los asignados.

| Acción | Visitante | Cliente | Pintor | Admin |
| --- | --- | --- | --- | --- |
| Ver landing y cotizar (estimación) | Sí | Sí | — | Sí |
| Registrarse e iniciar sesión | Sí | — | Por invitación | Semilla |
| Enviar solicitud | Al crear cuenta | Sí | — | — |
| Ver sus trabajos, aceptar o rechazar precio, pagar, cancelar | — | Solo los suyos | — | — |
| Ver precio al cliente, margen y tarifas internas | — | Solo su precio | Nunca | Sí |
| Ver trabajos asignados, aceptar, iniciar, finalizar | — | — | Solo los suyos | Todos |
| Registrar visita técnica | — | — | Solo las suyas | Sí |
| Ver sus pagos como pintor | — | — | Solo los suyos | Todos |
| Ver teléfono y dirección del cliente | — | — | De sus trabajos (configurable) | Sí |
| Revisar solicitudes, confirmar o ajustar precios | — | — | — | Sí |
| Gestionar catálogo, configuración y cobertura | — | — | — | Sí |
| Gestionar pintores e invitaciones | — | — | — | Sí |
| Finanzas, costes y cierres | — | — | — | Sí |
| Panel de Hangfire | — | — | — | Sí |

Los dos socios tienen el rol Admin con los mismos permisos. El modelo admite diferenciarlos más adelante sin cambiar el esquema.

## Alcance

| Dentro del MVP demo | Fuera (después, si se avanza) |
| --- | --- |
| PWA responsive en PT, ES y EN | Apps nativas en tiendas |
| Cotización con esquema visual: superficie, habitación, vivienda | Plano a escala de la vivienda |
| Estimación instantánea y revisión humana de todas las solicitudes | Precio cerrado sin revisión |
| Fotos y vídeos por estancia | Procesado de vídeo o miniaturas |
| Cuentas con email o Google; pintores por invitación | Registro abierto de pintores |
| Pagos Stripe en modo test: tarjeta, MB WAY, Multibanco | Pagos reales y pago automático a pintores |
| Reembolsos según política de cancelación | Gestión de disputas desde la app |
| Panel admin: revisión, trabajos, pintores, precios, finanzas simuladas | Facturación certificada por la AT |
| Panel pintor mínimo | Calendario de disponibilidad de pintores |
| Emails transaccionales (Mailpit en local) | SMS, WhatsApp y notificaciones push |
| Datos de demo y reset | Despliegue en producción y Sentry |
| — | Valoraciones, chat, cuentas B2B con varias propiedades |

El detalle funcional de cada punto está en el documento 6.2.

## Requisitos no funcionales

Cada requisito tiene un objetivo medible; los que no pueden medirse en local se verifican antes del piloto.

| Área | Requisito |
| --- | --- |
| Rendimiento | Estimación de precio < 300 ms (p95) en local. Páginas públicas con LCP < 2,5 s en móvil 4G. Listados del admin paginados (25 por página) |
| Seguridad | Validación en servidor de toda entrada. Cookies HttpOnly, Secure, SameSite=Lax y antiforgery. Límite de peticiones en estimación (60/min por IP), login y registro (10/min por IP). Subidas solo de tipos permitidos (JPEG, PNG, HEIC, WebP, MP4, MOV) y tamaño máximo. Secretos fuera del repositorio. Errores sin detalles internos (ProblemDetails) |
| Privacidad (RGPD) | Consentimiento explícito de términos y privacidad al registrarse. Datos mínimos. Derecho de supresión: el cliente puede pedir borrar su cuenta, que se anonimiza conservando los datos contables. Fotos privadas con URLs firmadas. IBAN cifrado |
| Accesibilidad | Objetivo WCAG 2.2 AA: contraste 4,5:1, zonas táctiles de 44 px, navegación con teclado, etiquetas en formularios, el esquema de estancia usable con lector de pantalla (cada pared es un botón con estado) |
| Navegadores | Últimas 2 versiones de Chrome, Edge, Firefox y Safari; Safari iOS 16 o superior; Chrome Android |
| Idiomas | PT por defecto, ES y EN completos. Ningún texto de interfaz escrito en el código: todo en ficheros de traducción |
| Zona horaria y moneda | Fechas guardadas en UTC y mostradas en Europe/Lisbon. Moneda EUR con formato del idioma |
| PWA | Instalable (manifest e iconos). Sin modo sin conexión, salvo el borrador de cotización guardado en el dispositivo |
| Calidad | Motor de precios con cobertura de tests del 100%. Tests de integración de los flujos de la demo. Tests de arquitectura de las fronteras entre módulos |
| Observabilidad | Logs estructurados y health checks; panel de Aspire en local |

## Riesgos y preguntas abiertas

Ninguno bloquea empezar a programar: las cifras se cambian en el admin y lo legal llega en la Fase 8.

| Tema | Impacto | Responsable | Estado |
| --- | --- | --- | --- |
| Cifras reales de precios (tarifas, pinturas, mínimo, Premium) | La demo usa cifras de ejemplo | Socio | Pendiente |
| Revisión de las pantallas por el socio | Posibles ajustes de diseño | Socio | Pendiente |
| ¿Se paga al pintor la visita técnica? | Tarifa de visita del pintor | Socio | Pendiente |
| Empresa del socio y seguro de responsabilidad civil | Estructura para el piloto | Socio | Pendiente |
| Acuerdo de socios | Marco del negocio | Nathan y socio | Borrador por firmar |
| IVA aplicable a obras en vivienda (6% o 23%) | Precio final al cliente | Contabilista (Fase 8) | Abierto. La demo usa 23% configurable |
| Retención de fotos y datos | Política de privacidad | Fase 8 | Abierto. Propuesta: 2 años tras cerrar el trabajo |
| Proveedor de almacenamiento y email en producción | Coste y despliegue | Nathan (Fase 8) | Abierto. No afecta a la demo |
| Límites de MB WAY | Pagos de más de 1.000 € | Resuelto | Aviso en pantalla y Multibanco disponible |
| Tamaño del alcance para una persona | Plazo | Nathan | Mitigado con hitos que funcionan por sí solos (documento 6.6) |
