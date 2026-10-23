"use client";

import { Loader2, Plus, Star } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { useAdminPainters, type PainterListItem } from "@/lib/painter/use-admin-painters";
import { PainterStatusBadge } from "./painter-status-badge";

/** Pintores (A4.1): status, zones, jobs done and rating; invite a new one. */
export function PaintersScreen() {
  const t = useTranslations("admin.painters");
  const painters = useAdminPainters();

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-[28px] font-extrabold tracking-tight">{t("title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
        <Button asChild>
          <Link href="/admin/pintores/nuevo">
            <Plus aria-hidden="true" />
            {t("invite")}
          </Link>
        </Button>
      </header>

      {painters.isPending ? (
        <p role="status" className="flex items-center gap-2 py-10 text-muted-foreground">
          <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
          {t("loading")}
        </p>
      ) : painters.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3 py-10">
          <p className="text-destructive">{t("loadError")}</p>
          <Button variant="outline" onClick={() => painters.refetch()}>
            {t("retry")}
          </Button>
        </div>
      ) : painters.data.length === 0 ? (
        <div className="flex flex-col items-center gap-3 rounded-[20px] border border-dashed border-border bg-card px-6 py-12 text-center">
          <p className="text-lg font-bold">{t("empty")}</p>
          <Button asChild>
            <Link href="/admin/pintores/nuevo">{t("invite")}</Link>
          </Button>
        </div>
      ) : (
        <>
          <ul className="flex flex-col gap-2 md:hidden">
            {painters.data.map((p) => (
              <li key={p.id}>
                <Link href={`/admin/pintores/${p.id}`} className="flex flex-col gap-2 rounded-2xl border border-border bg-card p-3 hover:border-primary">
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-bold">{p.name}</p>
                    <PainterStatusBadge status={p.status} />
                  </div>
                  <p className="text-sm text-muted-foreground">{p.municipalities.join(", ") || t("noZones")}</p>
                  <Stats painter={p} />
                </Link>
              </li>
            ))}
          </ul>
          <div className="hidden overflow-x-auto rounded-2xl border border-border bg-card md:block">
            <table className="w-full text-left text-sm">
              <thead className="text-xs text-muted-foreground">
                <tr>
                  {(["name", "status", "zones", "jobs", "rating"] as const).map((key) => (
                    <th key={key} className="px-3 py-2.5 font-semibold">
                      {t(`columns.${key}`)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {painters.data.map((p) => (
                  <tr key={p.id} className="border-t border-border hover:bg-background">
                    <td className="px-3 py-2.5">
                      <Link href={`/admin/pintores/${p.id}`} className="font-semibold text-primary underline-offset-4 hover:underline">
                        {p.name}
                      </Link>
                      <p className="text-xs text-muted-foreground">{p.email}</p>
                    </td>
                    <td className="px-3 py-2.5">
                      <PainterStatusBadge status={p.status} />
                    </td>
                    <td className="px-3 py-2.5">{p.municipalities.join(", ") || "—"}</td>
                    <td className="px-3 py-2.5 tabular-nums">{p.jobsDone}</td>
                    <td className="px-3 py-2.5 tabular-nums">{p.rating ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function Stats({ painter }: { painter: PainterListItem }) {
  const t = useTranslations("admin.painters");
  return (
    <p className="flex items-center gap-3 text-sm">
      <span>{t("jobsDone", { count: painter.jobsDone })}</span>
      {painter.rating != null && (
        <span className="inline-flex items-center gap-1">
          <Star aria-hidden="true" className="size-3.5 fill-accent text-accent" />
          {painter.rating}
        </span>
      )}
    </p>
  );
}
