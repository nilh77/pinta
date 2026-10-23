import { describe, expect, it } from "vitest";
import { forgotPasswordSchema, registerSchema, resetPasswordSchema, safeNextPath, signInSchema } from "./schemas";

const valid = {
  name: "Ana Costa",
  email: "ana@example.pt",
  phone: "+351 912 345 678",
  password: "very-long-password",
  acceptTerms: true,
  marketingConsent: false,
};

function errorsOf(input: unknown) {
  const result = registerSchema.safeParse(input);
  return result.success ? {} : Object.fromEntries(result.error.issues.map((i) => [i.path.join("."), i.message]));
}

describe("C4.3 register schema", () => {
  it("accepts valid data and normalizes the phone to E.164", () => {
    expect(registerSchema.parse(valid).phone).toBe("+351912345678");
  });

  it("rejects what the API rejects, with translation keys", () => {
    expect(errorsOf({ ...valid, name: " ", email: "nope", phone: "912345678", password: "short", acceptTerms: false })).toEqual({
      name: "validation.required",
      email: "validation.email",
      phone: "validation.phone",
      password: "validation.passwordLength",
      acceptTerms: "validation.acceptTerms",
    });
  });
});

describe("S1 sign-in", () => {
  it("requires email and password", () => {
    expect(signInSchema.safeParse({ email: "", password: "" }).success).toBe(false);
  });

  it.each([
    [undefined, "/"],
    ["/cuenta", "/cuenta"],
    ["//evil.example", "/"],
    ["/\\evil.example", "/"],
    ["https://evil.example", "/"],
  ])("follows only same-site next paths (%s)", (next, expected) => {
    expect(safeNextPath(next)).toBe(expected);
  });
});

describe("password recovery schemas (S1)", () => {
  it("asks for a valid email", () => {
    expect(forgotPasswordSchema.safeParse({ email: "ana@pinta.pt" }).success).toBe(true);
    expect(forgotPasswordSchema.safeParse({ email: "ana" }).error?.issues[0]?.message).toBe("validation.email");
  });

  it("asks for a new password of at least 10 characters", () => {
    expect(resetPasswordSchema.safeParse({ password: "0123456789" }).success).toBe(true);
    expect(resetPasswordSchema.safeParse({ password: "short" }).error?.issues[0]?.message).toBe("validation.passwordLength");
  });
});
