# Pinta — Estados del trabajo

> Extraído del documento 5.3 (https://claude.ai/artifact/2kit1XZofSVHccZArkik82). Nombres en código: ver docs/glossary.md (JobStatus). Esta tabla es la única fuente de la máquina de estados.

## Transiciones

Esta tabla es la especificación de la máquina de estados: cualquier transición que no aparezca aquí la rechaza el código. El diagrama del flujo está en [Fase 2: Motor de Cotización](https://claude.ai/artifact/FjcgXBnGyGm9cN3jpSG14R).

| De | A | Quién | Condición o efecto |
| --- | --- | --- | --- |
| (nuevo) | Pendiente de email | Cliente | Envía la solicitud sin email confirmado |
| (nuevo) o Pendiente de email | En revisión | Cliente / sistema | Email confirmado; empieza el plazo de 2 días laborables |
| En revisión | Visita técnica | Admin | Asigna visita a un pintor o al socio, con fecha |
| Visita técnica | En revisión | Pintor / socio | Envía medidas reales, fotos y observaciones |
| En revisión | Precio confirmado | Admin | Crea la versión confirmada; empieza la validez de 15 días |
| En revisión | Rechazado | Admin | Motivo obligatorio; email al cliente |
| Precio confirmado | Señal pagada | Sistema | Webhook de Stripe con pago correcto |
| Precio confirmado | Expirado | Sistema | Pasa la validez sin pago (tarea diaria) |
| Expirado | En revisión | Cliente | Pide recotización; se crea una nueva versión |
| Señal pagada | Programado | Admin / Pintor | Pintor asignado y aceptado, fecha fijada: el pintor al aceptar la oferta, o el admin a mano con fecha |
| Programado | En curso | Pintor | Marca el inicio del trabajo |
| En curso | Finalizado | Pintor | Al menos una foto del resultado; se pide el resto al cliente |
| Finalizado | Cerrado | Sistema | Webhook de Stripe con el resto pagado; se genera el pago al pintor |
| Cualquiera antes de En curso | Cancelado | Cliente / admin | Reembolso de la señal según la política: 100% hasta 7 días antes, 50% después |

Cada transición escribe una fila en el historial (quién, cuándo, motivo) y publica un evento interno que dispara emails y pagos.
