import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { radioGroupKeyDown, rovingTabIndex, tabListKeyDown } from "./arrow-keys";

function Sizes() {
  const [size, setSize] = useState<string | null>(null);
  const sizes = ["S", "M", "L"];
  return (
    <div role="radiogroup" aria-label="Size" onKeyDown={radioGroupKeyDown}>
      {sizes.map((s, i) => (
        <button key={s} type="button" role="radio" aria-checked={size === s} tabIndex={rovingTabIndex(size === s, i, size !== null)} onClick={() => setSize(s)}>
          {s}
        </button>
      ))}
    </div>
  );
}

describe("radio groups and tabs by keyboard (accessibility audit, H8)", () => {
  it("enters on the first option, then arrows move and choose, wrapping around", () => {
    render(<Sizes />);
    const [s, m, l] = screen.getAllByRole("radio");
    expect([s.tabIndex, m.tabIndex, l.tabIndex]).toEqual([0, -1, -1]);

    s.focus();
    fireEvent.keyDown(s, { key: "ArrowDown" });
    expect(document.activeElement).toBe(m);
    expect(m.getAttribute("aria-checked")).toBe("true");
    expect([s.tabIndex, m.tabIndex, l.tabIndex]).toEqual([-1, 0, -1]);

    fireEvent.keyDown(m, { key: "ArrowRight" });
    fireEvent.keyDown(l, { key: "ArrowDown" });
    expect(document.activeElement).toBe(s);
    expect(s.getAttribute("aria-checked")).toBe("true");

    fireEvent.keyDown(s, { key: "ArrowUp" });
    expect(document.activeElement).toBe(l);
  });

  it("leaves other keys alone", () => {
    render(<Sizes />);
    const [s] = screen.getAllByRole("radio");
    s.focus();
    fireEvent.keyDown(s, { key: "Tab" });
    expect(s.getAttribute("aria-checked")).toBe("false");
  });

  it("tab lists move the selection with the arrows too", () => {
    function Tabs() {
      const [tab, setTab] = useState("a");
      return (
        <div role="tablist" aria-label="Tabs" onKeyDown={tabListKeyDown}>
          {["a", "b"].map((key, i) => (
            <button key={key} type="button" role="tab" aria-selected={tab === key} tabIndex={rovingTabIndex(tab === key, i, true)} onClick={() => setTab(key)}>
              {key}
            </button>
          ))}
        </div>
      );
    }
    render(<Tabs />);
    const [a, b] = screen.getAllByRole("tab");
    a.focus();
    fireEvent.keyDown(a, { key: "ArrowRight" });
    expect(document.activeElement).toBe(b);
    expect([a.tabIndex, b.tabIndex]).toEqual([-1, 0]);
  });
});
