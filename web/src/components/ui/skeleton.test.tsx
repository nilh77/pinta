import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { CardListSkeleton, DetailSkeleton, Skeleton } from "./skeleton";

afterEach(cleanup);

describe("Skeleton", () => {
  it("is hidden from assistive technology and stops pulsing with reduced motion", () => {
    const { container } = render(<Skeleton className="h-8" />);
    const block = container.querySelector('[data-slot="skeleton"]');
    expect(block?.getAttribute("aria-hidden")).toBe("true");
    expect(block?.className).toContain("motion-reduce:animate-none");
    expect(block?.className).toContain("h-8");
  });
});

describe("CardListSkeleton", () => {
  it("announces the label once and draws one block per card", () => {
    const { container } = render(<CardListSkeleton label="Loading" count={2} />);
    expect(screen.getByRole("status").textContent).toBe("Loading");
    expect(container.querySelectorAll('[data-slot="skeleton"]')).toHaveLength(2);
  });
});

describe("DetailSkeleton", () => {
  it("announces the label once and draws the page silhouette", () => {
    const { container } = render(<DetailSkeleton label="Loading the job" />);
    expect(screen.getAllByRole("status")).toHaveLength(1);
    expect(screen.getByRole("status").textContent).toBe("Loading the job");
    expect(container.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThanOrEqual(4);
  });
});
