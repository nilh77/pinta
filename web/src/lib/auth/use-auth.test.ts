import { describe, expect, it } from "vitest";
import { homeFor } from "./use-auth";

describe("homeFor (the logo's destination)", () => {
  it("takes each role to its start page and visitors to the landing", () => {
    expect(homeFor({ role: "client" })).toBe("/mis-trabajos");
    expect(homeFor({ role: "painter" })).toBe("/pintor");
    expect(homeFor({ role: "admin" })).toBe("/admin/revision");
    expect(homeFor(null)).toBe("/");
    expect(homeFor(undefined)).toBe("/");
  });
});
