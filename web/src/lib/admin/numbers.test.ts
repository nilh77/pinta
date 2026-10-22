import { describe, expect, it } from "vitest";
import { centsFromInput, centsToInput, decimalPlaces, formatDecimalInput, parseDecimal } from "./numbers";

describe("parseDecimal", () => {
  it.each([
    ["8,00", 8],
    ["8.5", 8.5],
    [" 12 ", 12],
    ["1.234,56", 1234.56],
    ["1,234.56", 1234.56],
    ["15000", 15000],
    ["0,5", 0.5],
  ])("reads %s as %d", (text, expected) => {
    expect(parseDecimal(text)).toBe(expected);
  });

  it.each(["", "abc", "8,0,0", "1e3", "€8"])("rejects %j", (text) => {
    expect(parseDecimal(text)).toBeNull();
  });
});

describe("centsFromInput", () => {
  it("converts euros to cents without floating point drift", () => {
    expect(centsFromInput("8,00")).toBe(800);
    expect(centsFromInput("121,95")).toBe(12195);
    expect(centsFromInput("0.29")).toBe(29);
    expect(centsFromInput("15.000,00")).toBe(1_500_000);
  });

  it("rejects more than two decimals", () => {
    expect(decimalPlaces("8,125")).toBe(3);
    expect(centsFromInput("8,125")).toBeNull();
  });
});

describe("formatDecimalInput", () => {
  it("uses the decimal separator of each language", () => {
    expect(centsToInput("pt", 1_500_000)).toBe("15000,00");
    expect(centsToInput("es", 800)).toBe("8,00");
    expect(centsToInput("en", 800)).toBe("8.00");
    expect(formatDecimalInput("pt", 1.15)).toBe("1,15");
    expect(formatDecimalInput("en", 10, 0)).toBe("10");
  });
});
