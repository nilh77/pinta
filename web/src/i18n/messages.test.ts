import { describe, expect, it } from "vitest";
import en from "../../messages/en.json";
import es from "../../messages/es.json";
import pt from "../../messages/pt.json";

function keys(value: unknown, prefix = ""): string[] {
  if (typeof value !== "object" || value === null) return [prefix];
  return Object.entries(value).flatMap(([key, child]) => keys(child, prefix ? `${prefix}.${key}` : key)).sort();
}

describe("translations", () => {
  it.each([
    ["es", es],
    ["en", en],
  ])("%s has exactly the same keys as pt", (_, messages) => {
    expect(keys(messages)).toEqual(keys(pt));
  });

  it.each([
    ["pt", pt],
    ["es", es],
    ["en", en],
  ])("%s has no empty texts", (_, messages) => {
    const empty = keys(messages).filter((key) => !key.split(".").reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], messages));
    expect(empty).toEqual([]);
  });
});
