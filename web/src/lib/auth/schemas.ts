import { z } from "zod";

/** Same rules as the API validators (RegisterRequestValidator); messages are translation keys. */
export const PASSWORD_MIN_LENGTH = 10;
const PHONE_E164 = /^\+[1-9]\d{7,14}$/;

export const signInSchema = z.object({
  email: z.string().trim().min(1, "validation.required"),
  // A pasted password keeps its spaces: they may be part of it.
  password: z.string().min(1, "validation.required"),
});

/** An email as people paste it: surrounding spaces dropped before checking (docs/screens.md, «Formularios»). */
export const emailField = () => z.string().trim().max(256, "validation.tooLong").pipe(z.email("validation.email"));

const newPassword = () => z.string().min(PASSWORD_MIN_LENGTH, "validation.passwordLength").max(128, "validation.tooLong");

/** «Repetir contraseña» must match; checked in the browser only, the API never receives it. */
const passwordsMatch = {
  check: (values: { password: string; confirmPassword: string }) => values.password === values.confirmPassword,
  issue: { message: "validation.passwordMismatch", path: ["confirmPassword"] },
};

export const registerSchema = z
  .object({
    name: z.string().trim().min(1, "validation.required").max(200, "validation.tooLong"),
    email: emailField(),
    // Users type spaces ("+351 912 345 678"); the API expects E.164.
    phone: z
      .string()
      .transform((value) => value.replace(/[\s().-]/g, ""))
      .pipe(z.string().regex(PHONE_E164, "validation.phone")),
    password: newPassword(),
    confirmPassword: z.string(),
    acceptTerms: z.boolean().refine((accepted) => accepted, "validation.acceptTerms"),
    marketingConsent: z.boolean(),
  })
  .refine(passwordsMatch.check, passwordsMatch.issue)
  .transform((values) => {
    const { confirmPassword, ...sent } = values;
    void confirmPassword;
    return sent;
  });

export const forgotPasswordSchema = z.object({ email: emailField() });

export const resetPasswordSchema = z
  .object({ password: newPassword(), confirmPassword: z.string() })
  .refine(passwordsMatch.check, passwordsMatch.issue);

/** Same rule for the account's «Cambiar contraseña», which keeps its fields in plain state. */
export const passwordsDiffer = (password: string, confirmPassword: string) => !passwordsMatch.check({ password, confirmPassword });

export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
export type SignInValues = z.infer<typeof signInSchema>;
export type RegisterInput = z.input<typeof registerSchema>;
export type RegisterValues = z.output<typeof registerSchema>;

/** Only same-site paths are followed after sign-in (S1: back to the requested page). */
export function safeNextPath(next: string | undefined | null): string {
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\") ? next : "/";
}
