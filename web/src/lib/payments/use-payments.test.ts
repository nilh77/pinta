import { describe, expect, it } from "vitest";
import { formatCountdown, isPaymentType, MB_WAY_WINDOW_SECONDS, receiptUrl } from "./use-payments";

describe("payment screen helpers (C5.3)", () => {
  it("only accepts the three payment types from the URL", () => {
    expect(isPaymentType("deposit")).toBe(true);
    expect(isPaymentType("balance")).toBe(true);
    expect(isPaymentType("site_visit")).toBe(true);
    expect(isPaymentType("tip")).toBe(false);
    expect(isPaymentType(null)).toBe(false);
  });

  it("counts down MB WAY's five minutes as m:ss", () => {
    expect(formatCountdown(MB_WAY_WINDOW_SECONDS)).toBe("5:00");
    expect(formatCountdown(65)).toBe("1:05");
    expect(formatCountdown(-3)).toBe("0:00");
  });

  it("links the receipt of a payment", () => {
    expect(receiptUrl("j1", "p1")).toBe("/api/jobs/j1/payments/p1/receipt");
  });
});
