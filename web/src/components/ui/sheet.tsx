"use client";

import { X } from "lucide-react";
import { Dialog } from "radix-ui";
import { useRef, type PointerEvent, type ReactNode } from "react";
import { cn } from "@/lib/utils";
import { useReturnFocus } from "@/lib/use-return-focus";
import { drawerMotion, drawerOverlayMotion, overlayStyle } from "./motion";

/** Release faster than this (px/ms) or further than this fraction of the height and the sheet closes (docs/motion.md). */
const DISMISS_VELOCITY = 0.11;
const DISMISS_FRACTION = 0.25;

type SheetProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  closeLabel: string;
  /** Header actions next to the title (e.g. rename). */
  actions?: ReactNode;
  footer?: ReactNode;
  children: ReactNode;
};

/**
 * Bottom sheet below 768 px and side panel from 768 px (docs/screens.md, breakpoints). Radix Dialog gives focus trap,
 * Escape and focus return. On phones the handle and title row drag the sheet down to close it.
 */
export function Sheet({ open, onOpenChange, title, closeLabel, actions, footer, children }: SheetProps) {
  const returnFocus = useReturnFocus();
  const content = useRef<HTMLDivElement>(null);
  const drag = useRef<{ y: number; time: number } | null>(null);

  // The drag writes straight to the element's style: no React render per frame (docs/motion.md, performance).
  const place = (offset: number, follow: boolean) => {
    const element = content.current;
    if (!element) return;
    element.style.transition = follow ? "none" : "";
    element.style.translate = offset === 0 ? "" : `0 ${offset}px`;
  };

  const startDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (!window.matchMedia("(max-width: 767px)").matches) return;
    if ((event.target as Element).closest("button, a, input")) return;
    drag.current = { y: event.clientY, time: event.timeStamp };
    event.currentTarget.setPointerCapture(event.pointerId);
  };
  const moveDrag = (event: PointerEvent<HTMLDivElement>) => {
    if (!drag.current) return;
    const dy = event.clientY - drag.current.y;
    // Downwards follows the finger; upwards resists more the further it goes.
    place(dy >= 0 ? dy : -Math.sqrt(-dy) * 2, true);
  };
  const endDrag = (event: PointerEvent<HTMLDivElement>) => {
    const start = drag.current;
    drag.current = null;
    if (!start) return;
    const dy = event.clientY - start.y;
    const velocity = dy / Math.max(1, event.timeStamp - start.time);
    const height = content.current?.offsetHeight ?? Infinity;
    if (event.type === "pointerup" && dy > 0 && (velocity > DISMISS_VELOCITY || dy > height * DISMISS_FRACTION)) {
      // Keep the offset: the exit animation continues from where the finger left the sheet.
      content.current?.style.removeProperty("transition");
      onOpenChange(false);
      return;
    }
    place(0, false);
  };

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className={cn("fixed inset-0 z-40 bg-foreground/40", drawerOverlayMotion)} style={overlayStyle(open)} />
        <Dialog.Content
          {...returnFocus}
          ref={content}
          aria-describedby={undefined}
          className={cn(
            "fixed z-50 flex flex-col bg-background shadow-xl outline-none",
            "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-3xl",
            "md:inset-y-0 md:right-0 md:left-auto md:max-h-none md:w-[440px] md:rounded-none md:rounded-l-3xl",
            drawerMotion,
            "transition-[translate] motion-reduce:transition-none",
          )}
        >
          <div
            onPointerDown={startDrag}
            onPointerMove={moveDrag}
            onPointerUp={endDrag}
            onPointerCancel={endDrag}
            className="max-md:cursor-grab max-md:touch-none"
          >
            <div aria-hidden="true" className="mx-auto mt-2 h-1.5 w-10 rounded-full bg-border md:hidden" />
            <div className="flex items-center gap-2 px-4 pt-2 pb-1 md:pt-5">
              <Dialog.Title className="min-w-0 flex-1 truncate font-display text-xl font-bold">{title}</Dialog.Title>
              {actions}
              <Dialog.Close
                aria-label={closeLabel}
                className="flex size-11 items-center justify-center rounded-xl text-muted-foreground hover:bg-primary-soft hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
              >
                <X aria-hidden="true" className="size-5" />
              </Dialog.Close>
            </div>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-4">{children}</div>
          {footer && <div className="border-t border-border bg-card px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))]">{footer}</div>}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
