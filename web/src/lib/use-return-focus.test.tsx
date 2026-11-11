import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";

function Screen() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Continue
      </button>
      <ConfirmDialog open={open} onOpenChange={setOpen} title="Start again?" confirmLabel="Yes" cancelLabel="Keep" onConfirm={() => {}} />
    </>
  );
}

afterEach(cleanup);

describe("useReturnFocus", () => {
  it("gives focus back to the button that opened a controlled dialog", async () => {
    render(<Screen />);
    const opener = screen.getByRole("button", { name: "Continue" });
    opener.focus();
    fireEvent.click(opener);
    await waitFor(() => expect(screen.getByRole("alertdialog")).toBeTruthy());
    expect(document.activeElement).not.toBe(opener);

    fireEvent.click(screen.getByRole("button", { name: "Keep" }));
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(document.activeElement).toBe(opener);
  });

  it("also returns focus when the dialog is closed with Escape", async () => {
    render(<Screen />);
    const opener = screen.getByRole("button", { name: "Continue" });
    opener.focus();
    fireEvent.click(opener);
    const dialog = await screen.findByRole("alertdialog");
    fireEvent.keyDown(dialog, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(document.activeElement).toBe(opener);
  });
});
