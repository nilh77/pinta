"use client";

import { ArrowLeft, CircleCheck, Clock, Loader2, MapPin, Navigation, Phone, Play, TriangleAlert } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FormDialog, TextAreaField } from "@/components/ui/form-dialog";
import { Link } from "@/i18n/navigation";
import type { components } from "@/lib/api/schema";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatArea, formatCents, formatDate, formatDateTime, formatMetres, formatPlace } from "@/lib/format";
import { useCatalog } from "@/lib/quote/use-quote-data";
import {
  uploadResultPhoto,
  useAddProgress,
  useFinishJob,
  useReportIncident,
  usePainterJob,
  useStartJob,
  type PainterElement,
  type PainterJobDetail,
} from "@/lib/painter/use-painter";
import { PhotoPicker, type PickedPhoto } from "./photo-picker";

type JobFile = components["schemas"]["JobFile"];
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
              {data.postalCode} {formatPlace(data.locality, data.municipality, " · ")}
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

export function Card({ title, children }: { title: string; children: ReactNode }) {
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

export function FileGrid({ files }: { files: JobFile[] }) {
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
          <Incident job={job} />
        </section>
      );
    case "in_progress":
      return (
        <>
          <ResultPhotos job={job} />
          <Progress job={job} />
          <Incident job={job} />
        </>
      );
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

/** P2: progress photos while the work runs; the client sees them on their job. */
function Progress({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job.progress");
  const errorText = useErrorText();
  const add = useAddProgress(job.id);
  const [photos, setPhotos] = useState<PickedPhoto[]>([]);
  const ready = photos.filter((p) => p.fileId);
  const uploading = photos.some((p) => !p.fileId && !p.failed);

  return (
    <section aria-labelledby="progress-title" className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
      <div>
        <h2 id="progress-title" className="font-bold">
          {t("title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("body")}</p>
      </div>
      {job.progressFiles.length > 0 && <FileGrid files={job.progressFiles} />}
      <PhotoPicker photos={photos} onChange={setPhotos} upload={(blob, type, progress) => uploadResultPhoto(job.id, blob, type, progress, "progress")} />
      <Button
        variant="outline"
        className="self-start"
        disabled={ready.length === 0 || uploading || add.isPending}
        onClick={() =>
          add.mutate(
            ready.map((p) => p.fileId!),
            { onSuccess: () => (setPhotos([]), toast.success(t("sent"))), onError: (e) => toast.error(errorText(e)) },
          )
        }
      >
        {add.isPending && <Loader2 aria-hidden="true" className="animate-spin" />}
        {t("send", { count: ready.length })}
      </Button>
    </section>
  );
}

/** P2: «Reportar incidencia» with text and photos, before or during the work; only Pinta sees it. */
function Incident({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job.incident");
  const locale = useLocale();
  const errorText = useErrorText();
  const report = useReportIncident(job.id);
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [photos, setPhotos] = useState<PickedPhoto[]>([]);
  const uploading = photos.some((p) => !p.fileId && !p.failed);

  return (
    <section className="flex flex-col gap-2 rounded-[20px] border border-border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-bold">{t("title")}</h2>
        <Button variant="outline" size="sm" className="text-destructive" onClick={() => setOpen(true)}>
          <TriangleAlert aria-hidden="true" />
          {t("open")}
        </Button>
      </div>
      {job.incidents.length > 0 && (
        <ul className="flex flex-col gap-1 text-sm">
          {job.incidents.map((i) => (
            <li key={i.id}>
              <span className="text-xs text-muted-foreground">{formatDateTime(locale, i.createdAt)} · </span>
              {i.text}
            </li>
          ))}
        </ul>
      )}
      <FormDialog
        open={open}
        onOpenChange={setOpen}
        title={t("dialogTitle", { code: job.code })}
        description={t("dialogBody")}
        submitLabel={t("submit")}
        cancelLabel={t("cancel")}
        pending={report.isPending}
        submitDisabled={!text.trim() || uploading}
        destructive
        onSubmit={() =>
          report.mutate(
            { text: text.trim(), fileIds: photos.filter((p) => p.fileId).map((p) => p.fileId!) },
            {
              onSuccess: () => {
                setOpen(false);
                setText("");
                setPhotos([]);
                toast.success(t("sent"));
              },
              onError: (e) => toast.error(errorText(e)),
            },
          )
        }
      >
        <div className="flex flex-col gap-3">
          <TextAreaField id={`incident-${job.id}`} label={t("text")} value={text} onChange={setText} max={2000} required />
          <PhotoPicker photos={photos} onChange={setPhotos} upload={(blob, type, progress) => uploadResultPhoto(job.id, blob, type, progress, "incident")} />
        </div>
      </FormDialog>
    </section>
  );
}

/** Result photos straight to storage, then «Marcar como terminado» with at least `minResultPhotos` (P2.2). */
function ResultPhotos({ job }: { job: PainterJobDetail }) {
  const t = useTranslations("painter.job.result");
  const errorText = useErrorText();
  const finish = useFinishJob(job.id);
  const [photos, setPhotos] = useState<PickedPhoto[]>([]);
  const [confirming, setConfirming] = useState(false);
  const uploaded = photos.filter((p) => p.fileId);
  const uploading = photos.some((p) => !p.fileId && !p.failed);
  const enough = uploaded.length >= job.minResultPhotos;

  return (
    <section aria-labelledby="result-title" className="flex flex-col gap-3 rounded-[20px] border-2 border-accent bg-card p-4">
      <div>
        <h2 id="result-title" className="font-bold">
          {t("title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("body", { min: job.minResultPhotos })}</p>
      </div>

      <PhotoPicker photos={photos} onChange={setPhotos} upload={(blob, type, progress) => uploadResultPhoto(job.id, blob, type, progress)} />

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
