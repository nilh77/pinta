import { describe, expect, it, vi } from "vitest";

vi.mock("sonner", () => {
  const base = Object.assign(vi.fn(), { error: vi.fn(), success: vi.fn() });
  return { toast: base };
});

import { toast as sonner } from "sonner";
import { toast } from "./toast";

describe("toast", () => {
  it("closes errors by themselves after 8 s, with a close button", () => {
    toast.error("boom");
    expect(sonner.error).toHaveBeenCalledWith("boom", { duration: 8000, closeButton: true });
  });

  it("lets a call override the error options", () => {
    toast.error("boom", { duration: Infinity });
    expect(sonner.error).toHaveBeenLastCalledWith("boom", { duration: Infinity, closeButton: true });
  });

  it("passes success through untouched", () => {
    toast.success("ok");
    expect(sonner.success).toHaveBeenCalledWith("ok");
  });

  it("passes plain toasts through untouched", () => {
    toast("undo me", { duration: 5000 });
    expect(sonner).toHaveBeenCalledWith("undo me", { duration: 5000 });
  });
});
