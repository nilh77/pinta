"use client";

import { Loader2 } from "lucide-react";
import { Dialog } from "radix-ui";
import type { FormEvent, ReactNode } from "react";
import { Button } from "./button";

type FormDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: ReactNode;
  children?: ReactNode;
  submitLabel: string;
  cancelLabel: string;
  onSubmit: () => void;
  pending?: boolean;
  submitDisabled?: boolean;
  destructive?: boolean;
};

/** A dialog that asks for a few fields before an action (reason, message, date). Radix gives focus trap and Escape. */
export function FormDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  submitLabel,
  cancelLabel,
  onSubmit,
  pending = false,
  submitDisabled = false,
  destructive = false,
}: FormDialogProps) {
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!pending && !submitDisabled) onSubmit();
  };

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-foreground/40" />
        <Dialog.Content
          className="fixed top-1/2 left-1/2 z-50 max-h-[90dvh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-3xl bg-card p-6 shadow-xl"
        >
          <form onSubmit={submit} noValidate>
            <Dialog.Title className="font-display text-xl font-bold">{title}</Dialog.Title>
            {description ? (
              <Dialog.Description asChild>
                <div className="mt-2 text-sm text-muted-foreground">{description}</div>
              </Dialog.Description>
            ) : (
              <Dialog.Description className="sr-only">{title}</Dialog.Description>
            )}
            {children && <div className="mt-4 flex flex-col gap-3">{children}</div>}
            <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <Dialog.Close asChild>
                <Button type="button" variant="ghost">
                  {cancelLabel}
                </Button>
              </Dialog.Close>
              <Button type="submit" variant={destructive ? "destructive" : "default"} disabled={pending || submitDisabled}>
                {pending && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
                {submitLabel}
              </Button>
            </div>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** Label + textarea for dialogs; counts characters against `max`. */
export function TextAreaField({
  id,
  label,
  value,
  onChange,
  max,
  required,
  error,
  hint,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  max: number;
  required?: boolean;
  error?: string;
  hint?: string;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[13px] font-semibold">
        {label}
      </label>
      <textarea
        id={id}
        value={value}
        required={required}
        maxLength={max}
        rows={4}
        aria-invalid={error ? true : undefined}
        aria-describedby={`${id}-count${error ? ` ${id}-error` : ""}`}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-xl border-[1.5px] border-input bg-card px-3.5 py-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive"
      />
      <p id={`${id}-count`} className="flex justify-between text-xs text-muted-foreground">
        <span>{hint}</span>
        <span>
          {value.length}/{max}
        </span>
      </p>
      {error && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
