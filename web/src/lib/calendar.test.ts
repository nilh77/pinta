import { describe, expect, it } from "vitest";
import { addDays, addMonths, clamp, isIsoDate, isSelectable, monthGrid, weekday, weekdayNames } from "./calendar";

describe("calendar arithmetic", () => {
  it("adds days and months across the year, keeping short months", () => {
    expect(addDays("2026-12-31", 1)).toBe("2027-01-01");
    expect(addMonths("2026-01-31", 1)).toBe("2026-02-28");
    expect(addMonths("2026-01-15", -1)).toBe("2025-12-15");
  });

  it("starts weeks on Monday", () => {
    expect(weekday("2026-11-16")).toBe(0); // Monday
    expect(weekday("2026-11-22")).toBe(6); // Sunday
    expect(weekdayNames("es", "short")[0]).toMatch(/^lun/);
  });

  it("lays out November 2026 in six full weeks, from Monday 26 October to Sunday 6 December", () => {
    const weeks = monthGrid("2026-11-01");
    expect(weeks).toHaveLength(6);
    expect(weeks[0][0]).toBe("2026-10-26");
    expect(weeks.every((w) => w.length === 7)).toBe(true);
    expect(weeks.flat()).toContain("2026-11-30");
    expect(weeks.at(-1)!.at(-1)).toBe("2026-12-06");
  });

  it("lays out a month that starts on Monday without a leading week", () => {
    const weeks = monthGrid("2027-02-01");
    expect(weeks[0][0]).toBe("2027-02-01");
    expect(weeks).toHaveLength(4);
  });

  it("respects min and max", () => {
    expect(isSelectable("2026-11-17", "2026-11-18")).toBe(false);
    expect(isSelectable("2026-11-18", "2026-11-18", "2026-12-01")).toBe(true);
    expect(clamp("2027-01-01", undefined, "2026-12-31")).toBe("2026-12-31");
    expect(isIsoDate("2026-11-17")).toBe(true);
    expect(isIsoDate("17/11/2026")).toBe(false);
  });
});
