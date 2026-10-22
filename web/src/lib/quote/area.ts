/**
 * Areas of a quote element, computed in the browser with the same formula and rounding as `Pinta.Pricing`
 * (docs/pricing-engine.md, step 1). Area only: prices always come from the API.
 *
 * Measures have at most 2 decimals, so they are handled as integer centimetres and areas as integer hundredths of
 * m²: no floating-point drift, and rounding half-up matches `MidpointRounding.AwayFromZero` for positive values.
 */

export type Walls = readonly [boolean, boolean, boolean, boolean];

export type AreaInput =
  | { kind: "room"; length: number; width: number; height: number; walls: Walls; ceiling: boolean }
  | { kind: "surface"; length: number; width: number };

/** Hundredths of m² (4840 = 48,40 m²). */
export type ElementArea = { walls: number; ceiling: number; total: number };

const centimetres = (metres: number) => Math.round(metres * 100);

/** cm × cm = cm²; one hundredth of m² is 100 cm², rounded half-up. */
const hundredthsOfM2 = (aCm: number, bCm: number) => Math.round((aCm * bCm) / 100);

export function elementArea(input: AreaInput): ElementArea {
  const length = centimetres(input.length);
  const width = centimetres(input.width);
  if (input.kind === "surface") {
    const total = hundredthsOfM2(length, width);
    return { walls: 0, ceiling: 0, total };
  }

  // Walls A and C measure the length; B and D the width.
  const [a, b, c, d] = input.walls;
  const wallLength = (a ? length : 0) + (b ? width : 0) + (c ? length : 0) + (d ? width : 0);
  const walls = wallLength > 0 ? hundredthsOfM2(wallLength, centimetres(input.height)) : 0;
  const ceiling = input.ceiling ? hundredthsOfM2(length, width) : 0;
  return { walls, ceiling, total: walls + ceiling };
}

/** Totals are sums of rounded lines, like the engine. */
export const sumAreas = (areas: number[]) => areas.reduce((sum, area) => sum + area, 0);

export const toM2 = (hundredths: number) => hundredths / 100;
