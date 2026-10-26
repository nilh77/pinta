import type { KeyboardEvent } from "react";

const NEXT = new Set(["ArrowDown", "ArrowRight"]);
const PREVIOUS = new Set(["ArrowUp", "ArrowLeft"]);

/** Moves focus to the next or previous `role` inside the group, wrapping around, and chooses it by clicking it. */
function moveWithArrows(event: KeyboardEvent<HTMLElement>, role: "radio" | "tab") {
  const step = NEXT.has(event.key) ? 1 : PREVIOUS.has(event.key) ? -1 : 0;
  if (!step) return;
  const items = [...event.currentTarget.querySelectorAll<HTMLElement>(`[role="${role}"]:not([disabled])`)];
  const current = items.indexOf(document.activeElement as HTMLElement);
  if (current < 0) return;
  event.preventDefault();
  const target = items[(current + step + items.length) % items.length];
  target.focus();
  target.click();
}

/** Keyboard of a `role="radiogroup"` of `role="radio"` buttons (WAI-ARIA radio group). Put it on the group's `onKeyDown`. */
export const radioGroupKeyDown = (event: KeyboardEvent<HTMLElement>) => moveWithArrows(event, "radio");

/** Keyboard of a `role="tablist"` (WAI-ARIA tabs, selection follows focus). Put it on the list's `onKeyDown`. */
export const tabListKeyDown = (event: KeyboardEvent<HTMLElement>) => moveWithArrows(event, "tab");

/** Roving tab stop: Tab enters the group on the chosen item, or on the first one when none is chosen yet. */
export function rovingTabIndex(selected: boolean, index: number, anySelected: boolean) {
  return selected || (!anySelected && index === 0) ? 0 : -1;
}
