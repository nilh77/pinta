import { describe, expect, it } from "vitest";
import { recentMonths } from "./use-finance";

describe("recentMonths (A5.1)", () => {
  it("lists the latest month first and crosses the year", () => {
    expect(recentMonths("2026-02", 4)).toEqual(["2026-02", "2026-01", "2025-12", "2025-11"]);
  });

  it("offers two years by default", () => {
    const months = recentMonths("2026-11");
    expect(months).toHaveLength(24);
    expect(months.at(-1)).toBe("2024-12");
  });
});
