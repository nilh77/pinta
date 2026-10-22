"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2, Loader2, RefreshCw } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Tabs } from "radix-ui";
import { useEffect, useState } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { pricingFormSchema, toFormValues, type PricingCatalog, type PricingFormValues } from "@/lib/admin/pricing-form";
import { SaveError, usePricingCatalog, useSavePricing } from "@/lib/admin/use-pricing";
import { ExtrasTab, OccupancyTab, SizesTab, SurfacesAndPaintsTab } from "./catalog-tabs";
import { RulesTab } from "./rules-tab";
import { SimulatorPanel } from "./simulator-panel";

const TABS = ["surfacesPaints", "sizes", "occupancy", "extras", "rules"] as const;

/** A1 · Precios y simulador: edit the catalog and global rules, try them in the simulator (A1.1–A1.8). */
export function PricingScreen() {
  const t = useTranslations("admin.pricing");
  const catalog = usePricingCatalog();

  return (
    <div className="mx-auto flex max-w-[1400px] flex-col gap-5">
      {catalog.isPending ? (
        <div role="status" className="flex flex-col gap-4">
          <span className="sr-only">{t("loading")}</span>
          <div className="h-10 w-48 animate-pulse rounded-xl bg-primary-soft motion-reduce:animate-none" />
          <div className="h-80 animate-pulse rounded-2xl bg-primary-soft motion-reduce:animate-none" />
        </div>
      ) : catalog.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3 rounded-2xl border border-border bg-card p-6">
          <h1 className="text-2xl font-bold">{t("title")}</h1>
          <p className="text-muted-foreground">{t("loadError")}</p>
          <Button onClick={() => catalog.refetch()}>
            <RefreshCw aria-hidden="true" />
            {t("retry")}
          </Button>
        </div>
      ) : (
        <PricingEditor catalog={catalog.data} catalogVersion={catalog.dataUpdatedAt} onReload={() => catalog.refetch()} />
      )}
    </div>
  );
}

function PricingEditor({ catalog, catalogVersion, onReload }: { catalog: PricingCatalog; catalogVersion: number; onReload: () => void }) {
  const t = useTranslations("admin.pricing");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const form = useForm<PricingFormValues>({
    resolver: zodResolver(pricingFormSchema),
    defaultValues: toFormValues(catalog, locale),
  });
  const save = useSavePricing();
  const [tab, setTab] = useState<(typeof TABS)[number]>("surfacesPaints");
  const [saved, setSaved] = useState(false);
  const { isDirty } = form.formState;

  // New data after a save or a reload replaces what is on screen.
  useEffect(() => {
    form.reset(toFormValues(catalog, locale));
  }, [catalog, locale, form]);

  // Unsaved changes → warn before leaving (docs/screens.md, A1).
  useEffect(() => {
    if (!isDirty) return;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [isDirty]);

  const onSubmit = form.handleSubmit(
    (values) => {
      setSaved(false);
      save.mutate({ values, dirty: form.formState.dirtyFields, loaded: catalog }, { onSuccess: () => setSaved(true) });
    },
    (errors) => {
      // Show the first tab with an error.
      const first = TABS.find((name) => tabHasError(name, errors));
      if (first) setTab(first);
    },
  );

  const error = save.error instanceof SaveError ? save.error : null;
  const conflict = error?.code === "concurrency_conflict" || save.error?.message === "concurrency_conflict";

  return (
    <FormProvider {...form}>
      <form noValidate onSubmit={onSubmit} className="flex flex-col gap-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="max-w-2xl">
            <h1 className="text-3xl font-extrabold">{t("title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
          </div>
          <div className="flex items-center gap-3">
            {isDirty && !save.isPending && <span className="text-sm text-muted-foreground">{t("unsaved")}</span>}
            {saved && !isDirty && (
              <span role="status" className="flex items-center gap-1.5 text-sm font-medium text-primary">
                <CheckCircle2 aria-hidden="true" className="size-4" />
                {t("saved")}
              </span>
            )}
            <Button type="submit" size="lg" disabled={!isDirty || save.isPending}>
              {save.isPending && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
              {t("save")}
            </Button>
          </div>
        </div>

        {conflict ? (
          <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-accent-soft px-4 py-3 text-sm text-accent-ink">
            <span className="font-semibold">{t("conflict")}</span>
            <Button type="button" variant="outline" size="sm" onClick={() => { save.reset(); onReload(); }}>
              <RefreshCw aria-hidden="true" />
              {t("reload")}
            </Button>
          </div>
        ) : error ? (
          <div role="alert" className="rounded-xl bg-destructive/10 px-4 py-3 text-sm text-destructive">
            <p className="font-semibold">
              {t("saveError", { item: error.label === "settings" ? t("tabs.rules") : error.label })} {tErrors(error.code)}
            </p>
            {error.problem?.errors && (
              <ul className="mt-1 list-disc pl-5">
                {Object.values(error.problem.errors).flat().map((message, i) => <li key={i}>{message}</li>)}
              </ul>
            )}
          </div>
        ) : null}

        <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
          <Tabs.Root value={tab} onValueChange={(value) => setTab(value as (typeof TABS)[number])} className="flex min-w-0 flex-col gap-4">
            <Tabs.List aria-label={t("tabs.label")} className="flex gap-1.5 overflow-x-auto pb-1">
              {TABS.map((name) => (
                <Tabs.Trigger
                  key={name}
                  value={name}
                  className="h-11 shrink-0 rounded-xl px-4 text-sm font-semibold whitespace-nowrap text-muted-foreground hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none data-[state=active]:bg-foreground data-[state=active]:text-background"
                >
                  {t(`tabs.${name}`)}
                  {tabHasError(name, form.formState.errors) && <span className="ml-1.5 inline-block size-2 rounded-full bg-destructive" aria-label={t("tabHasErrors")} />}
                </Tabs.Trigger>
              ))}
            </Tabs.List>
            {/* forceMount keeps every tab's inputs registered, so validation and dirty tracking cover all of them. */}
            <Tabs.Content value="surfacesPaints" forceMount className="data-[state=inactive]:hidden"><SurfacesAndPaintsTab /></Tabs.Content>
            <Tabs.Content value="sizes" forceMount className="data-[state=inactive]:hidden"><SizesTab catalog={catalog} /></Tabs.Content>
            <Tabs.Content value="occupancy" forceMount className="data-[state=inactive]:hidden"><OccupancyTab /></Tabs.Content>
            <Tabs.Content value="extras" forceMount className="data-[state=inactive]:hidden"><ExtrasTab /></Tabs.Content>
            <Tabs.Content value="rules" forceMount className="data-[state=inactive]:hidden"><RulesTab /></Tabs.Content>
          </Tabs.Root>
          <div className="xl:sticky xl:top-6">
            <SimulatorPanel catalog={catalog} catalogVersion={catalogVersion} />
          </div>
        </div>
      </form>
    </FormProvider>
  );
}

const TAB_FIELDS: Record<(typeof TABS)[number], (keyof PricingFormValues)[]> = {
  surfacesPaints: ["surfaces", "paints"],
  sizes: ["roomTypes"],
  occupancy: ["modifiers"],
  extras: ["extras"],
  rules: ["settings"],
};

function tabHasError(tab: (typeof TABS)[number], errors: object): boolean {
  return TAB_FIELDS[tab].some((field) => field in errors);
}
