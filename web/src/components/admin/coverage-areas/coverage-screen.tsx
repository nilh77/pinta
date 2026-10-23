"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Loader2, Plus } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { orThrow, type Schemas } from "@/lib/jobs/api";
import { formatDateTime } from "@/lib/format";

type Area = Schemas["CoverageAreaDto"];
const coverageKey = ["admin", "coverage"] as const;
const fieldClass =
  "h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive";

/** Cobertura (A1.7): concelhos with their postal prefixes on or off, new prefixes, and the waitlist with its export. */
export function CoverageScreen() {
  const t = useTranslations("admin.coverage");
  const areas = useQuery({ queryKey: coverageKey, queryFn: async () => orThrow(await api.GET("/api/admin/coverage")) });
  const waitlist = useQuery({ queryKey: ["admin", "waitlist"], queryFn: async () => orThrow(await api.GET("/api/admin/coverage/waitlist")) });
  const byMunicipality = Object.entries(
    (areas.data ?? []).reduce<Record<string, Area[]>>((groups, area) => {
      (groups[area.municipality] ??= []).push(area);
      return groups;
    }, {}),
  );

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
        <section aria-labelledby="areas-title" className="flex flex-col gap-3">
          <h2 id="areas-title" className="font-bold">
            {t("areas")}
          </h2>
          {areas.isPending ? (
            <Loading />
          ) : areas.isError ? (
            <LoadError onRetry={() => areas.refetch()} />
          ) : (
            <>
              <ul className="grid gap-2 sm:grid-cols-2">
                {byMunicipality.map(([municipality, list]) => (
                  <li key={municipality} className="rounded-2xl border border-border bg-card p-3">
                    <p className="mb-2 font-semibold">{municipality}</p>
                    <ul className="flex flex-wrap gap-2">
                      {list.map((area) => (
                        <li key={area.id}>
                          <PrefixToggle area={area} />
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
              <AddPrefix municipalities={byMunicipality.map(([m]) => m)} />
            </>
          )}
        </section>

        <section aria-labelledby="waitlist-title" className="flex flex-col gap-3">
          <div className="flex items-center justify-between gap-2">
            <h2 id="waitlist-title" className="font-bold">
              {t("waitlist", { count: waitlist.data?.length ?? 0 })}
            </h2>
            <Button asChild variant="outline" size="sm">
              <a href="/api/admin/coverage/waitlist/export" download>
                <Download aria-hidden="true" />
                {t("export")}
              </a>
            </Button>
          </div>
          {waitlist.isPending ? (
            <Loading />
          ) : waitlist.isError ? (
            <LoadError onRetry={() => waitlist.refetch()} />
          ) : waitlist.data.length === 0 ? (
            <p className="rounded-2xl border border-dashed border-border bg-card p-4 text-sm text-muted-foreground">{t("waitlistEmpty")}</p>
          ) : (
            <WaitlistTable entries={waitlist.data} />
          )}
        </section>
      </div>
    </div>
  );
}

function Loading() {
  const t = useTranslations("admin.coverage");
  return (
    <p role="status" className="flex items-center gap-2 text-muted-foreground">
      <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
      {t("loading")}
    </p>
  );
}

function LoadError({ onRetry }: { onRetry: () => void }) {
  const t = useTranslations("admin.coverage");
  return (
    <div role="alert" className="flex flex-col items-start gap-2">
      <p className="text-destructive">{t("loadError")}</p>
      <Button variant="outline" size="sm" onClick={onRetry}>
        {t("retry")}
      </Button>
    </div>
  );
}

/** A switch per prefix; a stale version reloads the list (409 concurrency_conflict). */
function PrefixToggle({ area }: { area: Area }) {
  const t = useTranslations("admin.coverage");
  const errorText = useErrorText();
  const queryClient = useQueryClient();
  const toggle = useMutation({
    mutationFn: async () =>
      orThrow(
        await api.PUT("/api/admin/coverage/{id}", {
          params: { path: { id: area.id } },
          body: { postalPrefix: null, municipality: area.municipality, isActive: !area.isActive, version: area.version },
        }),
      ),
    onError: (error) => toast.error(errorText(error)),
    onSettled: () => queryClient.invalidateQueries({ queryKey: coverageKey }),
  });

  return (
    <button
      type="button"
      role="switch"
      aria-checked={area.isActive}
      aria-label={t("toggle", { prefix: area.postalPrefix })}
      disabled={toggle.isPending}
      onClick={() => toggle.mutate()}
      className="group flex min-h-11 items-center gap-2 rounded-xl border border-border px-3 text-sm font-semibold aria-checked:border-primary aria-checked:bg-primary-soft aria-checked:text-primary disabled:opacity-60"
    >
      <span aria-hidden="true" className="relative block h-5 w-9 shrink-0 rounded-full bg-border transition-colors group-aria-checked:bg-primary motion-reduce:transition-none">
        <span className="absolute top-0.5 left-0.5 size-4 rounded-full bg-white transition-transform group-aria-checked:translate-x-4 motion-reduce:transition-none" />
      </span>
      {area.postalPrefix}
    </button>
  );
}

function AddPrefix({ municipalities }: { municipalities: string[] }) {
  const t = useTranslations("admin.coverage");
  const errorText = useErrorText();
  const id = useId();
  const queryClient = useQueryClient();
  const [prefix, setPrefix] = useState("");
  const [municipality, setMunicipality] = useState("");
  const create = useMutation({
    mutationFn: async () => orThrow(await api.POST("/api/admin/coverage", { body: { postalPrefix: prefix, municipality: municipality.trim(), isActive: true, version: null } })),
    onSuccess: () => {
      setPrefix("");
      toast.success(t("added"));
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: coverageKey }),
  });
  const fieldErrors = create.error instanceof ApiError ? (create.error.problem?.errors ?? {}) : {};
  const valid = /^\d{4}$/.test(prefix) && municipality.trim() !== "";

  return (
    <form
      className="flex flex-col gap-2 rounded-2xl border border-border bg-card p-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (valid) create.mutate();
      }}
    >
      <p className="text-sm font-bold">{t("addTitle")}</p>
      <div className="grid gap-2 sm:grid-cols-[120px_minmax(0,1fr)_auto] sm:items-end">
        <label className="flex flex-col gap-1 text-xs font-semibold">
          {t("prefix")}
          <input
            className={fieldClass}
            inputMode="numeric"
            maxLength={4}
            placeholder="2710"
            value={prefix}
            aria-invalid={fieldErrors.postalPrefix ? true : undefined}
            aria-describedby={fieldErrors.postalPrefix ? `${id}-error` : undefined}
            onChange={(e) => setPrefix(e.target.value.replace(/\D/g, ""))}
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold">
          {t("municipality")}
          <input className={fieldClass} list={`${id}-municipalities`} maxLength={100} value={municipality} onChange={(e) => setMunicipality(e.target.value)} />
          <datalist id={`${id}-municipalities`}>
            {municipalities.map((m) => (
              <option key={m} value={m} />
            ))}
          </datalist>
        </label>
        <Button type="submit" disabled={!valid || create.isPending}>
          {create.isPending ? <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <Plus aria-hidden="true" />}
          {t("add")}
        </Button>
      </div>
      {fieldErrors.postalPrefix && (
        <p id={`${id}-error`} className="text-xs font-medium text-destructive">
          {fieldErrors.postalPrefix.join(" ")}
        </p>
      )}
      {create.error && !fieldErrors.postalPrefix && <p className="text-xs font-medium text-destructive">{errorText(create.error)}</p>}
    </form>
  );
}

function WaitlistTable({ entries }: { entries: Schemas["WaitlistEntryDto"][] }) {
  const t = useTranslations("admin.coverage");
  const locale = useLocale();
  return (
    <div className="max-h-[480px] overflow-auto rounded-2xl border border-border bg-card">
      <table className="w-full text-left text-sm">
        <thead className="sticky top-0 bg-background text-xs text-muted-foreground">
          <tr>
            <th scope="col" className="px-3 py-2 font-semibold">{t("email")}</th>
            <th scope="col" className="px-3 py-2 font-semibold">{t("postalCode")}</th>
            <th scope="col" className="px-3 py-2 font-semibold">{t("date")}</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.id} className="border-t border-border">
              <td className="max-w-[180px] truncate px-3 py-2">{entry.email}</td>
              <td className="px-3 py-2 whitespace-nowrap">
                {entry.postalCode} <span className="text-xs text-muted-foreground uppercase">{entry.locale}</span>
              </td>
              <td className="px-3 py-2 whitespace-nowrap text-muted-foreground">{formatDateTime(locale, entry.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
