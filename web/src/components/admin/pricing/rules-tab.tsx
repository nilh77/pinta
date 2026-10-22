"use client";

import { useTranslations } from "next-intl";
import { useFormContext } from "react-hook-form";
import { Checkbox } from "@/components/ui/checkbox";
import type { PricingFormValues, SettingsFormValues } from "@/lib/admin/pricing-form";
import { CellInput } from "./cells";

type Field = { name: Exclude<keyof SettingsFormValues, "version" | "siteVisitDeductible">; suffix: string };

const GROUPS: { key: "job" | "price" | "range" | "operations"; fields: Field[] }[] = [
  {
    key: "job",
    fields: [
      { name: "minJobAmount", suffix: "€" },
      { name: "maxJobAmount", suffix: "€" },
      { name: "premiumAreaM2", suffix: "m²" },
      { name: "defaultHeight", suffix: "m" },
    ],
  },
  {
    key: "price",
    fields: [
      { name: "vatPct", suffix: "%" },
      { name: "wastePct", suffix: "%" },
      { name: "minMarginPct", suffix: "%" },
      { name: "depositPct", suffix: "%" },
      { name: "quoteValidityDays", suffix: "d" },
    ],
  },
  {
    key: "range",
    fields: [
      { name: "rangeLowPct", suffix: "%" },
      { name: "rangeHighPct", suffix: "%" },
      { name: "rangeSizeWideningPct", suffix: "%" },
      { name: "rangeDamageWideningPct", suffix: "%" },
    ],
  },
  {
    key: "operations",
    fields: [
      { name: "siteVisitPrice", suffix: "€" },
      { name: "reviewDeadlineBusinessDays", suffix: "d" },
      { name: "fullRefundMinDays", suffix: "d" },
      { name: "lateRefundPct", suffix: "%" },
    ],
  },
];

/** A1.6: global rules of docs/pricing-engine.md. They apply to quotes calculated from now on. */
export function RulesTab() {
  const t = useTranslations("admin.pricing.rules");
  const { watch, setValue } = useFormContext<PricingFormValues>();
  const deductible = watch("settings.siteVisitDeductible");

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {GROUPS.map((group) => (
        <section key={group.key} className="flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 sm:p-5">
          <h2 className="text-lg font-bold">{t(`groups.${group.key}`)}</h2>
          <div className="flex flex-col gap-3">
            {group.fields.map((field) => (
              <div key={field.name} className="grid grid-cols-[1fr_8rem] items-start gap-3">
                <label htmlFor={`field-settings.${field.name}`} className="pt-3 text-sm font-medium">
                  {t(`fields.${field.name}`)}
                </label>
                <CellInput
                  name={`settings.${field.name}`}
                  label={t(`fields.${field.name}`)}
                  suffix={field.suffix}
                  inputMode={field.suffix === "d" ? "numeric" : "decimal"}
                />
              </div>
            ))}
            {group.key === "operations" && (
              <label htmlFor="field-settings.siteVisitDeductible" className="flex min-h-11 items-center gap-2 text-sm font-medium">
                <Checkbox
                  id="field-settings.siteVisitDeductible"
                  checked={deductible}
                  onCheckedChange={(checked) => setValue("settings.siteVisitDeductible", checked === true, { shouldDirty: true })}
                />
                {t("fields.siteVisitDeductible")}
              </label>
            )}
          </div>
        </section>
      ))}
    </div>
  );
}
