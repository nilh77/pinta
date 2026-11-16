import { useCallback, useRef } from "react";

/**
 * Grows a header panel out of the control that opened it and drains it back on close (docs/motion.md, «Abrir un
 * control de la cabecera»). Returns a callback ref for the Radix content; the CSS lives in globals.css
 * (.morph-popover, [data-morph-cluster]).
 */

type Box = { top: number; right: number; bottom: number; left: number; width: number; height: number };
type Side = "top" | "right" | "bottom" | "left";

/** Gap between the panel and the controls it pushes aside. */
const GAP = 8;

/**
 * Where the clip starts: the trigger's rect relative to the panel when the panel covers it, otherwise the trigger
 * flattened onto the panel edge that faces it. Values are `inset()` lengths (top right bottom left) and the radius.
 */
export function seedFor(panel: Box, trigger: Box, triggerRadius: number, side: Side): { inset: [number, number, number, number]; radius: number } {
  const inside =
    trigger.left >= panel.left - 0.5 && trigger.right <= panel.right + 0.5 && trigger.top >= panel.top - 0.5 && trigger.bottom <= panel.bottom + 0.5;
  if (inside) {
    return {
      inset: [trigger.top - panel.top, panel.right - trigger.right, panel.bottom - trigger.bottom, trigger.left - panel.left],
      radius: Math.min(triggerRadius, trigger.width / 2, trigger.height / 2),
    };
  }
  const left = clamp(trigger.left - panel.left, 0, panel.width);
  const right = clamp(panel.right - trigger.right, 0, panel.width);
  const top = clamp(trigger.top - panel.top, 0, panel.height);
  const bottom = clamp(panel.bottom - trigger.bottom, 0, panel.height);
  // The panel opens on `side` of the trigger, so the seed is a zero-thickness strip on the opposite edge.
  if (side === "bottom") return { inset: [0, right, panel.height, left], radius: 0 };
  if (side === "top") return { inset: [panel.height, right, 0, left], radius: 0 };
  if (side === "right") return { inset: [top, panel.width, bottom, 0], radius: 0 };
  return { inset: [top, 0, bottom, panel.width], radius: 0 };
}

/**
 * How the controls of a cluster make room for the panel of the control at `index`, using resting positions (no
 * current shift). Controls before it slide left together until they end GAP px left of the panel; controls after it
 * slide right only if the panel covers them. Where there is no room (inside `bounds`) they yield (fade out) instead.
 */
export function planShifts(
  rest: { left: number; right: number }[],
  index: number,
  panel: { left: number; right: number },
  bounds: { left: number; right: number },
): { shift: number[]; yield: boolean[] } {
  const shift = rest.map(() => 0);
  const yieldIt = rest.map(() => false);

  if (index > 0) {
    const overlap = rest[index - 1].right - (panel.left - GAP);
    if (overlap > 0) {
      const fits = rest[0].left - overlap >= bounds.left;
      for (let i = 0; i < index; i++) {
        if (fits) shift[i] = -overlap;
        else yieldIt[i] = true;
      }
    }
  }
  if (index < rest.length - 1) {
    const overlap = panel.right + GAP - rest[index + 1].left;
    if (overlap > 0) {
      // Together, so they keep their spacing.
      const fits = rest[rest.length - 1].right + overlap <= bounds.right;
      for (let i = index + 1; i < rest.length; i++) {
        if (fits) shift[i] = overlap;
        else yieldIt[i] = true;
      }
    }
  }
  return { shift, yield: yieldIt };
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/** The horizontal shift a transform currently applies to an element. */
const currentShift = (element: Element) => {
  const transform = getComputedStyle(element).transform;
  return transform && transform !== "none" ? new DOMMatrixReadOnly(transform).m41 : 0;
};

function findTrigger(content: HTMLElement): HTMLElement | null {
  const byControls = content.id ? document.querySelector<HTMLElement>(`[aria-controls="${CSS.escape(content.id)}"]`) : null;
  if (byControls) return byControls;
  const labelledBy = content.getAttribute("aria-labelledby");
  return labelledBy ? document.getElementById(labelledBy) : null;
}

type Session = { count: number; stop: () => void };
const sessions = new WeakMap<HTMLElement, Session>();

function start(content: HTMLElement): () => void {
  const trigger = findTrigger(content);
  let measured = false;
  let restore: (() => void) | null = null;

  const measure = () => {
    if (!trigger) return;
    const wrapper = content.closest<HTMLElement>("[data-radix-popper-content-wrapper]") ?? content;
    const panel = wrapper.getBoundingClientRect();
    const triggerBox = trigger.getBoundingClientRect();
    const side = (content.dataset.side as Side | undefined) ?? "bottom";
    const seed = seedFor(panel, triggerBox, parseFloat(getComputedStyle(trigger).borderTopLeftRadius) || 0, side);
    content.style.setProperty("--morph-inset", seed.inset.map((v) => `${Math.round(v * 10) / 10}px`).join(" "));
    content.style.setProperty("--morph-seed-r", `${seed.radius}px`);
    content.style.setProperty("--morph-r", getComputedStyle(content).borderTopLeftRadius);
    restore = makeRoom(trigger, panel);
  };

  const check = () => {
    const state = content.dataset.state;
    // Radix holds the content at `animation: none` until it is positioned. Check the longhand: Chrome serialises the
    // shorthand differently, and reading it measured panels off screen in Chrome only.
    if (state === "open" && !measured && content.style.animationName !== "none") {
      measured = true;
      measure();
    } else if (state === "closed" && measured) {
      measured = false;
      restore?.();
      restore = null;
      trigger?.setAttribute("data-morph-closing", "");
    }
  };

  const observer = new MutationObserver(check);
  observer.observe(content, { attributes: true, attributeFilter: ["style", "data-state"] });
  check();

  return () => {
    observer.disconnect();
    restore?.();
    trigger?.removeAttribute("data-morph-closing");
  };
}

/** Moves the trigger's neighbours aside and returns a function that undoes only what it wrote. */
function makeRoom(trigger: HTMLElement, panel: DOMRect): (() => void) | null {
  const cluster = trigger.closest<HTMLElement>("[data-morph-cluster]");
  if (!cluster) return null;
  const items = Array.from(cluster.children) as HTMLElement[];
  const index = items.findIndex((item) => item.contains(trigger));
  if (index < 0) return null;

  // Resting positions: a control may still be sliding back from another panel, and this panel rides with it.
  const rest = items.map((item) => {
    const box = item.getBoundingClientRect();
    const shift = currentShift(item);
    return { left: box.left - shift, right: box.right - shift };
  });
  const own = currentShift(items[index]);
  const restPanel = { left: panel.left - own, right: panel.right - own };
  const header = cluster.closest<HTMLElement>(".morph-header") ?? cluster.parentElement ?? cluster;
  const row = header.getBoundingClientRect();
  const padding = parseFloat(getComputedStyle(header).paddingLeft) || 0;
  const plan = planShifts(rest, index, restPanel, { left: row.left + padding, right: row.right - padding });

  const written = new Map<HTMLElement, string>();
  const yielded: HTMLElement[] = [];
  items.forEach((item, i) => {
    if (plan.yield[i]) {
      item.setAttribute("data-morph-yield", "");
      yielded.push(item);
    } else if (plan.shift[i] !== 0) {
      item.style.transform = `translateX(${plan.shift[i]}px)`;
      // Store what the browser kept: it reserialises the value.
      written.set(item, item.style.transform);
    }
  });

  // Pieces of the header outside the cluster (the logo pill) yield when the panel covers them.
  for (const piece of Array.from(header.children) as HTMLElement[]) {
    if (piece === cluster || piece.contains(cluster)) continue;
    const box = piece.getBoundingClientRect();
    if (box.right + GAP > restPanel.left && box.left < restPanel.right) {
      piece.setAttribute("data-morph-yield", "");
      yielded.push(piece);
    }
  }

  return () => {
    written.forEach((value, item) => {
      // Leave alone anything another panel has moved since.
      if (item.style.transform === value) item.style.transform = "";
    });
    yielded.forEach((item) => item.removeAttribute("data-morph-yield"));
  };
}

function attach(node: HTMLElement) {
  const session = sessions.get(node);
  if (session) {
    session.count += 1;
    return;
  }
  sessions.set(node, { count: 1, stop: start(node) });
}

function detach(node: HTMLElement) {
  const session = sessions.get(node);
  if (!session) return;
  session.count -= 1;
  // Radix composes a new ref on every render, which detaches and reattaches this one: end the session only if it
  // is still detached after the render, so the push never restarts mid-open.
  queueMicrotask(() => {
    if (session.count > 0 || sessions.get(node) !== session) return;
    session.stop();
    sessions.delete(node);
  });
}

/** Callback ref for `DropdownMenu.Content` (or `Popover.Content`) inside a `.morph-header`. */
export function useTriggerMorph() {
  const current = useRef<HTMLElement | null>(null);
  return useCallback((node: HTMLElement | null) => {
    if (current.current && current.current !== node) detach(current.current);
    if (node && node !== current.current) attach(node);
    current.current = node;
  }, []);
}
