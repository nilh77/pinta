import { describe, expect, it } from "vitest";
import { formatTaxId, optionalTaxId, taxIdDigits } from "./tax-id";

describe("NIF mask", () => {
  it.each([
    ["123456789", "123 456 789"],
    ["1234", "123 4"],
    [" PT 123-456 789 0", "123 456 789"],
    ["abc", ""],
  ])("%j shows as %j", (typed, shown) => expect(formatTaxId(typed)).toBe(shown));

  it("sends only the digits", () => {
    expect(taxIdDigits("123 456 789")).toBe("123456789");
    expect(optionalTaxId("bad").parse(" 123 456 789 ")).toBe("123456789");
    expect(optionalTaxId("bad").parse("")).toBe("");
    expect(optionalTaxId("bad").safeParse("123 45").error?.issues[0]?.message).toBe("bad");
  });
});
