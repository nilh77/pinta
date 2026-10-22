import { describe, expect, it } from "vitest";
import pt from "../../../messages/pt.json";
import { ApiError, errorCode, KNOWN_ERROR_CODES } from "./problem";

describe("ProblemDetails codes", () => {
  it("maps known codes and falls back to internal_error", () => {
    expect(errorCode({ code: "email_taken" })).toBe("email_taken");
    expect(errorCode({ code: "something_new" })).toBe("internal_error");
    expect(errorCode(undefined)).toBe("internal_error");
    expect(new ApiError({ code: "invalid_credentials" } as never).code).toBe("invalid_credentials");
  });

  it("has a message for every known code", () => {
    expect(Object.keys(pt.errors).sort()).toEqual([...KNOWN_ERROR_CODES].sort());
  });
});
