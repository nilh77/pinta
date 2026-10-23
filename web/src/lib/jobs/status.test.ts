import { describe, expect, it } from "vitest";
import { ALL_STATUSES, progressIndex, STATUS_TONE } from "./status";

describe("job status (docs/states.md)", () => {
  it("has a tone for every status", () => {
    expect(Object.keys(STATUS_TONE).sort()).toEqual([...ALL_STATUSES].sort());
  });

  it("places each status on the client's progress line", () => {
    expect(progressIndex("in_review")).toBe(0);
    expect(progressIndex("site_visit")).toBe(0);
    expect(progressIndex("quoted")).toBe(1);
    expect(progressIndex("scheduled")).toBe(3);
    expect(progressIndex("closed")).toBe(5);
    expect(progressIndex("cancelled")).toBeNull();
    expect(progressIndex("expired")).toBeNull();
  });
});
