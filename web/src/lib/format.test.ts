import { describe, expect, it } from "vitest";
import { formatEuros } from "./format";

// Intl uses narrow no-break spaces; compare with plain spaces.
const plain = (text: string) => text.replace(/[  ]/g, " ");

describe("formatEuros", () => {
  it.each([
    ["pt", "1.270,00 €"],
    ["es", "1.270,00 €"],
    ["en", "€1,270.00"],
  ])("formats euros for %s", (locale, expected) => {
    expect(plain(formatEuros(locale, 1270))).toBe(expected);
  });

  it("can drop the cents for ranges", () => {
    expect(plain(formatEuros("pt", 1620, 0))).toBe("1.620 €");
  });
});
