"use client";

import { ArrowLeft, CircleCheck, Loader2, MapPin, Navigation, Pencil, Phone, Plus, Send, Trash2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { JobStatusBadge } from "@/components/jobs/job-status-badge";
import { RoomEditor } from "@/components/quote/room-editor";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatArea, formatDateTime } from "@/lib/format";
import { uploadVisitPhoto, usePainterVisit, useReportVisit } from "@/lib/painter/use-painter";
import {
  firstInvalid,
  loadVisitDraft,
  saveVisitDraft,
  startVisitDraft,
  toReport,
  type VisitDetail,
  type VisitDraft,
} from "@/lib/painter/visit-draft";
import { addRoom, displayNames, elementAreaOf, removeElement, saveElement, validateElement, type DraftElement } from "@/lib/quote/draft";
import { useCatalog } from "@/lib/quote/use-quote-data";
import { Card, FileGrid } from "./painter-job-screen";
import { PhotoPicker, type PickedPhoto } from "./photo-picker";

const fieldClass =
  "h-11 min-w-0 rounded-xl border-[1.5px] border-input bg-card px-3 text-base outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

/** P3 · Visita técnica (board PintorVisita) [P3.1]: the real measures, extras, photos and a note; never a price. */
export function PainterVisitScreen({ id }: { id: string }) {
  const t = useTranslations("painter.visit");
  const locale = useLocale();
  const visit = usePainterVisit(id);

  if (visit.isPending) {
    return (
      <p role="status" className="flex items-center gap-2 py-16 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  if (visit.isError) {
    const notFound = visit.error instanceof ApiError && visit.error.code === "not_found";
    return (
      <div role="alert" className="flex flex-col items-start gap-4 py-16">
        <p className="text-lg font-semibold">{t(notFound ? "notFound" : "error")}</p>
        <div className="flex gap-2">
          {!notFound && <Button onClick={() => visit.refetch()}>{t("retry")}</Button>}
          <Button asChild variant="outline">
            <Link href="/pintor">{t("back")}</Link>
          </Button>
        </div>
      </div>
    );
  }

  const data = visit.data;
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
        <p className="text-sm text-muted-foreground">{t("at", { date: formatDateTime(locale, data.at) })}</p>
      </header>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t("address")}>
          <p className="flex items-start gap-2 text-sm">
            <MapPin aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-primary" />
            <span>
              {data.addressLine1}
              <br />
              {data.postalCode} {data.locality} · {data.municipality}
            </span>
          </p>
          {data.accessNotes && <p className="mt-2 text-sm">{t("access", { notes: data.accessNotes })}</p>}
          <Button asChild variant="outline" size="sm" className="mt-3">
            <a href={mapUrl} target="_blank" rel="noopener noreferrer">
              <Navigation aria-hidden="true" />
              {t("map")}
            </a>
          </Button>
        </Card>
        <Card title={t("client")}>
          <p className="font-semibold">{data.clientName}</p>
          {data.clientPhone && (
            <Button asChild size="sm" className="mt-3">
              <a href={`tel:${data.clientPhone}`}>
                <Phone aria-hidden="true" />
                {t("call", { phone: data.clientPhone })}
              </a>
            </Button>
          )}
        </Card>
      </div>

      <Declared visit={data} />
      {data.reportedAt ? <Sent visit={data} /> : <Report visit={data} />}
    </div>
  );
}

/** What the client described in the request, to compare on site. */
function Declared({ visit }: { visit: VisitDetail }) {
  const t = useTranslations("painter.visit");
  const locale = useLocale();
  const catalog = useCatalog();
  const rooms = Object.fromEntries((catalog.data?.roomTypes ?? []).map((r) => [r.id, r.name]));
  const surfaces = Object.fromEntries((catalog.data?.surfaceTypes ?? []).map((s) => [s.id, s.name]));
  return (
    <Card title={t("declared")}>
      {visit.description && <p className="mb-3 text-sm">{visit.description}</p>}
      <ul className="flex flex-col gap-1 text-sm">
        {visit.elements.map((e) => (
          <li key={e.id} className="flex justify-between gap-3">
            <span>{e.name || (e.roomTypeId ? rooms[e.roomTypeId] : e.surfaceTypeId ? surfaces[e.surfaceTypeId] : null) || e.kind}</span>
            <span className="font-semibold whitespace-nowrap">{formatArea(locale, Math.round(e.areaM2 * 100))}</span>
          </li>
        ))}
      </ul>
      {visit.clientFiles.length > 0 && (
        <div className="mt-3">
          <FileGrid files={visit.clientFiles} />
        </div>
      )}
    </Card>
  );
}

function Sent({ visit }: { visit: VisitDetail }) {
  const t = useTranslations("painter.visit");
  const locale = useLocale();
  return (
    <section className="flex flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
      <p className="flex items-center gap-2 font-bold">
        <CircleCheck aria-hidden="true" className="size-5 text-primary" />
        {t("sent", { date: formatDateTime(locale, visit.reportedAt!) })}
      </p>
      {visit.notes && <p className="text-sm">{visit.notes}</p>}
      {visit.visitFiles.length > 0 && <FileGrid files={visit.visitFiles} />}
    </section>
  );
}

/** The report: elements corrected with the client's room editor, extras, photos and a note, kept on the device until sent. */
function Report({ visit }: { visit: VisitDetail }) {
  const t = useTranslations("painter.visit.report");
  const tQuote = useTranslations("quote");
  const locale = useLocale();
  const router = useRouter();
  const errorText = useErrorText();
  const catalog = useCatalog();
  const report = useReportVisit(visit.id);
  const [state, setState] = useState<VisitDraft>(() => loadVisitDraft(visit.id) ?? startVisitDraft(visit, locale));
  const [photos, setPhotos] = useState<PickedPhoto[]>(() =>
    state.photos.map((p) => ({ key: p.id, thumbnail: p.thumbnail, progress: 100, fileId: p.id, failed: false })),
  );
  const [editing, setEditing] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const info = catalog.info;

  useEffect(() => {
    saveVisitDraft(visit.id, { ...state, photos: photos.filter((p) => p.fileId).map((p) => ({ id: p.fileId!, thumbnail: p.thumbnail })) });
  }, [visit.id, state, photos]);

  if (!catalog.data || !info) {
    return (
      <p role="status" className="flex items-center gap-2 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </p>
    );
  }

  const names = displayNames(
    state.draft.elements,
    Object.fromEntries(catalog.data.roomTypes.map((r) => [r.id, r.name])),
    tQuote("schema.surfaceName"),
  );
  const setElement = (element: DraftElement) =>
    setState((s) => ({ ...s, draft: { ...s.draft, elements: s.draft.elements.map((e) => (e.id === element.id ? element : e)) } }));
  const uploading = photos.some((p) => !p.fileId && !p.failed);

  const send = () => {
    const invalid = firstInvalid(state.draft);
    if (invalid >= 0) {
      setEditing(state.draft.elements[invalid].id);
      setProblem(t("fixElement", { name: names[invalid] }));
      return;
    }

    const body = toReport({ ...state, photos: photos.filter((p) => p.fileId).map((p) => ({ id: p.fileId!, thumbnail: null })) }, info);
    if (!body) {
      setProblem(t("fixExtras"));
      return;
    }

    setProblem(null);
    report.mutate(body, {
      onSuccess: () => {
        saveVisitDraft(visit.id, null);
        toast.success(t("done"));
        router.push("/pintor");
      },
      onError: (error) => setProblem(error instanceof ApiError && error.problem?.errors ? Object.values(error.problem.errors).flat().join(" ") : errorText(error)),
    });
  };

  return (
    <section aria-labelledby="report-title" className="flex flex-col gap-4 rounded-[20px] border-2 border-accent bg-card p-4">
      <div>
        <h2 id="report-title" className="font-bold">
          {t("title")}
        </h2>
        <p className="text-sm text-muted-foreground">{t("body")}</p>
      </div>

      <ul className="flex flex-col gap-2">
        {state.draft.elements.map((element, index) => {
          const area = elementAreaOf(element, info);
          const open = editing === element.id;
          const invalid = !element.described || Object.keys(validateElement(element)).length > 0;
          return (
            <li key={element.id} className="rounded-2xl border border-border p-3">
              <div className="flex items-center justify-between gap-2">
                <p className="font-semibold">{names[index]}</p>
                <span className="flex items-center gap-1">
                  <span className={invalid ? "text-sm font-semibold text-destructive" : "text-sm font-semibold"}>
                    {area ? formatArea(locale, Math.round(area.total * 100)) : "—"}
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-expanded={open}
                    onClick={() => {
                      if (open) setState((s) => ({ ...s, draft: saveElement(s.draft, element) }));
                      setEditing(open ? null : element.id);
                    }}
                  >
                    <Pencil aria-hidden="true" />
                    {open ? t("doneEditing") : t("correct")}
                  </Button>
                  {state.draft.elements.length > 1 && (
                    <button
                      type="button"
                      aria-label={t("remove", { name: names[index] })}
                      onClick={() => setState((s) => ({ ...s, draft: removeElement(s.draft, element.id).draft }))}
                      className="grid size-10 place-items-center rounded-lg text-muted-foreground hover:text-destructive"
                    >
                      <Trash2 aria-hidden="true" className="size-4" />
                    </button>
                  )}
                </span>
              </div>
              {open && (
                <div className="mt-3 border-t border-border pt-3">
                  <RoomEditor element={element} onChange={setElement} errors={validateElement(element)} catalog={catalog.data} info={info} />
                </div>
              )}
            </li>
          );
        })}
      </ul>
      <label className="flex flex-col gap-1 text-[13px] font-semibold">
        {t("addRoom")}
        <select
          className={fieldClass}
          value=""
          onChange={(e) => {
            if (!e.target.value) return;
            const added = addRoom(state.draft, e.target.value, info);
            const room = added.elements.at(-1);
            // A room added on site is part of the report as soon as it exists; its measures are checked on send.
            const next = room ? saveElement(added, room) : added;
            setState((s) => ({ ...s, draft: next }));
            setEditing(room?.id ?? null);
          }}
        >
          <option value="">{t("chooseRoom")}</option>
          {catalog.data.roomTypes.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </label>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-bold">{t("extras")}</legend>
        <p className="text-xs text-muted-foreground">{t("extrasHint")}</p>
        {state.extras.map((x, i) => (
          <div key={x.id} className="grid grid-cols-[minmax(0,1fr)_7rem_auto] items-end gap-2">
            <label className="flex flex-col gap-1 text-[13px] font-semibold">
              {t("extraDescription")}
              <input
                className={fieldClass}
                maxLength={200}
                value={x.description}
                onChange={(e) => setState((s) => ({ ...s, extras: s.extras.map((y, j) => (j === i ? { ...y, description: e.target.value } : y)) }))}
              />
            </label>
            <label className="flex flex-col gap-1 text-[13px] font-semibold">
              {t("extraPay")}
              <input
                className={fieldClass}
                inputMode="decimal"
                value={x.pay}
                onChange={(e) => setState((s) => ({ ...s, extras: s.extras.map((y, j) => (j === i ? { ...y, pay: e.target.value } : y)) }))}
              />
            </label>
            <button
              type="button"
              aria-label={t("removeExtra", { n: i + 1 })}
              onClick={() => setState((s) => ({ ...s, extras: s.extras.filter((_, j) => j !== i) }))}
              className="grid size-11 place-items-center rounded-lg text-muted-foreground hover:text-destructive"
            >
              <Trash2 aria-hidden="true" className="size-4" />
            </button>
          </div>
        ))}
        <Button
          variant="outline"
          size="sm"
          className="self-start"
          onClick={() => setState((s) => ({ ...s, extras: [...s.extras, { id: crypto.randomUUID(), description: "", pay: "" }] }))}
        >
          <Plus aria-hidden="true" />
          {t("addExtra")}
        </Button>
      </fieldset>

      <div className="flex flex-col gap-2">
        <h3 className="text-sm font-bold">{t("photos")}</h3>
        <PhotoPicker photos={photos} onChange={setPhotos} upload={(blob, type, progress) => uploadVisitPhoto(visit.id, blob, type, progress)} />
      </div>

      <label className="flex flex-col gap-1 text-sm font-bold">
        {t("notes")}
        <textarea
          rows={3}
          maxLength={2000}
          value={state.notes}
          onChange={(e) => setState((s) => ({ ...s, notes: e.target.value }))}
          className="rounded-xl border-[1.5px] border-input bg-card px-3 py-2 text-base font-normal outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
        />
      </label>

      {problem && (
        <p role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 px-3.5 py-3 text-sm text-destructive">
          {problem}
        </p>
      )}
      <Button size="lg" disabled={report.isPending || uploading} onClick={send}>
        {report.isPending ? <Loader2 aria-hidden="true" className="animate-spin" /> : <Send aria-hidden="true" />}
        {t("send")}
      </Button>
      <p className="text-xs text-muted-foreground">{t("kept")}</p>
    </section>
  );
}
