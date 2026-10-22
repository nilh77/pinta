import { describe, expect, it } from "vitest";
import { classifyFile, MAX_VIDEO_BYTES } from "./uploads";

describe("classifyFile (C3.1)", () => {
  it("accepts the photo and video formats of screen 5", () => {
    expect(classifyFile({ type: "image/jpeg", name: "a.jpg", size: 30_000_000 })).toEqual({ kind: "image", contentType: "image/jpeg" });
    expect(classifyFile({ type: "", name: "IMG_1.HEIC", size: 2_000_000 })).toEqual({ kind: "image", contentType: "image/heic" });
    expect(classifyFile({ type: "", name: "clip.mov", size: 80 * 1024 * 1024 })).toEqual({ kind: "video", contentType: "video/quicktime" });
  });

  it("rejects other formats and files that are too large before uploading", () => {
    expect(classifyFile({ type: "image/gif", name: "a.gif", size: 10 })).toEqual({ error: "quote.photos.errors.format" });
    expect(classifyFile({ type: "video/mp4", name: "v.mp4", size: MAX_VIDEO_BYTES + 1 })).toEqual({ error: "quote.photos.errors.videoTooLarge" });
    expect(classifyFile({ type: "image/heic", name: "a.heic", size: 11 * 1024 * 1024 })).toEqual({ error: "quote.photos.errors.imageTooLarge" });
  });
});
