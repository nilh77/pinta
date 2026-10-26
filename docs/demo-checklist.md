# Lista para la demo

Guía práctica del [guion de la demo](plan.md#guion-de-la-demo), comprobada en los ensayos de H8. Todo en local y con Stripe en modo test.

## Antes (15 minutos)

1. **Arrancar**: Docker Desktop abierto y `dotnet run --project src/Pinta.AppHost`. En el panel de Aspire, esperar a que la API, la web, PostgreSQL, MinIO, Mailpit y Stripe CLI estén en verde.
2. **Restablecer**: entrar como `nathan@demo.pinta.pt` en Admin › Ajustes › «Restablecer datos de demo» y confirmar. Quedan los 13 trabajos de demo (PT-…-0091 a 0095 y 0101 a 0108) con fechas desde hoy; la siguiente solicitud será la PT-…-0121.
3. **Tres sesiones a la vez**: un mismo navegador comparte las cookies por dirección, así que cada rol va en su propia dirección (o en un dispositivo distinto). `PUERTO` es el de la web en el panel de Aspire:
   - Admin en el portátil: `http://localhost:PUERTO` (Nathan).
   - Cliente en el móvil: la IP del portátil en la red (`http://192.168.x.x:PUERTO`) o, en el mismo portátil, `http://127.0.0.1:PUERTO` con la vista móvil del navegador.
   - Pintor: `http://[::1]:PUERTO` o un segundo móvil, con `rui@demo.pinta.pt`.
   Las contraseñas de las cuentas de demo están en el seed (`src/Pinta.Api/Seed/DemoSeeder.cs`).
4. **En cada dirección**: cerrar una vez el aviso de cookies («Entendido»), que si no tapa los botones de abajo. Las sesiones de cliente y pintor de un ensayo anterior se cierran solas tras restablecer (esas cuentas ya no existen): el pintor vuelve a entrar con `rui@demo.pinta.pt`.
5. **Calentar Stripe**: abrir una vez cualquier pantalla de pago y volver. La primera carga del formulario de Stripe tarda unos 8 segundos.
6. **Abrir Mailpit** (enlace en el panel de Aspire) y la consola de Stripe en modo test.

## Durante, paso a paso

| Min. | Dónde | Qué hacer | Comprobar |
| --- | --- | --- | --- |
| 0–2 | Cliente · pantalla 0 | Cambiar de PT a ES. En «¿Llegamos a tu zona?», escribir `2710-405` (Sintra) | «Sí, llegamos a Sintra.» |
| 2–9 | Cliente · pantallas 1–4 | «Cotizar gratis» › «Una vivienda completa» › «Con muebles». Sala: tamaño M. Dormitorio: «Medidas exactas» 4,2 × 3,5 y quitar la pared D. Baño: «Hay humedad, grietas o desconchados» | El rango se mueve en cada cambio; en la estimación aparece el aviso de humedad del baño |
| 9–12 | Cliente · pantallas 5–8 | Una foto del baño. Dirección **en Lisboa** (p. ej. Rua da Prata, 80 · `1100-420`), «Lo antes posible». Cuenta nueva con un email inventado `@demo.pinta.pt` y enviar | Código PT-…-0121. En Mailpit, abrir «Hemos recibido tu solicitud» y pulsar el enlace de confirmación (si el enlace lleva a `localhost`, cambiarlo por la dirección del cliente) |
| 12–16 | Admin · Revisión | Abrir PT-…-0121: foto, desglose interno y margen. «Trabajos adicionales» › del catálogo «Tratamiento anti-humedad» › «Adicionar». «Confirmar precio» › «Confirmar y enviar» | El total y el margen se recalculan al añadir; la solicitud sale de la cola |
| 16–18 | Admin · Precios | Techo: cliente 7,00 → 8,00 › «Guardar alterações»; mirar el simulador; volver a 7,00 y guardar | La línea del techo del simulador sube (96,60 € → 110,40 €) y vuelve |
| 18–20 | Cliente · pantallas 10–11 | En «Mis trabajos», PT-…-0121: precio cerrado frente a la estimación. «Aceptar y pagar la señal» › MB WAY › teléfono `912345678` › «Pagar» | Stripe test aprueba solo en unos segundos: «Pago recibido» y el trabajo pasa a «Señal pagada» |
| 20–23 | Admin y pintor · P1–P2 | Admin › Trabajos › PT-…-0121 › «Atribuição»: Rui y una fecha › «Enviar oferta». Rui › «Ofertas» › «Aceitar». Después, Rui › PT-…-0106 › «Começar trabalho», tres fotos del resultado › «Marcar como terminado» | Rui ve solo localidad, fecha, m² y su pago; 0106 queda «Terminado» |
| 23–25 | Admin · Finanças | Mes anterior en el selector | Ingresos 1.647,43 €, margen 499,27 €, costes 63,93 €, beneficio 435,34 €, 217,67 € por socio |

Notas:

- La solicitud nueva debe estar en una zona de Rui (Lisboa, Oeiras o Amadora): la lista de pintores de «Atribuição» solo muestra a los que cubren la zona del trabajo. Con Sintra solo aparece Carla.
- En «Medidas exactas» los campos traen las medidas de la talla elegida: seleccionarlas antes de escribir.
- El teléfono de la cuenta ya trae `+351`.

## Plan B

- **Stripe o la red fallan**: enseñar el pago con PT-…-0105, ya pagado por MB WAY, y reenviar el webhook después con Stripe CLI.
- **Un paso se tuerce**: Admin › Ajustes › «Restablecer datos de demo» deja todo como al principio en unos segundos (los emails de Mailpit se quedan).
