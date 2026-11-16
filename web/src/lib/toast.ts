import { toast as sonner, type ExternalToast } from "sonner";

/**
 * Sonner with the durations of docs/screens.md (global states): success 3 s (set on the Toaster), undo 5 s (per call),
 * errors 8 s with a close button, longer than success so they can be read. Import `toast` from here, never from "sonner".
 */
export const ERROR_TOAST_MS = 8000;

export const toast: typeof sonner = Object.assign((...args: Parameters<typeof sonner>) => sonner(...args), sonner, {
  error: ((message, data?: ExternalToast) => sonner.error(message, { duration: ERROR_TOAST_MS, closeButton: true, ...data })) as typeof sonner.error,
});
