import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import messages from "../../../messages/es.json";
import { DateInput } from "./date-input";

afterEach(cleanup);

function setup(props: Partial<Parameters<typeof DateInput>[0]> = {}) {
  const onChange = vi.fn();
  render(
    <NextIntlClientProvider locale="es" messages={messages}>
      <DateInput aria-label="Desde" value="2026-11-20" min="2026-11-18" max="2026-12-31" onChange={onChange} {...props} />
    </NextIntlClientProvider>,
  );
  return onChange;
}

describe("DateInput (docs/screens.md, «Formularios»)", () => {
  it("shows the chosen date written in the language", () => {
    setup();
    expect(screen.getByRole("button", { name: /Desde, viernes, 20 de noviembre de 2026/ }).textContent).toMatch(/20 nov 2026/);
  });

  it("opens on the chosen day, moves with the keyboard and picks with Enter", async () => {
    const onChange = setup();
    await act(async () => fireEvent.click(screen.getByRole("button", { name: /Desde/ })));
    expect(screen.getByRole("grid", { name: /noviembre de 2026/i })).toBeTruthy();
    const day20 = screen.getByRole("button", { name: "viernes, 20 de noviembre de 2026" });
    expect(document.activeElement).toBe(day20);

    await act(async () => fireEvent.keyDown(day20, { key: "ArrowDown" }));
    const day27 = screen.getByRole("button", { name: "viernes, 27 de noviembre de 2026" });
    expect(document.activeElement).toBe(day27);
    await act(async () => fireEvent.click(day27));
    expect(onChange).toHaveBeenCalledWith("2026-11-27");
  });

  it("does not let days outside min and max be picked", async () => {
    const onChange = setup();
    await act(async () => fireEvent.click(screen.getByRole("button", { name: /Desde/ })));
    const before = screen.getByRole("button", { name: "martes, 17 de noviembre de 2026" }) as HTMLButtonElement;
    expect(before.disabled).toBe(true);
    // Going back past the minimum stops at it.
    await act(async () => fireEvent.keyDown(document.activeElement!, { key: "PageUp" }));
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "miércoles, 18 de noviembre de 2026" }));
    expect(onChange).not.toHaveBeenCalled();
  });
});
