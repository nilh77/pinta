import { describe, expect, it } from "vitest";
import { shouldInvite } from "./push-invite";

const now = Date.parse("2026-11-17T12:00:00Z");
const base = { signedIn: true, touch: true, status: "off" as const, dismissedAt: null, now };

describe("push invitation (docs/screens.md, «Avisos», «Invitación»)", () => {
  it("invites a signed-in phone where push can be turned on", () => {
    expect(shouldInvite(base)).toBe(true);
  });

  it("stays away when push is on, blocked, unsupported or needs the installed app", () => {
    for (const status of ["on", "blocked", "unsupported", "ios-install", "unavailable", "loading"] as const) {
      expect(shouldInvite({ ...base, status })).toBe(false);
    }
  });

  it("stays away on desktops, for visitors and for 14 days after «Ahora no»", () => {
    expect(shouldInvite({ ...base, touch: false })).toBe(false);
    expect(shouldInvite({ ...base, signedIn: false })).toBe(false);
    expect(shouldInvite({ ...base, dismissedAt: now - 3 * 24 * 3600_000 })).toBe(false);
    expect(shouldInvite({ ...base, dismissedAt: now - 15 * 24 * 3600_000 })).toBe(true);
  });
});
