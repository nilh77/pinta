import { describe, expect, it } from "vitest";
import { elementArea, sumAreas, toM2, type AreaInput } from "./area";

const all = [true, true, true, true] as const;
const room = (length: number, width: number, height = 2.6, walls: readonly boolean[] = all, ceiling = true): AreaInput => ({
  kind: "room",
  length,
  width,
  height,
  walls: [walls[0], walls[1], walls[2], walls[3]],
  ceiling,
});

/** Element areas of the golden cases in docs/pricing-engine.md, in m². */
describe("elementArea (same rounding as Pinta.Pricing)", () => {
  it("TC01 bedroom 4 × 3 × 2,6: walls 36,40 + ceiling 12,00", () => {
    const area = elementArea(room(4, 3));
    expect([toM2(area.walls), toM2(area.ceiling), toM2(area.total)]).toEqual([36.4, 12, 48.4]);
  });

  it("TC04 bedroom size M (3,5 × 3), walls A, B, C + ceiling: 36,50", () => {
    expect(toM2(elementArea(room(3.5, 3, 2.6, [true, true, true, false])).total)).toBe(36.5);
  });

  it("TC07 bathroom 2,5 × 2 × 2,4: 26,60", () => {
    expect(toM2(elementArea(room(2.5, 2, 2.4)).total)).toBe(26.6);
  });

  it("loose surfaces: TC05 7,80, TC08 72,00, TC12 800,00", () => {
    expect(toM2(elementArea({ kind: "surface", length: 3, width: 2.6 }).total)).toBe(7.8);
    expect(toM2(elementArea({ kind: "surface", length: 12, width: 6 }).total)).toBe(72);
    expect(toM2(elementArea({ kind: "surface", length: 40, width: 20 }).total)).toBe(800);
  });

  it("TC09 six-room flat: 266,44", () => {
    const flat = [
      room(5, 4),
      room(3.5, 3),
      room(4, 3.5),
      room(3.5, 3, 2.6, [true, true, true, false]),
      room(2.5, 2),
      room(6, 1.2, 2.6, all, false),
    ];
    expect(toM2(sumAreas(flat.map((e) => elementArea(e).total)))).toBe(266.44);
  });

  it("TC11 eight bedrooms L + living room L: 584,80", () => {
    const rooms = [...Array.from({ length: 8 }, () => room(4.5, 4)), room(6, 5)];
    expect(toM2(sumAreas(rooms.map((e) => elementArea(e).total)))).toBe(584.8);
  });

  it("rounds each line half-up to the cent of m², without float drift", () => {
    // 2,45 × 2,55 = 6,2475 → 6,25; 0,1 × 0,7 would drift in floating point.
    expect(toM2(elementArea({ kind: "surface", length: 2.45, width: 2.55 }).total)).toBe(6.25);
    expect(toM2(elementArea({ kind: "surface", length: 0.1, width: 0.7 }).total)).toBe(0.07);
  });

  it("a room without walls or ceiling has no area", () => {
    expect(elementArea(room(4, 3, 2.6, [false, false, false, false], false)).total).toBe(0);
  });
});
