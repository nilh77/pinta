import { describe, expect, it } from "vitest";
import { keyBytes, pushSupport } from "./push";

const IPHONE = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 Version/18.1 Mobile/15E148 Safari/604.1";
const ANDROID = "Mozilla/5.0 (Linux; Android 15) AppleWebKit/537.36 Chrome/130.0 Mobile Safari/537.36";

describe("push support (docs/integrations.md)", () => {
  it("asks iPhone users to install the app first", () => {
    expect(pushSupport({ userAgent: IPHONE, standalone: false, hasPush: false })).toBe("ios-install");
    expect(pushSupport({ userAgent: IPHONE, standalone: true, hasPush: true })).toBe("ok");
  });

  it("works where the browser has push, and says so where it has not", () => {
    expect(pushSupport({ userAgent: ANDROID, standalone: false, hasPush: true })).toBe("ok");
    expect(pushSupport({ userAgent: ANDROID, standalone: false, hasPush: false })).toBe("unsupported");
  });

  it("decodes the base64url VAPID key", () => {
    expect([...keyBytes("AQID_-8")]).toEqual([1, 2, 3, 255, 239]);
  });
});
