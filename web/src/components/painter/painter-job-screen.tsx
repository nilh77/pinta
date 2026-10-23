"use client";

import { ArrowLeft, CircleCheck, Clock, ImagePlus, Loader2, MapPin, Navigation, Phone, Play, Trash2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useRef, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Link } from "@/i18n/navigation";
import type { components } from "@/lib/api/schema";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatArea, formatCents, formatDate, formatMetres } from "@/lib/format";
import { useCatalog } from "@/lib/quote/use-quote-data";
import { prepareImage } from "@/lib/quote/uploads";
import {
  uploadResultPhoto,
  useFinishJob,
  usePainterJob,
  useStartJob,
  type PainterElement,
  type PainterJobDetail,
} from "@/lib/painter/use-painter";

type JobFile = components["schemas"]["JobFile"];
const RESULT_TYPES = ["image/jpeg", "image/png", "image/webp"];
const WALLS = ["A", "B", "C", "D"];

/** P2 · Trabajo y ejecución (board PintorDetalle) [P1.3, P2.1, P2.2]. Never the client price: only the painter's pay. */
export function PainterJobScreen({ id }: { id: string }) {
  const t = useTranslations("painter.job");
  const locale = useLocale();
  const job = usePainterJob(id);

  if (job.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (job.isError) {
    const notFound = job.error instanceof ApiError && job.error.code === "not_found";
    return (
      <div role="alert" className="flex flex-col items-start gap-4 py-16">
        <p className="text-lg font-semibold">{t(notFound ? "notFound" : "error")}</p>
        <div className="flex gap-2">
          {!notFound && <Button onClick={() => job.refetch()}>{t("retry")}</Button>}
          <Button asChild variant="outline">
            <Link href="/pintor">{t("back")}</Link>
          </Button>
        </div>
      </div>
    );
  }

  const data = job.data;
  const mapUrl = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${data.addressLine1}, ${data.postalCode} ${data.locality}`)}`;
  return (
    <div className="flex flex-col gap-5">
      <Link href="/pintor" className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("back")}
      </Link>

      <header className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-[13px] font-semibold text-muted-foreground">{data.code}</p>
          <JobStatusBadge status={data.status} />
        </div>
        <h1 className="text-[26px] leading-tight font-extrabold tracking-tight">
          {data.addressLine1}, {data.locality}
        </h1>
        <p className="text-sm text-muted-foreground">
          {t("scheduledOn", { date: formatDate(locale, data.scheduledOn) })} · {formatArea(locale, Math.round(data.areaM2 * 100))}
        </p>
      </header>

      <Actions job={data} />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t("address.title")}>
          <p className="flex items-start gap-2 text-sm">
            <MapPin aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-primary" />
            <span>
              {data.addressLine1}
              <br />
              {data.postalCode} {data.locality} · {data.municipality}
            </span>
          </p>
          {data.accessNotes && (
            <p className="mt-2 text-sm">
              <span className="text-muted-foreground">{t("address.access")}: </span>
              {data.accessNotes}
            </p>
          )}
          <Button asChild variant="outline" size="sm" className="mt-3">
            <a href={mapUrl} target="_blank" rel="noopener noreferrer">
              <Navigation aria-hidden="true" />
              {t("address.map")}
            </a>
          </Button>
        </Card>

        <Card title={t("client.title")}>
          <p className="font-semibold">{data.clientName}</p>
          {data.clientPhone ? (
            <Button asChild size="sm" className="mt-3">
              <a href={`tel:${data.clientPhone}`}>
                <Phone aria-hidden="true" />
                {t("client.call", { phone: data.clientPhone })}
              </a>
            </Button>
          ) : (
            <p className="mt-1 text-sm text-muted-foreground">{t("client.noPhone")}</p>
          )}
        </Card>
      </div>

      <Card title={t("pay.title")}>
        <p className="text-2xl font-extrabold text-primary">{formatCents(locale, data.painterPayCents)}</p>
        <p className="mt-1 text-sm text-muted-foreground">{t(data.extras.length > 0 ? "pay.bodyExtras" : "pay.body")}</p>
      </Card>

      <Work job={data} />

      {(data.description || data.reviewerNote) && (
        <Card title={t("notes.title")}>
          {data.description && (
            <p className="text-sm">
              <span className="font-semibold">{t("notes.client")}: </span>
              {data.description}
            </p>
          )}
          {data.reviewerNote && (
            <p className="mt-2 text-sm">
              <span className="font-semibold">{t("notes.reviewer")}: </span>
              {data.reviewerNote}
            </p>
          )}
        </Card>
      )}

      {data.clientFiles.length > 0 && (
        <Card title={t("clientPhotos")}>
          <FileGrid files={data.clientFiles} />
        </Card>
      )}
    </div>
  );
}

function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[20px] border border-border bg-card p-4">
      <h2 className="mb-2 text-[13px] font-semibold text-muted-foreground uppercase">{title}</h2>
      {children}
    </section>
  );
}

/** Elements with measures, walls, paint and litres; material Pinta delivers; extras with the painter's pay (P1.3). */
function Work({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job.work");
  const locale = useLocale();
  const catalog = useCatalog();
  const paints = Object.fromEntries((catalog.data?.paints ?? []).map((p) => [p.id, p.name]));
  const rooms = Object.fromEntries((catalog.data?.roomTypes ?? []).map((r) => [r.id, r.name]));
  const surfaces = Object.fromEntries((catalog.data?.surfaceTypes ?? []).map((s) => [s.id, s.name]));
  const litres = (value: number) => t("litres", { litres: new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value) });
  const nameOf = (e: PainterElement) => e.name || (e.roomTypeId ? rooms[e.roomTypeId] : e.surfaceTypeId ? surfaces[e.surfaceTypeId] : null) || t("element");

  return (
    <>
      <Card title={t("title")}>
        <ul className="flex flex-col divide-y divide-border">
          {job.elements.map((e) => (
            <li key={e.id} className="flex flex-col gap-1 py-3 first:pt-0 last:pb-0">
              <div className="flex items-baseline justify-between gap-3">
                <p className="font-semibold">{nameOf(e)}</p>
                <p className="text-sm font-semibold whitespace-nowrap">{formatArea(locale, Math.round(e.areaM2 * 100))}</p>
              </div>
              <p className="text-sm text-muted-foreground">
                {[
                  e.length != null && e.width != null
                    ? [e.length, e.width, e.height].filter((v): v is number => v != null).map((v) => formatMetres(locale, v)).join(" × ")
                    : null,
                  e.kind === "room" && e.walls
                    ? t("walls", { walls: e.walls.map((on, i) => (on ? WALLS[i] : null)).filter(Boolean).join(" ") || "—" })
                    : null,
                  e.ceiling ? t("ceiling") : null,
                  e.damage ? t("damage") : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
              <p className="text-sm">
                {paints[e.paintId] ?? e.paintId} · {litres(e.litres)}
              </p>
            </li>
          ))}
        </ul>
      </Card>

      <Card title={t("material")}>
        <ul className="flex flex-col gap-1 text-sm">
          {job.paints.map((p) => (
            <li key={p.paintId} className="flex justify-between gap-3">
              <span>{paints[p.paintId] ?? p.paintId}</span>
              <span className="font-semibold">{litres(p.litres)}</span>
            </li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">{t("materialNote")}</p>
      </Card>

      {job.extras.length > 0 && (
        <Card title={t("extras")}>
          <ul className="flex flex-col gap-1 text-sm">
            {job.extras.map((x, i) => (
              <li key={i} className="flex justify-between gap-3">
                <span>{x.description}</span>
                <span className="font-semibold">{formatCents(locale, x.painterPayCents)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </>
  );
}

function FileGrid({ files }: { files: JobFile[] }) {
  const t = useTranslations("painter.job");
  return (
    <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
      {files.map((f, i) => (
        <li key={f.id} className="aspect-square overflow-hidden rounded-xl bg-muted">
          {f.kind === "video" ? (
            <video src={f.url} controls preload="metadata" className="size-full object-cover" aria-label={t("photo", { n: i + 1 })} />
          ) : (
            <a href={f.url} target="_blank" rel="noopener noreferrer">
              {/* eslint-disable-next-line @next/next/no-img-element -- signed storage URLs, not optimisable */}
              <img src={f.url} alt={t("photo", { n: i + 1 })} className="size-full object-cover" loading="lazy" />
            </a>
          )}
        </li>
      ))}
    </ul>
  );
}

/** What the painter can do in each state: start from the day (P2.1), result photos and finish (P2.2), then read-only. */
function Actions({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job");
  const locale = useLocale();
  const errorText = useErrorText();
  const start = useStartJob(job.id);

  switch (job.status) {
    case "scheduled":
      return (
        <section className="flex flex-col gap-2 rounded-[20px] border-2 border-accent bg-card p-4">
          <Button
            size="lg"
            disabled={!job.canStart || start.isPending}
            onClick={() => start.mutate(undefined, { onSuccess: () => toast.success(t("started")), onError: (e) => toast.error(errorText(e)) })}
          >
            {start.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Play aria-hidden="true" />}
            {t("start")}
          </Button>
          {!job.canStart && (
            <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
              <Clock aria-hidden="true" className="size-4" />
              {t("startFrom", { date: formatDate(locale, job.scheduledOn) })}
            </p>
          )}
        </section>
      );
    case "in_progress":
      return <ResultPhotos job={job} />;
    case "finished":
    case "closed":
      return (
        <section className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
          <p className="flex items-center gap-2 font-bold">
            <CircleCheck aria-hidden="true" className="size-5 text-primary" />
            {t(job.status === "finished" ? "pendingClose" : "closed")}
          </p>
          {job.resultFiles.length > 0 && <FileGrid files={job.resultFiles} />}
        </section>
      );
    default:
      return null;
  }
}

type LocalPhoto = { key: string; thumbnail: string | null; progress: number; fileId: string | null; failed: boolean };

/** Result photos straight to storage, then «Marcar como terminado» with at least `minResultPhotos` (P2.2). */
function ResultPhotos({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job.result");
  const errorText = useErrorText();
  const finish = useFinishJob(job.id);
  const input = useRef<HTMLInputElement>(null);
  const [photos, setPhotos] = useState<LocalPhoto[]>([]);
  const [confirming, setConfirming] = useState(false);
  const uploaded = photos.filter((p) => p.fileId);
  const uploading = photos.some((p) => !p.fileId && !p.failed);
  const enough = uploaded.length >= job.minResultPhotos;

  const update = (key: string, change: Partial<LocalPhoto>) => setPhotos((all) => all.map((p) => (p.key === key ? { ...p, ...change } : p)));

  const add = async (files: FileList | null) => {
    for (const file of Array.from(files ?? [])) {
      if (!RESULT_TYPES.includes(file.type)) {
        toast.error(t("format", { name: file.name }));
        continue;
      }

      const key = crypto.randomUUID();
      setPhotos((all) => [...all, { key, thumbnail: null, progress: 0, fileId: null, failed: false }]);
      try {
        const prepared = await prepareImage(file, file.type);
        update(key, { thumbnail: prepared.thumbnail });
        const fileId = await uploadResultPhoto(job.id, prepared.blob, prepared.contentType, (progress) => update(key, { progress }));
        update(key, { fileId, progress: 100 });
      } catch {
        update(key, { failed: true });
        toast.error(t("failed", { name: file.name }));
      }
    }
  };

  return (
    <section aria-labelledby="result-title" className="flex flex-col gap-3 rounded-[20px] border-2 border-accent bg-card p-4">
      <div>
        <h2 id="result-title" className="font-bold">
          {t("title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("body", { min: job.minResultPhotos })}</p>
      </div>

      <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
        {photos.map((p, i) => (
          <li key={p.key} className="relative aspect-square overflow-hidden rounded-xl bg-muted">
            {/* eslint-disable-next-line @next/next/no-img-element -- local data URL thumbnail */}
            {p.thumbnail && <img src={p.thumbnail} alt={t("photo", { n: i + 1 })} className="size-full object-cover" />}
            {!p.fileId && !p.failed && (
              <span className="absolute inset-x-0 bottom-0 bg-foreground/60 py-0.5 text-center text-xs text-background tabular-nums">{p.progress}%</span>
            )}
            {p.failed && <span className="absolute inset-0 grid place-items-center bg-destructive/70 text-xs font-semibold text-background">{t("error")}</span>}
            <button
              type="button"
              onClick={() => setPhotos((all) => all.filter((x) => x.key !== p.key))}
              className="absolute top-1 right-1 grid size-8 place-items-center rounded-full bg-card/90 text-foreground"
              aria-label={t("remove", { n: i + 1 })}
            >
              <Trash2 aria-hidden="true" className="size-4" />
            </button>
          </li>
        ))}
        <li>
          <button
            type="button"
            onClick={() => input.current?.click()}
            className="grid aspect-square w-full place-items-center rounded-xl border-2 border-dashed border-border text-sm font-semibold text-primary hover:border-primary focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <span className="flex flex-col items-center gap-1">
              <ImagePlus aria-hidden="true" className="size-6" />
              {t("add")}
            </span>
          </button>
          <input
            ref={input}
            type="file"
            accept={RESULT_TYPES.join(",")}
            multiple
            className="sr-only"
            tabIndex={-1}
            onChange={(event) => {
              void add(event.target.files);
              event.target.value = "";
            }}
          />
        </li>
      </ul>

      <p aria-live="polite" className="text-sm font-semibold tabular-nums">
        {t("count", { count: uploaded.length, min: job.minResultPhotos })}
      </p>
      <Button size="lg" disabled={!enough || uploading || finish.isPending} onClick={() => setConfirming(true)}>
        {finish.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("finish")}
      </Button>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("confirmTitle")}
        description={t("confirmBody")}
        confirmLabel={t("finish")}
        cancelLabel={t("cancel")}
        onConfirm={() =>
          finish.mutate(
            uploaded.map((p) => p.fileId!),
            { onSuccess: () => toast.success(t("finished")), onError: (e) => toast.error(errorText(e)) },
          )
        }
      />
    </section>
  );
}
