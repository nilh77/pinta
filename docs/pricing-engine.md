# Pinta — Fase 6.3: PRD · Motor de Cotización

> Fuente: documento 6.3 del PRD de Pinta (https://claude.ai/artifact/JkEwoPhH4KPJDXFmdA7UxA). Si este archivo y el documento difieren, manda el documento.

Especificación exacta del motor de precios: entradas, configuración, fórmulas, redondeos y 13 casos de prueba con el resultado esperado al céntimo. La implementación en .NET (`Pinta.Pricing`) debe reproducir estos números exactamente. Sustituye al ejemplo de la Fase 2 donde haya diferencias.

## Entradas y validaciones

Un trabajo es una lista de elementos más la ocupación y, tras la revisión, las partidas extra.

| Campo | Aplica a | Valores |
| --- | --- | --- |
| `kind` | Elemento | `room` (habitación o estancia de vivienda) o `surface` (superficie suelta) |
| `roomType` | room | `living`, `bedroom`, `kitchen`, `bathroom`, `hallway`, `other` |
| `size` | room | `S`, `M`, `L` — excluyente con medidas exactas |
| `length`, `width`, `height` | room exacta | Metros, 2 decimales. Alto por defecto 2,60 |
| `walls` | room | 4 booleanos A, B, C, D. A y C miden `length`; B y D miden `width` |
| `ceiling` | room | Booleano |
| `length`, `width` | surface | Metros; en una pared, `width` es el alto |
| `surfaceType` | surface | `interior_wall`, `ceiling`, `facade`, `wood`, `metal` |
| `paintId` | Elemento | Pintura del catálogo, activa |
| `damage` | Elemento | Booleano: humedad, grietas o desconchados declarados |
| `occupancy` | Trabajo | `empty`, `furnished`, `inhabited`. Superficies sueltas: `empty` por defecto |
| `extras[]` | Trabajo | Solo el admin: descripción, precio al cliente y pago al pintor, sin IVA |

**Validaciones (error 422 con el campo afectado):**

- Largo y ancho entre 0,50 y 30,00 m; alto entre 2,00 y 6,00 m; superficie suelta hasta 40 × 20 m.
- Una estancia debe tener al menos una pared o el techo seleccionado.
- Entre 1 y 30 elementos por trabajo.
- Pintura y tipo de superficie deben existir y estar activos.
- Importes de partidas extra mayores o iguales a 0, con 2 decimales.

## Configuración semilla

Valores de ejemplo para la demo y los tests. Todos se editan en el admin; los casos de prueba usan exactamente estos.

**Pinturas**

| Clave | Nombre | €/litro | Rendimiento m²/L | Manos | Sugerida en |
| --- | --- | --- | --- | --- | --- |
| `matte` | Plástica mate | 8,00 | 10 | 2 | Sala, dormitorio, pasillo, otra |
| `satin` | Plástica satinada | 11,00 | 10 | 2 | Cocina |
| `anti_damp` | Anti-humedad | 16,00 | 8 | 2 | Baño |
| `exterior` | Exterior acrílica | 14,00 | 9 | 2 | Fachada |

**Superficies**

| Clave | Nombre | Cliente €/m² | Pintor €/m² | Factor |
| --- | --- | --- | --- | --- |
| `interior_wall` | Pared interior | 6,00 | 4,00 | 1,00 |
| `ceiling` | Techo | 7,00 | 4,80 | 1,15 |
| `facade` | Fachada | 12,00 | 8,50 | 1,40 |
| `wood` | Madera | 9,00 | 6,00 | 1,20 |
| `metal` | Metal | 10,00 | 7,00 | 1,30 |

**Ocupación:** vacía ×1,00 · con muebles ×1,10 · habitada ×1,20.

**Tallas por tipo de estancia (largo × ancho en m; alto 2,60)**

| Tipo | S | M | L |
| --- | --- | --- | --- |
| Sala (`living`) | 4 × 3 | 5 × 4 | 6 × 5 |
| Dormitorio (`bedroom`) | 3 × 2,5 | 3,5 × 3 | 4,5 × 4 |
| Cocina (`kitchen`) | 3 × 2 | 3,5 × 3 | 4,5 × 3,5 |
| Baño (`bathroom`) | 2 × 1,5 | 2,5 × 2 | 3 × 2,5 |
| Pasillo (`hallway`) | 3 × 1 | 5 × 1,2 | 7 × 1,2 |
| Otra (`other`) | 2,5 × 2,5 | 3 × 3 | 4 × 3,5 |

**Globales**

| Parámetro | Valor |
| --- | --- |
| Desperdicio de material | 10% |
| IVA | 23% |
| Trabajo mínimo (IVA incluido) | 150,00 € |
| Trabajo máximo para precio instantáneo (IVA incluido) | 15.000,00 € |
| Umbral Premium | Más de 250 m² |
| Rango mostrado | −10% / +15% |
| Ampliación del rango con talla S/M/L | +10 puntos en el límite superior |
| Ampliación del rango con daños declarados | +10 puntos en el límite superior |
| Señal | 30% |
| Validez del precio | 15 días: confirmado el día D, vale hasta D+15 y el día D+16 a las 03:00 (Lisboa) pasa a «Expirado» |
| Margen mínimo (aviso al admin) | 20% |
| Precio de visita técnica | 0,00 €, descontable |
| Plazo de revisión | 2 días laborables |
| Cancelación | 100% hasta 7 días antes, 50% después |

## Algoritmo y redondeos

Todo se calcula en `decimal` y cada valor intermedio se redondea a 2 decimales con redondeo **hacia arriba en el punto medio** (`MidpointRounding.AwayFromZero`): 0,125 → 0,13.

1. **Líneas de superficie.** Cada elemento genera hasta dos líneas:
   - Estancia: una línea de paredes con tipo `interior_wall` y área = (suma de las paredes seleccionadas) × alto; y una línea de techo con tipo `ceiling` y área = largo × ancho. Con talla, las medidas salen de la tabla de tallas y el alto es 2,60.
   - Superficie suelta: una línea con su tipo y área = largo × ancho.
   - El área de cada línea se redondea a 2 decimales.
2. **Por cada línea** (pintura *p*, superficie *s*, ocupación *o*):
   - litros = área × manos(p) ÷ rendimiento(p) × (1 + desperdicio)
   - material = litros × €/litro(p)
   - mano de obra = área × tarifa cliente(s) × factor(s) × o
   - pago al pintor = área × tarifa pintor(s) × factor(s) × o
3. **Totales:** sumas de las líneas ya redondeadas (área, litros, material, mano de obra, pago al pintor).
4. **Base sin IVA** = material + mano de obra + partidas extra (precio al cliente).
5. **Mínimo:** mínimo sin IVA = mínimo con IVA ÷ (1 + IVA). Si la base es menor, el subtotal es el mínimo sin IVA y se marca `minimumApplied`.
6. **IVA** = subtotal × 23%. **Total** = subtotal + IVA.
7. **Pago total al pintor** = pago de las líneas + pago de las partidas extra.
8. **Margen bruto** = subtotal − material − pago total al pintor. **Margen %** = margen ÷ subtotal × 100, a 1 decimal.

El material se vende a precio de coste: el margen sale solo de la mano de obra, de las partidas extra y, si aplica, del mínimo.

## Reglas derivadas

| Regla | Definición |
| --- | --- |
| Rango para el cliente | Mínimo = total × (1 − 10%) redondeado **hacia abajo** al múltiplo de 5 €. Máximo = total × (1 + 15% + ampliaciones) redondeado **hacia arriba** al múltiplo de 5 € |
| Ampliaciones del rango | +10 puntos si alguna estancia usa talla S/M/L; +10 puntos si algún elemento declara daños. Acumulables |
| Trabajo mínimo | Si se aplica, el cliente ve el rango y la etiqueta "trabajo mínimo" |
| Máximo | Si el total supera 15.000 €, no hay rango: el cliente ve "cotización personalizada" (`overMax`) |
| Premium | Automático si el área total supera 250 m². El admin también puede marcarlo a mano |
| Aviso de margen | El admin ve un aviso si el margen % es menor que el margen mínimo (20%) |
| Ajuste manual | El admin puede fijar el subtotal a mano con motivo obligatorio; el IVA y el margen se recalculan sobre el nuevo subtotal, y con ellos el total, la señal y el resto. No se aplica el mínimo |
| Señal | Señal = total × 30%, a 2 decimales. Resto = total − señal |
| Reembolso por cancelación | 100% de la señal si no hay fecha programada o faltan 7 días o más; 50% si faltan menos de 7 días. Después de "En curso" no se puede cancelar |
| Visita técnica | Si tiene precio (IVA incluido), se cobra como pago aparte. Si es descontable y está pagada, la cotización que sigue a la visita resta del subtotal `R2(precio / (1 + IVA))`; el IVA, el total, el margen, la señal y el resto se calculan sobre el subtotal ya descontado. No se aplica el mínimo después del descuento |
| Lo que ve el cliente | Solo rango, total con IVA, señal, resto, áreas y partidas extra con su precio. Nunca tarifas, material desglosado, pago al pintor ni margen |

## Casos de prueba

Resultados esperados con la configuración semilla, en euros. Cada caso es un test del proyecto `Pinta.Pricing.Tests`.

| Caso | Entrada | Área m² | Material | Mano de obra | Subtotal | Total | Pintor | Margen | Rango | Señal | Marcas |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TC01 | Dormitorio exacto 4 × 3 × 2,6; 4 paredes + techo; mate; vacía | 48,40 | 85,20 | 315,00 | 400,20 | 492,25 | 211,84 | 103,16 (25,8%) | 440 – 570 | 147,68 | — |
| TC02 | TC01 con muebles | 48,40 | 85,20 | 346,50 | 431,70 | 530,99 | 233,02 | 113,48 (26,3%) | 475 – 615 | 159,30 | — |
| TC03 | TC01 habitada | 48,40 | 85,20 | 378,00 | 463,20 | 569,74 | 254,21 | 123,79 (26,7%) | 510 – 660 | 170,92 | — |
| TC04 | Dormitorio talla M; paredes A, B, C + techo; mate; vacía | 36,50 | 64,24 | 240,53 | 304,77 | 374,87 | 161,96 | 78,57 (25,8%) | 335 – 470 | 112,46 | Rango ampliado por talla |
| TC05 | Pared suelta 3 × 2,6; pared interior; mate | 7,80 | 13,76 | 46,80 | 121,95 | 150,00 | 31,20 | 76,99 (63,1%) | 135 – 175 | 45,00 | Trabajo mínimo |
| TC06 | Techo suelto 5 × 4; techo; mate | 20,00 | 35,20 | 161,00 | 196,20 | 241,33 | 110,40 | 50,60 (25,8%) | 215 – 280 | 72,40 | — |
| TC07 | Baño exacto 2,5 × 2 × 2,4; anti-humedad; daños | 26,60 | 117,12 | 169,85 | 286,97 | 352,97 | 114,00 | 55,85 (19,5%) | 315 – 445 | 105,89 | Rango ampliado por daños; aviso de margen |
| TC08 | Fachada 12 × 6; exterior | 72,00 | 246,40 | 1.209,60 | 1.456,00 | 1.790,88 | 856,80 | 352,80 (24,2%) | 1.610 – 2.060 | 537,26 | — |
| TC09 | Vivienda de 6 estancias (detalle abajo); con muebles | 266,44 | 573,41 | 1.893,81 | 2.467,22 | 3.034,68 | 1.272,67 | 621,14 (25,2%) | 2.730 – 4.100 | 910,40 | Premium; rango ampliado por talla y daños |
| TC10 | TC09 + partida extra anti-humedad (121,95 cliente / 75,00 pintor) | 266,44 | 573,41 | 1.893,81 | 2.589,17 | 3.184,68 | 1.347,67 | 668,09 (25,8%) | 2.865 – 4.300 | 955,40 | Premium |
| TC11 | 8 dormitorios talla L + sala talla L; mate; vacía | 584,80 | 1.028,96 | 3.865,50 | 4.894,46 | 6.020,19 | 2.603,68 | 1.261,82 (25,8%) | 5.415 – 7.530 | 1.806,06 | Premium |
| TC12 | Fachada 40 × 20; exterior | 800,00 | 2.737,84 | 13.440,00 | 16.177,84 | 19.898,74 | 9.520,00 | 3.920,00 (24,2%) | — | 5.969,62 | Supera el máximo; Premium |
| TC14 | TC10 con el subtotal ajustado a mano a 2.500,00 € | 266,44 | 573,41 | 1.893,81 | 2.500,00 | 3.075,00 | 1.347,67 | 578,92 (23,2%) | 2.765 – 4.155 | 922,50 | Ajuste manual; Premium |
| TC13 | Dormitorio sin paredes ni techo | — | — | — | — | — | — | — | — | — | Error 422 de validación |

**Vivienda de TC09:** sala exacta 5 × 4 (mate); cocina exacta 3,5 × 3 (satinada); dormitorio 1 exacto 4 × 3,5 (mate); dormitorio 2 talla M con paredes A, B, C + techo (mate); baño exacto 2,5 × 2 con daños (anti-humedad); pasillo exacto 6 × 1,2 solo paredes (mate). Todas con alto 2,60 y, salvo indicación, 4 paredes + techo.

**Reembolsos sobre una señal de 300,00 €:** sin fecha programada → 300,00; faltan 7 días → 300,00; faltan 6 días → 150,00; el mismo día → 150,00.

## Notas de implementación

- `Pinta.Pricing` es una librería sin dependencias: recibe la configuración y los elementos y devuelve un resultado inmutable con las líneas y los totales. No accede a la base de datos ni al reloj.
- La configuración que recibe es la copia congelada guardada en la cotización (`snapshot_precios`), así que recalcular una cotización antigua da siempre el mismo número.
- La estimación pública devuelve solo área, rango y marcas. El simulador y la revisión del admin devuelven el desglose completo.
- Los casos de prueba se implementan como tests parametrizados que comparan al céntimo.
- El repositorio incluye una implementación de referencia en Python (`docs/reference/pricing_reference.py`) que generó estos números. Si una regla cambia, se cambia primero ahí, se regeneran los resultados y después se actualiza el código .NET.
