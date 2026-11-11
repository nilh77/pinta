import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Button } from "./button";

afterEach(cleanup);

describe("Button pending", () => {
  it("keeps its label for screen readers, shows a spinner and blocks clicks", () => {
    render(<Button pending>Save</Button>);
    const button = screen.getByRole("button", { name: "Save" });
    expect(button.getAttribute("aria-busy")).toBe("true");
    expect((button as HTMLButtonElement).disabled).toBe(true);
    expect(button.querySelector("svg.animate-spin")).toBeTruthy();
    // The label keeps its place (same size) but is not visible; the spinner is not a direct child, so padding rules
    // such as has-[>svg] see the same children as before.
    expect(button.className).toContain("text-transparent");
    expect(button.querySelector(":scope > svg")).toBeNull();
  });

  it("still works as a link with asChild", () => {
    render(
      <Button asChild>
        <a href="https://example.com">Go</a>
      </Button>,
    );
    expect(screen.getByRole("link", { name: "Go" }).getAttribute("data-slot")).toBe("button");
  });

  it("renders plainly when not pending", () => {
    render(<Button>Save</Button>);
    const button = screen.getByRole("button", { name: "Save" });
    expect(button.getAttribute("aria-busy")).toBeNull();
    expect((button as HTMLButtonElement).disabled).toBe(false);
    expect(button.querySelector("svg")).toBeNull();
  });
});
