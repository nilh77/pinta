import { describe, expect, it } from "vitest";
import { formatArea, formatEuros, formatMetres } from "./format";

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

describe("formatArea and formatMetres", () => {
  it("formats hundredths of m² per language", () => {
    expect(formatArea("pt", 26644)).toBe("266,44 m²");
    expect(formatArea("es", 158480)).toBe("1.584,80 m²");
    expect(formatArea("en", 4840)).toBe("48.40 m²");
    expect(formatMetres("pt", 3.5)).toBe("3,50 m");
  });
});
