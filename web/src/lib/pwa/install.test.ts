import { describe, expect, it } from "vitest";
import { installInvite, isIosSafari } from "./install";

const IPHONE_SAFARI = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";
const IPHONE_CHROME = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1";
const ANDROID_CHROME = "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36";
const IPAD_DESKTOP_MODE = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";

describe("install invitation (screen 8, PWA)", () => {
  it("offers the browser's button where it can install", () => {
    expect(installInvite({ canPrompt: true, userAgent: ANDROID_CHROME, standalone: false, dismissed: false })).toBe("button");
  });

  it("explains the share menu on iPhone and iPad Safari only", () => {
    expect(isIosSafari(IPHONE_SAFARI)).toBe(true);
    expect(isIosSafari(IPAD_DESKTOP_MODE)).toBe(true);
    expect(isIosSafari(IPHONE_CHROME)).toBe(false);
    expect(isIosSafari(ANDROID_CHROME)).toBe(false);
    expect(installInvite({ canPrompt: false, userAgent: IPHONE_SAFARI, standalone: false, dismissed: false })).toBe("ios");
    expect(installInvite({ canPrompt: false, userAgent: ANDROID_CHROME, standalone: false, dismissed: false })).toBeNull();
  });

  it("stays away once installed or dismissed", () => {
    expect(installInvite({ canPrompt: true, userAgent: ANDROID_CHROME, standalone: true, dismissed: false })).toBeNull();
    expect(installInvite({ canPrompt: false, userAgent: IPHONE_SAFARI, standalone: false, dismissed: true })).toBeNull();
  });
});
