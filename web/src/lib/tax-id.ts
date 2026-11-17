import type { ChangeEvent } from "react";
import type { UseFormRegisterReturn } from "react-hook-form";
import { z } from "zod";

/** The NIF's 9 digits, whatever was typed or pasted around them ("PT 123 456-789" → "123456789"). */
export const taxIdDigits = (text: string) => text.replace(/\D/g, "").slice(0, 9);

/** The NIF as the field shows it while typing: groups of three, "123 456 789" (docs/screens.md, «Formularios»). */
export const formatTaxId = (text: string) => taxIdDigits(text).replace(/(\d{3})(?=\d)/g, "$1 ");

/** An optional NIF field: masked on screen, sent as its 9 digits, empty allowed. */
export const optionalTaxId = (message: string) =>
  z
    .string()
    .transform(taxIdDigits)
    .refine((v) => v === "" || v.length === 9, message);

/** Wraps a react-hook-form registration so the field formats itself as the person types. */
export function maskTaxId<T extends UseFormRegisterReturn>(registration: T): T {
  return {
    ...registration,
    onChange: (event: ChangeEvent<HTMLInputElement>) => {
      event.target.value = formatTaxId(event.target.value);
      return registration.onChange(event);
    },
  };
}
