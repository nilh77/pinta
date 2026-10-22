# Pinta — Fase 6.7: PRD · Glosario

> Fuente: documento 6.7 del PRD de Pinta (https://claude.ai/artifact/WKG3pB9oaJryLXa6ydDLRK). Si este archivo y el documento difieren, manda el documento.

## Uso del glosario

Los documentos están en español, el código en inglés y la interfaz en portugués (y español e inglés). Este glosario fija un único nombre para cada concepto en cada capa, para que Claude Code no invente sinónimos. Si un término nuevo aparece en el desarrollo, se añade aquí antes de usarlo.

Reglas de nombres en el código:

- C# en PascalCase para tipos y miembros; `enum` en PascalCase; en JSON se serializan en `snake_case` (documento 6.5).
- Tablas y columnas de PostgreSQL en `snake_case`, en inglés y en plural (`jobs`, `quote_versions`), dentro del esquema de su módulo (`jobs.jobs`, `catalog.paints`).
- Los importes llevan sufijo con su unidad: `TotalCents` en la API, `total_amount` como `numeric(12,2)` en la base; áreas con `M2` (`AreaM2`).
- Rutas web en el idioma de la interfaz solo para las páginas públicas que se indexan; el resto de rutas y todos los endpoints en inglés. En el MVP, las rutas de las pantallas del documento 6.4 se toman como clave interna y next-intl las traduce con `pathnames`.
- Las claves de traducción van por zona y en inglés: `quote.room.walls`, `job.status.in_review`.
- Nunca se usa «presupuesto» en el código: es `Quote`. «Señal» es `Deposit`. «Trabajo» y «solicitud» son el mismo `Job` en estados distintos.

## Estados del trabajo

| Español (documentos) | Código (`JobStatus`) | JSON | UI portugués | UI inglés |
| --- | --- | --- | --- | --- |
| Pendiente de confirmar email | `PendingEmail` | `pending_email` | A aguardar confirmação do email | Waiting for email confirmation |
| En revisión | `InReview` | `in_review` | Em revisão | In review |
| Visita técnica | `SiteVisit` | `site_visit` | Visita técnica | Site visit |
| Precio confirmado | `Quoted` | `quoted` | Preço confirmado | Price confirmed |
| Caducado | `Expired` | `expired` | Expirado | Expired |
| Rechazado | `Rejected` | `rejected` | Não aceite | Declined |
| Señal pagada | `DepositPaid` | `deposit_paid` | Sinal pago | Deposit paid |
| Programado | `Scheduled` | `scheduled` | Agendado | Scheduled |
| En curso | `InProgress` | `in_progress` | Em curso | In progress |
| Terminado | `Finished` | `finished` | Concluído | Finished |
| Cerrado | `Closed` | `closed` | Fechado | Closed |
| Cancelado | `Cancelled` | `cancelled` | Cancelado | Cancelled |

En la interfaz del cliente, «Rechazado» se muestra con una palabra suave («Não aceite», «No aceptado», «Declined») y siempre con el motivo. «Terminado» significa que el pintor acabó; «Cerrado», que Pinta cobró el saldo y dio el trabajo por cerrado.

## Términos de negocio y de dominio

| Español (documentos) | Código | UI portugués | UI inglés | Definición |
| --- | --- | --- | --- | --- |
| Trabajo / solicitud | `Job` | Trabalho / pedido | Job / request | Lo que pide un cliente, desde el envío hasta el cierre |
| Código de solicitud | `Job.Code` | Código do pedido | Request code | `PT-AAAA-NNNN`, correlativo por año |
| Cotización / presupuesto | `Quote` | Orçamento | Quote | Precio calculado de un trabajo |
| Versión de cotización | `QuoteVersion` | Versão do orçamento | Quote version | Cada cálculo guardado con su foto de precios |
| Foto de precios | `PriceSnapshot` | — | — | Tarifas congeladas en una versión (`snapshot_precios`) |
| Estimación | `Estimate` | Estimativa | Estimate | Rango orientativo mostrado al cliente antes de la revisión |
| Rango | `PriceRange` | Intervalo de preço | Price range | Mínimo y máximo de la estimación |
| Precio cerrado | `ConfirmedPrice` | Preço fechado | Final price | Total confirmado por el admin, válido 15 días |
| Elemento | `QuoteElement` | Elemento | Item | Una estancia o una superficie suelta |
| Estancia | `Room` | Divisão | Room | Habitación con paredes A–D y techo |
| Superficie suelta | `LooseSurface` | Superfície avulsa | Single surface | Pared, techo, fachada, madera o metal por largo × alto |
| Tipo de estancia | `RoomType` | Tipo de divisão | Room type | Sala, dormitorio, cocina, baño, pasillo, otra |
| Talla | `RoomSize` | Tamanho | Size | S, M o L cuando no se conocen las medidas |
| Pared A, B, C, D | `Walls` (`WallA`…) | Parede A, B, C, D | Wall A, B, C, D | A y C miden el largo; B y D, el ancho |
| Esquema | `RoomSchema` | Esquema da casa | Home layout | Vista simple de los elementos; no es un plano a escala |
| Tipo de superficie | `SurfaceType` | Tipo de superfície | Surface type | Define tarifa y factor de dificultad |
| Pintura | `Paint` | Tinta | Paint | Producto con precio por litro, rendimiento y manos |
| Rendimiento | `CoverageM2PerLitre` | Rendimento | Coverage | m² que pinta un litro por mano |
| Manos | `Coats` | Demãos | Coats | Número de capas |
| Desperdicio | `WastePct` | — | — | Margen de material, 10% por defecto |
| Factor de dificultad | `DifficultyFactor` | — | — | Multiplicador por superficie |
| Ocupación | `Occupancy` | Ocupação | Occupancy | Vacía, con muebles o habitada |
| Daños | `HasDamage` | Humidade, fissuras ou tinta a descascar | Damp, cracks or peeling | Amplía el rango y avisa al revisor |
| Partida extra | `QuoteExtra` | Trabalho adicional | Extra item | Importe añadido por el admin, con pago al pintor |
| Material | `MaterialCost` | Material | Materials | Pintura a precio de coste que aporta Pinta |
| Mano de obra | `LabourPrice` | Mão de obra | Labour | Tarifa al cliente por m² |
| Pago al pintor | `PainterPay` | Pagamento ao pintor | Painter pay | Tarifa fija por m² que cobra el pintor |
| Margen | `Margin` | — | — | Subtotal − material − pago al pintor; solo admin |
| Mínimo del trabajo | `MinJobAmount` | Valor mínimo | Minimum charge | 150 € con IVA |
| Máximo del trabajo | `MaxJobAmount` | — | — | 15.000 € con IVA; por encima, presupuesto a medida |
| Premium | `IsPremium` | Premium | Premium | Más de 250 m² o marcado a mano |
| Señal | `Deposit` | Sinal | Deposit | 30% del total, se paga para reservar |
| Saldo | `Balance` | Restante | Balance | Total − señal, al terminar |
| Reembolso | `Refund` | Reembolso | Refund | 100% hasta 7 días antes; 50% después |
| Visita técnica | `SiteVisit` | Visita técnica | Site visit | Pintor que mide en la obra antes del precio cerrado |
| Asignación | `Assignment` | Atribuição | Assignment | Oferta de un trabajo o visita a un pintor |
| Pintor | `Painter` | Pintor | Painter | Profesional asociado del equipo curado |
| Invitación | `PainterInvitation` | Convite | Invitation | Enlace de alta enviado por el admin |
| Liquidación al pintor | `Payout` | Pagamento | Payout | Pago de Pinta al pintor por un trabajo |
| Cobertura | `CoverageArea` | Zona de serviço | Service area | Concelhos y prefijos postales activos |
| Lista de espera | `WaitlistEntry` | Lista de espera | Waitlist | Contactos fuera de cobertura |
| Coste operativo | `OperatingCost` | — | — | Gasto del sistema, en categoría cerrada |
| Cierre mensual | `MonthlyClose` | — | — | Congela el mes y el reparto 50/50 |
| Reparto | `PartnerSplit` | — | — | Beneficio tras costes, 50% por socio |
| Valoración | `Review` | Avaliação | Review | 1 a 5 estrellas y comentario tras terminar |
| NIF | `TaxId` | NIF | Tax ID (NIF) | Identificación fiscal para factura B2B |

Los términos con «—» en la interfaz solo aparecen en el admin, que en el MVP usa los mismos textos traducidos con next-intl.

## Tablas y módulos

Los diagramas del documento 5.3 usan nombres descriptivos en español; esta es su traducción a la base de datos y al código. Las columnas siguen la misma regla (`precio_litro` → `price_per_litre`, `rendimiento` → `coverage_m2_per_litre`, `paredes_abcd` → `wall_a`…`wall_d`, `danos` → `has_damage`).

| Documento 5.3 | Esquema.tabla | Entidad C# | Módulo |
| --- | --- | --- | --- |
| USUARIO | `identity.users` (Identity) | `User` | Identity |
| TIPO\_PINTURA | `catalog.paints` | `Paint` | Catalog |
| TIPO\_SUPERFICIE | `catalog.surface_types` | `SurfaceType` | Catalog |
| TIPO\_ESTANCIA | `catalog.room_types` | `RoomType` | Catalog |
| PARTIDA\_EXTRA | `catalog.extra_items` | `ExtraItem` | Catalog |
| (ajustes) | `catalog.settings` | `PricingSettings` | Catalog |
| (cobertura) | `catalog.coverage_areas` | `CoverageArea` | Catalog |
| (lista de espera) | `catalog.waitlist_entries` | `WaitlistEntry` | Catalog |
| COTIZACION | `quoting.quote_versions` | `QuoteVersion` | Quoting |
| ESTANCIA | `quoting.quote_elements` | `QuoteElement` (estancia o superficie) | Quoting |
| LINEA\_EXTRA | `quoting.quote_extras` | `QuoteExtra` | Quoting |
| TRABAJO | `jobs.jobs` | `Job` | Jobs |
| HISTORIAL\_ESTADO | `jobs.status_history` | `JobStatusChange` | Jobs |
| VISITA\_TECNICA | `jobs.site_visits` | `SiteVisit` | Jobs |
| (valoración) | `jobs.reviews` | `Review` | Jobs |
| ASIGNACION | `painters.assignments` | `Assignment` | Painters |
| PINTOR | `painters.painters` | `Painter` | Painters |
| DOCUMENTO\_PINTOR | `painters.documents` | `PainterDocument` | Painters |
| (invitación) | `painters.invitations` | `PainterInvitation` | Painters |
| ARCHIVO | `media.files` | `MediaFile` | Media |
| PAGO | `payments.payments` | `Payment` | Payments |
| REEMBOLSO | `payments.refunds` | `Refund` | Payments |
| EVENTO\_STRIPE | `payments.stripe_events` | `StripeEvent` | Payments |
| (idempotencia) | `shared.idempotency_keys` | `IdempotencyRecord` | SharedKernel |
| (emails enviados) | `notifications.outbox` | `OutgoingEmail` | Notifications |
| PAGO\_PINTOR | `finance.painter_payouts` | `PainterPayout` | Finance |
| COSTE\_OPERACION | `finance.operating_costs` | `OperatingCost` | Finance |
| CIERRE\_MENSUAL | `finance.monthly_closes` | `MonthlyClose` | Finance |

Las filas entre paréntesis no aparecen en los diagramas del 5.3 y las añade el PRD (documentos 6.2 y 6.5). Hangfire crea su propio esquema `hangfire`.
