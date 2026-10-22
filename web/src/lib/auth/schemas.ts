import { z } from "zod";

/** Same rules as the API validators (RegisterRequestValidator); messages are translation keys. */
export const PASSWORD_MIN_LENGTH = 10;
const PHONE_E164 = /^\+[1-9]\d{7,14}$/;

export const signInSchema = z.object({
  email: z.string().trim().min(1, "validation.required"),
  password: z.string().min(1, "validation.required"),
});

export const registerSchema = z.object({
  name: z.string().trim().min(1, "validation.required").max(200, "validation.tooLong"),
  email: z.email("validation.email").max(256, "validation.tooLong"),
  // Users type spaces ("+351 912 345 678"); the API expects E.164.
  phone: z
    .string()
    .transform((value) => value.replace(/[\s().-]/g, ""))
    .pipe(z.string().regex(PHONE_E164, "validation.phone")),
  password: z.string().min(PASSWORD_MIN_LENGTH, "validation.passwordLength").max(128, "validation.tooLong"),
  acceptTerms: z.boolean().refine((accepted) => accepted, "validation.acceptTerms"),
  marketingConsent: z.boolean(),
});

export type SignInValues = z.infer<typeof signInSchema>;
export type RegisterInput = z.input<typeof registerSchema>;
export type RegisterValues = z.output<typeof registerSchema>;

/** Only same-site paths are followed after sign-in (S1: back to the requested page). */
export function safeNextPath(next: string | undefined | null): string {
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\") ? next : "/";
}
