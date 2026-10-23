"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { FormError } from "@/components/auth/form-error";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api/problem";
import { SPECIALTIES, useCoveredMunicipalities, type PainterDetail } from "@/lib/painter/use-admin-painters";

const compact = (value: string) => value.replace(/\s/g, "").toUpperCase();

const schema = z.object({
  name: z.string().trim().min(1, "validation.required").max(200, "validation.tooLong"),
  email: z.string().trim(),
  locale: z.enum(["pt", "es", "en"]),
  phone: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\+[1-9]\d{7,14}$/.test(v.replace(/\s/g, "")), "validation.phone"),
  taxId: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\d{9}$/.test(v), "painter.onboarding.errors.taxId"),
  specialties: z.array(z.string()),
  municipalities: z.array(z.string()),
  iban: z.string().refine((v) => v.trim() === "" || /^PT50\d{21}$/.test(compact(v)), "painter.onboarding.errors.iban"),
  notes: z.string().max(2000, "validation.tooLong"),
  rating: z
    .string()
    .trim()
    .refine((v) => v === "" || (/^\d(?:[.,]\d)?$/.test(v) && Number(v.replace(",", ".")) >= 1 && Number(v.replace(",", ".")) <= 5), "admin.painters.form.ratingError"),
});
export type PainterFormValues = z.infer<typeof schema>;

export type PainterFormOutput = {
  name: string;
  email: string;
  locale: "pt" | "es" | "en";
  phone: string | null;
  taxId: string | null;
  specialties: string[];
  municipalities: string[];
  iban: string | null;
  notes: string | null;
  rating: number | null;
};

/**
 * The painter's data (A4.1, A4.2): on creation name, email and language are required and the rest may be prefilled
 * for the painter to confirm; on the ficha the IBAN stays masked and is replaced only when typed.
 */
export function PainterForm({
  painter,
  pending,
  error,
  submitLabel,
  onSubmit,
}: {
  painter?: PainterDetail;
  pending: boolean;
  error: unknown;
  submitLabel: string;
  onSubmit: (values: PainterFormOutput) => void;
}) {
  const t = useTranslations("admin.painters.form");
  const municipalities = useCoveredMunicipalities();
  const creating = !painter;
  const form = useForm<PainterFormValues>({
    resolver: zodResolver(
      creating
        ? schema.refine((v) => /^\S+@\S+\.\S+$/.test(v.email), { path: ["email"], message: "validation.email" })
        : schema,
    ),
    defaultValues: {
      name: painter?.name ?? "",
      email: painter?.email ?? "",
      locale: "pt",
      phone: painter?.phone ?? "",
      taxId: painter?.taxId ?? "",
      specialties: [...(painter?.specialties ?? [])],
      municipalities: [...(painter?.municipalities ?? [])],
      iban: "",
      notes: painter?.notes ?? "",
      rating: painter?.rating != null ? String(painter.rating) : "",
    },
  });
  const { errors } = form.formState;
  const server = error instanceof ApiError ? error.problem?.errors : undefined;
  const serverError = (field: string) => {
    const message = server?.[field]?.[0];
    return message ? { type: "server", message } : undefined;
  };

  const submit = form.handleSubmit((v) =>
    onSubmit({
      name: v.name.trim(),
      email: v.email.trim(),
      locale: v.locale,
      phone: v.phone.trim() ? v.phone.replace(/\s/g, "") : null,
      taxId: v.taxId.trim() || null,
      specialties: v.specialties,
      municipalities: v.municipalities,
      iban: v.iban.trim() ? compact(v.iban) : null,
      notes: v.notes.trim() || null,
      rating: v.rating.trim() ? Number(v.rating.replace(",", ".")) : null,
    }),
  );

  return (
    <form noValidate onSubmit={submit} className="flex flex-col gap-4">
      {!server && <FormError error={error} />}
      <div className="grid gap-4 md:grid-cols-2">
        <FormField id="name" label={t("name")} error={errors.name ?? serverError("name")} {...form.register("name")} />
        {creating ? (
          <FormField
            id="email"
            type="email"
            spellCheck={false}
            label={t("email")}
            error={errors.email ?? serverError("email")}
            {...form.register("email")}
          />
        ) : (
          <FormField id="email" label={t("email")} value={painter.email} readOnly disabled />
        )}
        {creating && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor="locale" className="text-[13px] font-semibold">
              {t("locale")}
            </label>
            <select
              id="locale"
              className="h-11 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
              {...form.register("locale")}
            >
              <option value="pt">Português</option>
              <option value="es">Español</option>
              <option value="en">English</option>
            </select>
          </div>
        )}
        <FormField id="phone" type="tel" label={t("phone")} hint={t("phoneHint")} error={errors.phone ?? serverError("phone")} {...form.register("phone")} />
        <FormField id="taxId" inputMode="numeric" label={t("taxId")} error={errors.taxId ?? serverError("taxId")} {...form.register("taxId")} />
        <FormField
          id="iban"
          autoComplete="off"
          spellCheck={false}
          label={t("iban")}
          hint={painter?.ibanMasked ? t("ibanKeep", { iban: painter.ibanMasked }) : t("ibanHint")}
          placeholder={painter?.ibanMasked ?? "PT50 …"}
          error={errors.iban ?? serverError("iban")}
          {...form.register("iban")}
        />
        {!creating && (
          <FormField
            id="rating"
            inputMode="decimal"
            label={t("rating")}
            hint={t("ratingHint")}
            error={errors.rating ?? serverError("rating")}
            {...form.register("rating")}
          />
        )}
      </div>

      <Controller
        control={form.control}
        name="specialties"
        render={({ field }) => (
          <ChipGroup legend={t("specialties")} options={SPECIALTIES.map((s) => ({ value: s, label: t(`specialty.${s}`) }))} {...field} />
        )}
      />
      <Controller
        control={form.control}
        name="municipalities"
        render={({ field }) =>
          municipalities.isPending ? (
            <p role="status" className="text-sm text-muted-foreground">
              {t("loadingZones")}
            </p>
          ) : (
            <ChipGroup
              legend={t("zones")}
              options={(municipalities.data ?? []).map((m) => ({ value: m, label: m }))}
              error={serverError("municipalities")?.message}
              {...field}
            />
          )
        }
      />
      <div className="flex flex-col gap-1.5">
        <label htmlFor="notes" className="text-[13px] font-semibold">
          {t("notes")}
        </label>
        <textarea
          id="notes"
          rows={3}
          maxLength={2000}
          className="rounded-xl border-[1.5px] border-input bg-card px-3 py-2 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
          {...form.register("notes")}
        />
      </div>

      <Button type="submit" disabled={pending} className="self-start">
        {pending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {submitLabel}
      </Button>
    </form>
  );
}

/** Toggle chips for a list of values (specialties, concelhos). */
function ChipGroup({
  legend,
  options,
  value,
  onChange,
  error,
}: {
  legend: string;
  options: { value: string; label: string }[];
  value: string[];
  onChange: (value: string[]) => void;
  error?: string;
}) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1.5 text-[13px] font-semibold">{legend}</legend>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => {
          const checked = value.includes(option.value);
          return (
            <label
              key={option.value}
              className="inline-flex min-h-11 cursor-pointer items-center rounded-xl border border-border px-3 text-sm font-medium has-checked:border-primary has-checked:bg-primary-soft has-checked:text-primary has-focus-visible:ring-[3px] has-focus-visible:ring-ring/50"
            >
              <input
                type="checkbox"
                className="sr-only"
                checked={checked}
                onChange={() => onChange(checked ? value.filter((x) => x !== option.value) : [...value, option.value])}
              />
              {option.label}
            </label>
          );
        })}
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
    </fieldset>
  );
}
