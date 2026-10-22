"use client";

import { useTranslations } from "next-intl";
import type { ComponentProps, ReactNode } from "react";
import type { FieldError } from "react-hook-form";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type Props = ComponentProps<typeof Input> & {
  id: string;
  label: string;
  hint?: ReactNode;
  error?: FieldError;
  /** Values for the translated error message, e.g. { min: 10 }. */
  errorValues?: Record<string, string | number>;
};

/** Label, input, hint and error wired together for screen readers. Errors hold translation keys or server text. */
export function FormField({ id, label, hint, error, errorValues, ...input }: Props) {
  const t = useTranslations();
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const message = error?.message;

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id} className="text-[13px] font-semibold">
        {label}
      </Label>
      <Input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={[hintId, errorId].filter(Boolean).join(" ") || undefined}
        {...input}
      />
      {hint && (
        <p id={hintId} className="text-[13px] text-muted-foreground">
          {hint}
        </p>
      )}
      {message && (
        <p id={errorId} className="text-[13px] font-medium text-destructive">
          {t.has(message as never) ? t(message as never, errorValues as never) : message}
        </p>
      )}
    </div>
  );
}
