// @vitest-environment node
import { NextRequest } from "next/server";
import { describe, expect, it } from "vitest";
import proxy, { LOCALE_COOKIE } from "./proxy";

const request = (path: string, cookie?: string) =>
  new NextRequest(new URL(path, "http://localhost:3000"), { headers: cookie ? { cookie: `${LOCALE_COOKIE}=${cookie}` } : {} });

describe("remembered language (C1.1)", () => {
  it("sends a first visit to Portuguese, whatever the browser says", () => {
    const response = proxy(new NextRequest("http://localhost:3000/", { headers: { "accept-language": "en-GB" } }));
    expect(response.headers.get("location")).toBe("http://localhost:3000/pt");
  });

  it("goes back to the saved language", () => {
    expect(proxy(request("/", "es")).headers.get("location")).toBe("http://localhost:3000/es");
  });

  it("remembers the language of the page the visitor opens", () => {
    const response = proxy(request("/en/cotizar", "pt"));
    expect(response.cookies.get(LOCALE_COOKIE)?.value).toBe("en");
  });

  it("ignores an unknown saved value", () => {
    expect(proxy(request("/", "fr")).headers.get("location")).toBe("http://localhost:3000/pt");
  });
});
