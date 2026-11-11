/**
 * Shared motion classes for Radix layers (docs/motion.md). Layers enter with a settling spring from where they
 * come from and leave faster with a strong ease-out. `motion-fade-only` keeps only the fade with reduced motion.
 */

/** Dim backdrop behind dialogs: fades with the dialog. */
export const overlayMotion =
  "ease-out data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-layer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:duration-(--duration-layer-out)";

/** Centred dialogs: rise and settle from the centre (correct for modals), shrink slightly on the way out. */
export const dialogMotion =
  "motion-fade-only data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-90 data-[state=open]:slide-in-from-bottom-4 data-[state=open]:ease-spring data-[state=open]:duration-(--duration-layer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:ease-out data-[state=closed]:duration-(--duration-layer-out)";

/** Dropdown menus: grow out of the trigger, nudged from its side, never from the centre. */
export const menuMotion =
  "motion-fade-only origin-(--radix-dropdown-menu-content-transform-origin) data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-90 data-[state=open]:ease-spring data-[state=open]:duration-(--duration-menu-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:ease-out data-[state=closed]:duration-(--duration-menu-out)";

/** Backdrop behind a sheet: same curve and timing as the sheet. */
export const drawerOverlayMotion =
  "ease-drawer data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-drawer-in) data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:duration-(--duration-drawer-out)";

/** Sheet: from the bottom below 768 px, from the right from 768 px. Each axis only at its breakpoint. */
export const drawerMotion =
  "motion-fade-only ease-drawer data-[state=open]:animate-in data-[state=open]:duration-(--duration-drawer-in) data-[state=closed]:animate-out data-[state=closed]:duration-(--duration-drawer-out) max-md:data-[state=open]:slide-in-from-bottom max-md:data-[state=closed]:slide-out-to-bottom md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-right";
