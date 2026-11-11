import { toast as sonner, type ExternalToast } from "sonner";

/**
 * Sonner with the durations of docs/screens.md (global states): success 3 s (set on the Toaster), undo 5 s (per call),
 * errors stay until the user closes them. Import `toast` from here, never from "sonner".
 */
export const toast: typeof sonner = Object.assign((...args: Parameters<typeof sonner>) => sonner(...args), sonner, {
  error: ((message, data?: ExternalToast) => sonner.error(message, { duration: Infinity, closeButton: true, ...data })) as typeof sonner.error,
});
