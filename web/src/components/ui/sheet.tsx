"use client";

import { X } from "lucide-react";
import { Dialog } from "radix-ui";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

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
 * Escape and focus return.
 */
export function Sheet({ open, onOpenChange, title, closeLabel, actions, footer, children }: SheetProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-foreground/40 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 motion-reduce:animate-none" />
        <Dialog.Content
          aria-describedby={undefined}
          className={cn(
            "fixed z-50 flex flex-col bg-background shadow-xl outline-none",
            "inset-x-0 bottom-0 max-h-[92dvh] rounded-t-3xl data-[state=open]:animate-in data-[state=open]:slide-in-from-bottom data-[state=closed]:animate-out data-[state=closed]:slide-out-to-bottom",
            "md:inset-y-0 md:right-0 md:left-auto md:max-h-none md:w-[440px] md:rounded-none md:rounded-l-3xl md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-right",
            "duration-200 motion-reduce:animate-none",
          )}
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
          <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-4">{children}</div>
          {footer && <div className="border-t border-border bg-card px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))]">{footer}</div>}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
