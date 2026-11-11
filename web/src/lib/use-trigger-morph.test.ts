import { describe, expect, it } from "vitest";
import { planShifts, seedFor } from "./use-trigger-morph";

const box = (left: number, top: number, width: number, height: number) => ({ left, top, width, height, right: left + width, bottom: top + height });

describe("seedFor", () => {
  it("starts from the trigger's own rect and radius when the panel covers it", () => {
    // Panel opened over its 40 px round control, aligned to the control's top-right corner.
    const panel = box(100, 16, 240, 200);
    const trigger = box(300, 16, 40, 40);
    expect(seedFor(panel, trigger, 20, "bottom")).toEqual({ inset: [0, 0, 160, 200], radius: 20 });
  });

  it("caps the seed radius at half the trigger's size", () => {
    const panel = box(0, 0, 200, 200);
    const trigger = box(160, 0, 40, 24);
    expect(seedFor(panel, trigger, 999, "bottom").radius).toBe(12);
  });

  it("flattens the trigger onto the panel edge that faces it when the panel is outside it", () => {
    // Panel below the trigger: a zero-height strip along the panel's top edge, under the trigger.
    const panel = box(100, 60, 240, 200);
    const trigger = box(300, 16, 40, 40);
    expect(seedFor(panel, trigger, 20, "bottom")).toEqual({ inset: [0, 0, 200, 200], radius: 0 });
  });
});

describe("planShifts", () => {
  // Three 40 px controls 8 px apart at x = 200, 248, 296.
  const rest = [
    { left: 200, right: 240 },
    { left: 248, right: 288 },
    { left: 296, right: 336 },
  ];

  it("slides the controls before the open one left together, ending 8 px from the panel", () => {
    const panel = { left: 120, right: 336 }; // panel of the last control
    const plan = planShifts(rest, 2, panel, { left: 16, right: 352 });
    expect(plan.shift).toEqual([-176, -176, 0]);
    expect(plan.yield).toEqual([false, false, false]);
  });

  it("does not move anything the panel does not cover", () => {
    const panel = { left: 300, right: 336 };
    expect(planShifts(rest, 2, panel, { left: 16, right: 352 }).shift).toEqual([0, 0, 0]);
  });

  it("fades the covered controls out when there is no room to slide them", () => {
    const panel = { left: 20, right: 336 }; // nearly as wide as a phone
    const plan = planShifts(rest, 2, panel, { left: 16, right: 352 });
    expect(plan.shift).toEqual([0, 0, 0]);
    expect(plan.yield).toEqual([true, true, false]);
  });

  it("slides later controls right only when the panel covers them and there is room", () => {
    const panel = { left: 200, right: 260 }; // panel of the first control, wider than it
    expect(planShifts(rest, 0, panel, { left: 16, right: 400 }).shift).toEqual([0, 20, 20]);
    expect(planShifts(rest, 0, panel, { left: 16, right: 340 }).yield).toEqual([false, true, true]);
  });
});
