import { useRef } from "react";

/**
 * Focus return for controlled Radix dialogs. Ours open from state, not from a `Dialog.Trigger`, so on close Radix tries
 * to focus a trigger that does not exist and focus falls to <body>: a keyboard or screen reader user loses their place.
 * Spread the result on `Dialog.Content` / `AlertDialog.Content`: it remembers what had focus when the dialog opened and
 * gives focus back to it when the dialog closes, if that element is still on the page. When the dialog was opened from
 * a menu item, focus goes back to the button that opened the menu, since the item itself is gone by then.
 */
export function useReturnFocus() {
  const previous = useRef<HTMLElement | null>(null);
  return {
    onOpenAutoFocus: () => {
      // Runs before Radix moves focus into the dialog, so this is still the element the user acted on.
      previous.current = document.activeElement instanceof HTMLElement ? menuTrigger(document.activeElement) : null;
    },
    onCloseAutoFocus: (event: Event) => {
      const element = previous.current;
      previous.current = null;
      if (!element?.isConnected) return;
      event.preventDefault();
      element.focus();
    },
  };
}

/** The trigger of the menu that contains `element` (Radix labels the menu with its trigger's id), or the element itself. */
function menuTrigger(element: HTMLElement): HTMLElement {
  const triggerId = element.closest('[role="menu"]')?.getAttribute("aria-labelledby");
  const trigger = triggerId ? document.getElementById(triggerId) : null;
  return trigger ?? element;
}
