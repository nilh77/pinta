import { beforeEach, describe, expect, it } from "vitest";
import { trackSession } from "./client";

describe("session expired (docs/screens.md, global states)", () => {
  beforeEach(() => {
    trackSession("/api/auth/logout", 204);
  });

  it("a 401 after a live session opens the dialog once", () => {
    trackSession("/api/auth/me", 200);
    expect(trackSession("/api/jobs", 200)).toBe(false);
    expect(trackSession("/api/jobs", 401)).toBe(true);
    expect(trackSession("/api/auth/me", 401)).toBe(false);
  });

  it("a visitor who never signed in gets no dialog", () => {
    expect(trackSession("/api/auth/me", 401)).toBe(false);
    expect(trackSession("/api/jobs", 401)).toBe(false);
  });

  it("signing in starts a session and signing out ends it", () => {
    trackSession("/api/auth/login", 200);
    trackSession("/api/auth/logout", 204);
    expect(trackSession("/api/jobs", 401)).toBe(false);
  });
});
