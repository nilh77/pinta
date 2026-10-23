import type { AdminQuote, DraftQuoteRequest, Schemas } from "./api";

export type DraftElement = Schemas["QuoteElementRequest"];
export type DraftExtra = Schemas["DraftExtraRequest"];

/** The editor's state from a stored version: what PUT draft-quote takes back. */
export function toDraftRequest(quote: AdminQuote): DraftQuoteRequest {
  return {
    occupancy: quote.occupancy,
    elements: quote.elements.map(
      ({ kind, name, roomTypeId, size, length, width, height, walls, ceiling, surfaceTypeId, paintId, damage, clientKey }): DraftElement => ({
        kind,
        name,
        roomTypeId,
        size,
        length,
        width,
        height,
        walls,
        ceiling,
        surfaceTypeId,
        paintId,
        damage,
        clientKey,
      }),
    ),
    // A free item from the painter's visit report keeps its client price unset (null) until the admin types it (P3.1).
    extras: quote.extras.map(({ catalogKey, description, clientPriceCents, painterPayCents, clientPriceToSet }) => ({
      catalogKey,
      description,
      clientPriceCents: clientPriceToSet ? null : clientPriceCents,
      painterPayCents,
    })),
    premium: quote.premiumOverride,
    manualSubtotalCents: quote.manualSubtotalCents,
    adjustmentReason: quote.adjustmentReason,
  };
}

/** A room switches between a size (S/M/L) and exact measures; measures start from the size's dimensions. */
export function withMeasures(element: DraftElement, sizes: Record<string, [number, number]> | undefined, defaultHeight: number): DraftElement {
  if (element.size == null) return { ...element, size: "M", length: null, width: null, height: null };
  const [length, width] = sizes?.[element.size] ?? [4, 3];
  return { ...element, size: null, length, width, height: element.height ?? defaultHeight };
}

/** Euros typed by the admin ("1.234,5" or "1234.50") to cents; null when empty or not a number. */
export function parseEurosToCents(text: string): number | null {
  const trimmed = text.trim();
  if (!trimmed) return null;
  const normalized = trimmed.includes(",") ? trimmed.replace(/\./g, "").replace(",", ".") : trimmed;
  if (!/^\d+(\.\d{0,2})?$/.test(normalized)) return null;
  return Math.round(Number(normalized) * 100);
}
