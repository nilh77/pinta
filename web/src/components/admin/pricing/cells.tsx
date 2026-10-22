"use client";

import { Languages } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState, type ComponentProps } from "react";
import { get, useFormContext, type FieldError, type FieldPath } from "react-hook-form";
import { Checkbox } from "@/components/ui/checkbox";
import type { PricingFormValues } from "@/lib/admin/pricing-form";
import { cn } from "@/lib/utils";

type Path = FieldPath<PricingFormValues>;

/** The translated message of a field error (translation key from the zod schema). */
export function useFieldError(path: Path): string | undefined {
  const t = useTranslations();
  const { formState } = useFormContext<PricingFormValues>();
  const error = get(formState.errors, path) as FieldError | undefined;
  if (!error?.message) return undefined;
  return t.has(error.message as never) ? t(error.message as never) : error.message;
}

type CellInputProps = Omit<ComponentProps<"input">, "name"> & { name: Path; label: string; suffix?: string };

/** A compact input for a table cell; the column header is its visible label, `label` its accessible name. */
export function CellInput({ name, label, suffix, className, ...props }: CellInputProps) {
  const { register } = useFormContext<PricingFormValues>();
  const error = useFieldError(name);
  const id = `field-${name}`;
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <div className="relative">
        <input
          id={id}
          inputMode="decimal"
          autoComplete="off"
          aria-label={label}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          className={cn(
            "h-11 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-right text-sm tabular-nums outline-none",
            "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive",
            suffix && "pr-8",
            className,
          )}
          {...register(name)}
          {...props}
        />
        {suffix && (
          <span aria-hidden="true" className="pointer-events-none absolute inset-y-0 right-2.5 flex items-center text-xs text-muted-foreground">
            {suffix}
          </span>
        )}
      </div>
      {error && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

/** Portuguese name (required) with the Spanish and English translations behind a toggle. */
export function NameCell({ prefix, keyName }: { prefix: `${"paints" | "surfaces" | "roomTypes" | "modifiers" | "extras"}.${number}`; keyName?: string }) {
  const t = useTranslations("admin.pricing.names");
  const { register } = useFormContext<PricingFormValues>();
  const [open, setOpen] = useState(false);
  const ptError = useFieldError(`${prefix}.name.pt`);
  const id = `field-${prefix}.name`;

  return (
    <div className="flex min-w-44 flex-col gap-1.5">
      <div className="flex items-center gap-1">
        <input
          id={`${id}.pt`}
          aria-label={t("pt")}
          aria-invalid={ptError ? true : undefined}
          aria-describedby={ptError ? `${id}.pt-error` : undefined}
          className="h-11 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-sm font-medium outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive"
          {...register(`${prefix}.name.pt`)}
        />
        <button
          type="button"
          aria-expanded={open}
          aria-controls={`${id}-translations`}
          aria-label={t("toggle")}
          title={t("toggle")}
          onClick={() => setOpen((value) => !value)}
          className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted-foreground hover:bg-primary-soft hover:text-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none aria-expanded:bg-primary-soft aria-expanded:text-primary"
        >
          <Languages aria-hidden="true" className="size-4" />
        </button>
      </div>
      {ptError && (
        <p id={`${id}.pt-error`} className="text-xs font-medium text-destructive">
          {ptError}
        </p>
      )}
      {keyName && <span className="font-mono text-[11px] text-muted-foreground">{keyName}</span>}
      {open && (
        <div id={`${id}-translations`} className="flex flex-col gap-1.5">
          {(["es", "en"] as const).map((language) => (
            <label key={language} className="flex items-center gap-2 text-xs font-semibold text-muted-foreground uppercase">
              {language}
              <input
                lang={language}
                aria-label={t(language)}
                placeholder={t("fallback")}
                className="h-10 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-sm font-normal normal-case text-foreground outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
                {...register(`${prefix}.name.${language}`)}
              />
            </label>
          ))}
        </div>
      )}
    </div>
  );
}

/** The key of a row not saved yet; saved rows keep their key. */
export function KeyInput({ prefix }: { prefix: `${"paints" | "surfaces" | "extras"}.${number}` }) {
  const t = useTranslations("admin.pricing");
  return <CellInput name={`${prefix}.key`} label={t("key")} inputMode="text" placeholder={t("keyPlaceholder")} className="text-left font-mono" />;
}

/** Active / inactive. Items are never deleted (docs/data-model.md). */
export function ActiveCell({ name, label }: { name: Path; label: string }) {
  const t = useTranslations("admin.pricing.status");
  const { watch, setValue } = useFormContext<PricingFormValues>();
  const active = watch(name) as boolean;
  const error = useFieldError(name);
  const id = `field-${name}`;
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="flex min-h-11 items-center gap-2 text-sm">
        <Checkbox
          id={id}
          checked={active}
          aria-label={`${label}: ${t("active")}`}
          onCheckedChange={(checked) => setValue(name, checked === true, { shouldDirty: true })}
        />
        <span className={cn("rounded-full px-2 py-0.5 text-xs font-semibold", active ? "bg-primary-soft text-primary" : "bg-muted text-muted-foreground")}>
          {active ? t("active") : t("inactive")}
        </span>
      </label>
      {error && <p className="text-xs font-medium text-destructive">{error}</p>}
    </div>
  );
}

/** A titled card with a table that scrolls sideways on small screens. */
export function TableCard({ title, action, children }: { title: string; action?: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 sm:p-5">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-bold">{title}</h2>
        {action}
      </div>
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">{children}</div>
    </section>
  );
}

export const th = "px-1.5 pb-2 text-left text-xs font-medium text-muted-foreground whitespace-nowrap border-b border-border";
export const td = "px-1.5 py-2 align-top border-b border-border/60";
