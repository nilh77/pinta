import { describe, expect, it } from "vitest";
import { formatArea, formatEuros, formatMetres, daysUntil, formatCents, formatDate, formatPlace } from "./format";

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

describe("job formats", () => {
  it("formats cents, dates and days left", () => {
    expect(plain(formatCents("pt", 318468))).toBe("3.184,68 €");
    expect(plain(formatCents("en", 95540))).toBe("€955.40");
    expect(formatDate("pt", "2026-11-06")).toBe("6 de novembro de 2026");
    expect(formatDate("en", "2026-11-06", true)).toBe("6 November");
    expect(daysUntil("2026-11-06", new Date("2026-10-23T10:00:00Z"))).toBe(14);
    expect(daysUntil("2026-10-22", new Date("2026-10-23T10:00:00Z"))).toBe(-1);
  });
});

describe("formatPlace", () => {
  it("names the municipality only when it differs from the locality", () => {
    expect(formatPlace("Sintra", "Sintra")).toBe("Sintra");
    expect(formatPlace("Carcavelos", "Cascais")).toBe("Carcavelos, Cascais");
    expect(formatPlace("Algés", "Oeiras", " · ")).toBe("Algés · Oeiras");
    expect(formatPlace("Lisboa", null)).toBe("Lisboa");
  });
});
