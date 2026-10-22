"use client";

import { Plus } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useFieldArray, useFormContext } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { labourMarginPct, localName, type PricingCatalog, type PricingFormValues } from "@/lib/admin/pricing-form";
import { ActiveCell, CellInput, KeyInput, NameCell, TableCard, td, th } from "./cells";

function AddButton({ onClick }: { onClick: () => void }) {
  const t = useTranslations("admin.pricing");
  return (
    <Button type="button" variant="ghost" size="sm" onClick={onClick}>
      <Plus aria-hidden="true" />
      {t("add")}
    </Button>
  );
}

const emptyName = { pt: "", es: "", en: "" };

/** A1.1 and A1.2: surface types and paints. */
export function SurfacesAndPaintsTab() {
  const t = useTranslations("admin.pricing");
  const { control, watch } = useFormContext<PricingFormValues>();
  const surfaces = useFieldArray({ control, name: "surfaces", keyName: "fieldKey" });
  const paints = useFieldArray({ control, name: "paints", keyName: "fieldKey" });

  return (
    <div className="flex flex-col gap-4">
      <TableCard
        title={t("surfaces.title")}
        action={<AddButton onClick={() => surfaces.append({ key: "", name: emptyName, isActive: true, clientRate: "", painterRate: "", factor: "1,00" })} />}
      >
        <table className="w-full min-w-[640px] border-collapse text-sm">
          <thead>
            <tr>
              <th className={th}>{t("surfaces.name")}</th>
              <th className={th}>{t("surfaces.clientRate")}</th>
              <th className={th}>{t("surfaces.painterRate")}</th>
              <th className={th}>{t("surfaces.factor")}</th>
              <th className={th}>{t("surfaces.labourMargin")}</th>
              <th className={th}>{t("status.title")}</th>
            </tr>
          </thead>
          <tbody>
            {surfaces.fields.map((field, i) => {
              const row = watch(`surfaces.${i}`);
              const margin = labourMarginPct(row.clientRate, row.painterRate);
              const label = row.name.pt || t("surfaces.new");
              return (
                <tr key={field.fieldKey}>
                  <td className={td}>
                    <NameCell prefix={`surfaces.${i}`} keyName={row.id ? row.key : undefined} />
                    {!row.id && <KeyInput prefix={`surfaces.${i}`} />}
                  </td>
                  <td className={td}><CellInput name={`surfaces.${i}.clientRate`} label={`${label} · ${t("surfaces.clientRate")}`} suffix="€" /></td>
                  <td className={td}><CellInput name={`surfaces.${i}.painterRate`} label={`${label} · ${t("surfaces.painterRate")}`} suffix="€" /></td>
                  <td className={td}><CellInput name={`surfaces.${i}.factor`} label={`${label} · ${t("surfaces.factor")}`} /></td>
                  <td className={`${td} pt-4 text-right tabular-nums text-muted-foreground`}>{margin === null ? "—" : `${margin}%`}</td>
                  <td className={td}><ActiveCell name={`surfaces.${i}.isActive`} label={label} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </TableCard>

      <TableCard
        title={t("paints.title")}
        action={<AddButton onClick={() => paints.append({ key: "", name: emptyName, isActive: true, pricePerLitre: "", coverage: "10", coats: "2" })} />}
      >
        <table className="w-full min-w-[600px] border-collapse text-sm">
          <thead>
            <tr>
              <th className={th}>{t("paints.name")}</th>
              <th className={th}>{t("paints.pricePerLitre")}</th>
              <th className={th}>{t("paints.coverage")}</th>
              <th className={th}>{t("paints.coats")}</th>
              <th className={th}>{t("status.title")}</th>
            </tr>
          </thead>
          <tbody>
            {paints.fields.map((field, i) => {
              const row = watch(`paints.${i}`);
              const label = row.name.pt || t("paints.new");
              return (
                <tr key={field.fieldKey}>
                  <td className={td}>
                    <NameCell prefix={`paints.${i}`} keyName={row.id ? row.key : undefined} />
                    {!row.id && <KeyInput prefix={`paints.${i}`} />}
                  </td>
                  <td className={td}><CellInput name={`paints.${i}.pricePerLitre`} label={`${label} · ${t("paints.pricePerLitre")}`} suffix="€" /></td>
                  <td className={td}><CellInput name={`paints.${i}.coverage`} label={`${label} · ${t("paints.coverage")}`} /></td>
                  <td className={td}><CellInput name={`paints.${i}.coats`} label={`${label} · ${t("paints.coats")}`} inputMode="numeric" /></td>
                  <td className={td}><ActiveCell name={`paints.${i}.isActive`} label={label} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </TableCard>
    </div>
  );
}

/** A1.3: S/M/L sizes per room type and the suggested paint. */
export function SizesTab({ catalog }: { catalog: PricingCatalog }) {
  const t = useTranslations("admin.pricing");
  const locale = useLocale();
  const { register, watch } = useFormContext<PricingFormValues>();
  const rooms = watch("roomTypes");
  const paints = catalog.paints.filter((p) => p.isActive);

  return (
    <TableCard title={t("sizes.title")}>
      <p className="mb-3 text-sm text-muted-foreground">{t("sizes.hint")}</p>
      <table className="w-full min-w-[860px] border-collapse text-sm">
        <thead>
          <tr>
            <th className={th}>{t("sizes.roomType")}</th>
            {(["s", "m", "l"] as const).map((size) => (
              <th key={size} className={th} colSpan={2}>
                {t("sizes.size", { size: size.toUpperCase() })}
              </th>
            ))}
            <th className={th}>{t("sizes.suggestedPaint")}</th>
            <th className={th}>{t("status.title")}</th>
          </tr>
        </thead>
        <tbody>
          {rooms.map((room, i) => {
            const label = room.name.pt;
            return (
              <tr key={room.id}>
                <td className={td}><NameCell prefix={`roomTypes.${i}`} keyName={room.key} /></td>
                {(["s", "m", "l"] as const).flatMap((size) => [
                  <td key={`${size}l`} className={`${td} w-20`}>
                    <CellInput name={`roomTypes.${i}.sizes.${size}.length`} label={`${label} · ${size.toUpperCase()} · ${t("sizes.length")}`} suffix="m" />
                  </td>,
                  <td key={`${size}w`} className={`${td} w-20`}>
                    <CellInput name={`roomTypes.${i}.sizes.${size}.width`} label={`${label} · ${size.toUpperCase()} · ${t("sizes.width")}`} suffix="m" />
                  </td>,
                ])}
                <td className={td}>
                  <select
                    aria-label={`${label} · ${t("sizes.suggestedPaint")}`}
                    className="h-11 w-full min-w-36 rounded-lg border-[1.5px] border-input bg-card px-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
                    {...register(`roomTypes.${i}.suggestedPaintId`)}
                  >
                    {paints.map((paint) => (
                      <option key={paint.id} value={paint.id}>
                        {localName(paint.name, locale)}
                      </option>
                    ))}
                  </select>
                </td>
                <td className={td}><ActiveCell name={`roomTypes.${i}.isActive`} label={label} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="mt-2 text-xs text-muted-foreground">{t("sizes.legend")}</p>
    </TableCard>
  );
}

/** A1.4: occupancy modifiers. */
export function OccupancyTab() {
  const t = useTranslations("admin.pricing");
  const { watch } = useFormContext<PricingFormValues>();
  const modifiers = watch("modifiers");

  return (
    <TableCard title={t("occupancy.title")}>
      <p className="mb-3 text-sm text-muted-foreground">{t("occupancy.hint")}</p>
      <table className="w-full min-w-[480px] border-collapse text-sm">
        <thead>
          <tr>
            <th className={th}>{t("occupancy.name")}</th>
            <th className={th}>{t("occupancy.factor")}</th>
            <th className={th}>{t("status.title")}</th>
          </tr>
        </thead>
        <tbody>
          {modifiers.map((modifier, i) => (
            <tr key={modifier.id}>
              <td className={td}><NameCell prefix={`modifiers.${i}`} keyName={modifier.key} /></td>
              <td className={`${td} w-32`}><CellInput name={`modifiers.${i}.factor`} label={`${modifier.name.pt} · ${t("occupancy.factor")}`} suffix="×" /></td>
              <td className={td}><ActiveCell name={`modifiers.${i}.isActive`} label={modifier.name.pt} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableCard>
  );
}

/** A1.5: catalog of extra items, before VAT. */
export function ExtrasTab() {
  const t = useTranslations("admin.pricing");
  const { control, watch } = useFormContext<PricingFormValues>();
  const extras = useFieldArray({ control, name: "extras", keyName: "fieldKey" });

  return (
    <TableCard
      title={t("extras.title")}
      action={<AddButton onClick={() => extras.append({ key: "", name: emptyName, isActive: true, clientPrice: "", painterPay: "" })} />}
    >
      <p className="mb-3 text-sm text-muted-foreground">{t("extras.hint")}</p>
      {extras.fields.length === 0 ? (
        <p className="rounded-xl bg-muted px-4 py-6 text-center text-sm text-muted-foreground">{t("extras.empty")}</p>
      ) : (
        <table className="w-full min-w-[560px] border-collapse text-sm">
          <thead>
            <tr>
              <th className={th}>{t("extras.name")}</th>
              <th className={th}>{t("extras.clientPrice")}</th>
              <th className={th}>{t("extras.painterPay")}</th>
              <th className={th}>{t("status.title")}</th>
            </tr>
          </thead>
          <tbody>
            {extras.fields.map((field, i) => {
              const row = watch(`extras.${i}`);
              const label = row.name.pt || t("extras.new");
              return (
                <tr key={field.fieldKey}>
                  <td className={td}>
                    <NameCell prefix={`extras.${i}`} keyName={row.id ? row.key : undefined} />
                    {!row.id && <KeyInput prefix={`extras.${i}`} />}
                  </td>
                  <td className={td}><CellInput name={`extras.${i}.clientPrice`} label={`${label} · ${t("extras.clientPrice")}`} suffix="€" /></td>
                  <td className={td}><CellInput name={`extras.${i}.painterPay`} label={`${label} · ${t("extras.painterPay")}`} suffix="€" /></td>
                  <td className={td}><ActiveCell name={`extras.${i}.isActive`} label={label} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </TableCard>
  );
}
