"use client";

import { DropdownMenu } from "radix-ui";
import type { ComponentProps } from "react";
import { useTriggerMorph } from "@/lib/use-trigger-morph";
import { cn } from "@/lib/utils";

/**
 * Panel of a header control (docs/motion.md, «Abrir un control de la cabecera»): opens over its 40 px round trigger,
 * aligned to its top-right corner, with the trigger's 20 px radius, and grows out of it. Keep the first row clear of
 * the top-right corner (about 40 px), where the trigger stays visible. Use it inside a `.morph-header`, with the menu
 * root non-modal so one panel can hand over to the next.
 */
export function MorphMenuContent({ className, children, ...props }: ComponentProps<typeof DropdownMenu.Content>) {
  const morph = useTriggerMorph();
  return (
    <DropdownMenu.Portal>
      <DropdownMenu.Content
        ref={morph}
        align="end"
        sideOffset={-40}
        collisionPadding={16}
        className={cn("morph-popover z-50 min-w-60 rounded-[20px] glass glass-overlay bg-glass-fill p-1.5 shadow-chrome outline-none", className)}
        {...props}
      >
        {children}
      </DropdownMenu.Content>
    </DropdownMenu.Portal>
  );
}

/** A row of a header panel. */
export const morphMenuItemClass =
  "flex min-h-11 cursor-pointer items-center gap-2.5 rounded-xl px-3 text-sm font-medium outline-none data-[highlighted]:bg-primary-soft";
