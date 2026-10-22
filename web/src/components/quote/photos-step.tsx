"use client";

import { Camera, Check, Droplets, Loader2, Play, RotateCcw, Video, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useRef } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useRouter } from "@/i18n/navigation";
import { ApiError } from "@/lib/api/problem";
import { displayNames, newUuid, toEstimateRequest, type Draft, type DraftFile } from "@/lib/quote/draft";
import { MAX_DESCRIPTION, photoCoverage } from "@/lib/quote/request";
import { ACCEPT, classifyFile, deleteUpload, MAX_PHOTOS, MAX_VIDEOS, prepareImage, uploadFile, type UploadKind } from "@/lib/quote/uploads";
import { setDraft } from "@/lib/quote/use-quote-draft";
import { cn } from "@/lib/utils";
import { QuoteShell, type QuoteContext } from "./quote-shell";

export const GENERAL = "general";

/** Files whose upload failed, kept in memory so "retry" can send them again (never persisted). */
const failedBlobs = new Map<string, { blob: Blob; contentType: string; kind: UploadKind }>();

/** Screen 5 · Fotos y detalles (board Fotos) [C3.1, C3.2]. */
export function PhotosStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={3} back={{ href: "/cotizar/estimacion", label: t("photos.back") }}>
      {(context) => <PhotosForm {...context} />}
    </QuoteShell>
  );
}

const updateFile = (key: string, id: string, patch: Partial<DraftFile>) =>
  setDraft((d) =>
    d && {
      ...d,
      details: { ...d.details, files: { ...d.details.files, [key]: (d.details.files[key] ?? []).map((f) => (f.id === id ? { ...f, ...patch } : f)) } },
    },
  );

const removeFile = (key: string, id: string) =>
  setDraft((d) => d && { ...d, details: { ...d.details, files: { ...d.details.files, [key]: (d.details.files[key] ?? []).filter((f) => f.id !== id) } } });

function PhotosForm({ draft, catalog, info, update }: QuoteContext) {
  const t = useTranslations("quote.photos");
  const tRoot = useTranslations();
  const router = useRouter();
  const request = useMemo(() => (draft ? toEstimateRequest(draft, info) : null), [draft, info]);

  useEffect(() => {
    if (!draft) router.replace("/cotizar");
    else if (!request) router.replace("/cotizar/esquema");
  }, [draft, request, router]);
  if (!draft || !request) return null;

  const names = displayNames(draft.elements, Object.fromEntries(catalog.roomTypes.map((r) => [r.id, r.name])), tRoot("quote.schema.surfaceName"));
  const elements = draft.elements
    .map((element, index) => ({ element, name: names[index] }))
    .filter(({ element }) => request.elements.some((e) => e.clientKey === element.id));
  const uploading = Object.values(draft.details.files).flat().some((f) => f.status === "uploading");
  const { withPhotos, total } = photoCoverage(draft, info);

  async function upload(key: string, file: Blob, contentType: string, kind: UploadKind, localId: string) {
    try {
      let blob = file;
      let type = contentType;
      if (kind === "image" && file instanceof File) {
        const prepared = await prepareImage(file, contentType);
        blob = prepared.blob;
        type = prepared.contentType;
        updateFile(key, localId, { thumbnail: prepared.thumbnail });
      }

      const fileId = await uploadFile({
        draftId: draft!.details.draftId,
        elementKey: key === GENERAL ? null : key,
        kind,
        blob,
        contentType: type,
        onProgress: (progress) => updateFile(key, localId, { progress }),
      });
      failedBlobs.delete(localId);
      updateFile(key, localId, { id: fileId, status: "done", progress: 100 });
    } catch (error) {
      const code = error instanceof ApiError ? error.problem?.code : undefined;
      if (code === "media_limit_reached" || code === "file_too_large" || code === "unsupported_media_type") {
        removeFile(key, localId);
        toast.error(t(`errors.${code}`));
        return;
      }

      failedBlobs.set(localId, { blob: file, contentType, kind });
      updateFile(key, localId, { status: "error" });
    }
  }

  function addFiles(key: string, files: FileList | null) {
    const existing = draft!.details.files[key] ?? [];
    let photos = existing.filter((f) => f.kind === "image").length;
    let videos = existing.filter((f) => f.kind === "video").length;
    for (const file of Array.from(files ?? [])) {
      const result = classifyFile(file);
      if ("error" in result) {
        toast.error(tRoot(result.error as never, { name: file.name } as never));
        continue;
      }

      if (result.kind === "image" ? photos >= MAX_PHOTOS : videos >= MAX_VIDEOS) {
        toast.error(t(result.kind === "image" ? "errors.tooManyPhotos" : "errors.tooManyVideos", { max: result.kind === "image" ? MAX_PHOTOS : MAX_VIDEOS }));
        continue;
      }

      if (result.kind === "image") photos++;
      else videos++;
      const localId = newUuid();
      setDraft((d) =>
        d && {
          ...d,
          details: {
            ...d.details,
            files: {
              ...d.details.files,
              [key]: [...(d.details.files[key] ?? []), { id: localId, kind: result.kind, name: file.name, thumbnail: null, status: "uploading", progress: 0 }],
            },
          },
        },
      );
      void upload(key, file, result.contentType, result.kind, localId);
    }
  }

  function retry(key: string, file: DraftFile) {
    const failed = failedBlobs.get(file.id);
    if (!failed) return removeFile(key, file.id);
    updateFile(key, file.id, { status: "uploading", progress: 0 });
    void upload(key, failed.blob, failed.contentType, failed.kind, file.id);
  }

  async function remove(key: string, file: DraftFile) {
    if (file.status === "done") await deleteUpload(draft!.details.draftId, file.id).catch(() => undefined);
    failedBlobs.delete(file.id);
    removeFile(key, file.id);
  }

  const blockProps = { files: draft.details.files, onAdd: addFiles, onRetry: retry, onRemove: remove };
  return (
    <div className="flex flex-1 flex-col pb-44 lg:pb-12">
      <h1 className="text-[26px] leading-tight font-bold tracking-tight">{t("title")}</h1>
      <p className="mt-1.5 text-sm text-muted-foreground">{t("subtitle")}</p>

      <div className="mt-4 flex flex-col gap-2.5">
        {elements.map(({ element, name }) => (
          <MediaBlock key={element.id} fileKey={element.id} title={name} damage={element.damage} {...blockProps} />
        ))}
        <MediaBlock fileKey={GENERAL} title={t("general")} subtitle={t("generalHint")} {...blockProps} />
      </div>

      <DescriptionField value={draft.details.description} onChange={(description) => update((d) => ({ ...d, details: { ...d.details, description } }))} />

      <div className="fixed inset-x-0 bottom-0 z-30 rounded-t-[20px] border-t border-border bg-card px-5 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))] lg:static lg:mt-6 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0">
        <div className="mx-auto flex max-w-2xl flex-col gap-2">
          <p className="text-center text-xs text-muted-foreground">
            {withPhotos === 0 ? t("noPhotosHint") : t("coverage", { withPhotos, total })}
          </p>
          <Button
            size="lg"
            className="w-full"
            disabled={uploading}
            onClick={() => {
              update((d) => ({ ...d, step: "address" }));
              router.push("/cotizar/direccion");
            }}
          >
            {uploading && <Loader2 aria-hidden="true" className="animate-spin motion-reduce:animate-none" />}
            {uploading ? t("waitingUploads") : t("continue")}
          </Button>
        </div>
      </div>
    </div>
  );
}

type MediaBlockProps = {
  fileKey: string;
  title: string;
  subtitle?: string;
  damage?: boolean;
  files: Draft["details"]["files"];
  onAdd: (key: string, files: FileList | null) => void;
  onRetry: (key: string, file: DraftFile) => void;
  onRemove: (key: string, file: DraftFile) => void;
};

/** One element's photos and videos (board Fotos): thumbnails, progress, retry and the Foto / Vídeo buttons. */
function MediaBlock({ fileKey, title, subtitle, damage, files, onAdd, onRetry, onRemove }: MediaBlockProps) {
  const t = useTranslations("quote.photos");
  const photoInput = useRef<HTMLInputElement>(null);
  const videoInput = useRef<HTMLInputElement>(null);
  const id = useId();
  const list = files[fileKey] ?? [];
  const photos = list.filter((f) => f.kind === "image").length;
  const videos = list.filter((f) => f.kind === "video").length;
  const done = list.some((f) => f.status === "done");

  return (
    <section aria-labelledby={`${id}-title`} className={cn("rounded-2xl border bg-card p-3.5", list.length === 0 ? "border-dashed border-[#7c918b]" : "border-border")}>
      <div className="flex items-center justify-between gap-2">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-1.5">
            <h2 id={`${id}-title`} className="text-[15px] font-semibold">
              {title}
            </h2>
            {damage && <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-ink">{t("damageBadge")}</span>}
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {list.length === 0 ? (subtitle ?? t("none")) : t("counter", { photos, maxPhotos: MAX_PHOTOS, videos, maxVideos: MAX_VIDEOS })}
          </p>
        </div>
        {done && (
          <span aria-hidden="true" className="flex size-6 items-center justify-center rounded-full bg-primary text-primary-foreground">
            <Check className="size-3.5" strokeWidth={3} />
          </span>
        )}
      </div>

      {damage && (
        <p className="mt-2.5 flex items-start gap-2 rounded-xl bg-[#fffbeb] px-3 py-2.5 text-xs text-[#4a3700]">
          <Droplets aria-hidden="true" className="mt-px size-4 shrink-0 text-accent-ink" />
          {t("damageHint")}
        </p>
      )}

      {list.length > 0 && (
        <ul className="mt-3 grid grid-cols-5 gap-1.5">
          {list.map((file) => (
            <li key={file.id} className="relative aspect-square overflow-hidden rounded-[10px] bg-[#dce8e4]">
              {file.kind === "video" ? (
                <span className="flex size-full items-center justify-center bg-foreground text-white">
                  <Play aria-hidden="true" className="size-5 fill-current" />
                </span>
              ) : file.thumbnail ? (
                // eslint-disable-next-line @next/next/no-img-element -- local data URL thumbnail
                <img src={file.thumbnail} alt="" className="size-full object-cover" />
              ) : (
                <span className="flex size-full items-center justify-center text-muted-foreground">
                  <Camera aria-hidden="true" className="size-5" />
                </span>
              )}
              <span className="sr-only">{t(file.kind === "video" ? "videoLabel" : "photoLabel", { name: file.name })}</span>
              {file.status === "uploading" && (
                <span className="absolute inset-x-1 bottom-1 h-1.5 overflow-hidden rounded-full bg-white/70" role="progressbar" aria-valuenow={file.progress} aria-valuemin={0} aria-valuemax={100} aria-label={t("uploading", { name: file.name })}>
                  <span className="block h-full bg-primary transition-[width] motion-reduce:transition-none" style={{ width: `${file.progress}%` }} />
                </span>
              )}
              {file.status === "error" && (
                <button type="button" onClick={() => onRetry(fileKey, file)} aria-label={t("retry", { name: file.name })} className="absolute inset-0 flex items-center justify-center bg-destructive/80 text-white focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none">
                  <RotateCcw aria-hidden="true" className="size-5" />
                </button>
              )}
              {file.status !== "uploading" && (
                <button
                  type="button"
                  onClick={() => onRemove(fileKey, file)}
                  aria-label={t("remove", { name: file.name })}
                  className="absolute top-0.5 right-0.5 flex size-6 items-center justify-center rounded-full bg-foreground/70 text-white focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none"
                >
                  <X aria-hidden="true" className="size-3.5" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      <div className="mt-3 grid grid-cols-2 gap-2">
        <Button type="button" variant="outline" disabled={photos >= MAX_PHOTOS} onClick={() => photoInput.current?.click()}>
          <Camera aria-hidden="true" />
          {t("addPhoto")}
        </Button>
        <Button type="button" variant="outline" disabled={videos >= MAX_VIDEOS} onClick={() => videoInput.current?.click()}>
          <Video aria-hidden="true" />
          {t("addVideo")}
        </Button>
      </div>
      {/* No `capture`: phones offer camera or gallery (docs/screens.md, screen 5). */}
      <input
        ref={photoInput}
        type="file"
        accept={ACCEPT.split(",").filter((a) => a.startsWith("image") || a.startsWith(".hei")).join(",")}
        multiple
        hidden
        onChange={(e) => {
          onAdd(fileKey, e.target.files);
          e.target.value = "";
        }}
      />
      <input
        ref={videoInput}
        type="file"
        accept={ACCEPT.split(",").filter((a) => a.startsWith("video") || a === ".mov").join(",")}
        hidden
        onChange={(e) => {
          onAdd(fileKey, e.target.files);
          e.target.value = "";
        }}
      />
    </section>
  );
}

function DescriptionField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const t = useTranslations("quote.photos");
  const id = useId();
  return (
    <div className="mt-5 flex flex-col gap-2">
      <label htmlFor={id} className="text-[13px] font-semibold">
        {t("description")}
      </label>
      <textarea
        id={id}
        rows={4}
        maxLength={MAX_DESCRIPTION}
        value={value}
        placeholder={t("descriptionPlaceholder")}
        onChange={(e) => onChange(e.target.value)}
        aria-describedby={`${id}-count`}
        className="w-full resize-none rounded-[14px] border-[1.5px] border-input bg-card px-3.5 py-3 text-[15px] leading-relaxed outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
      />
      <p id={`${id}-count`} className="text-right text-xs text-muted-foreground tabular-nums">
        {value.length}/{MAX_DESCRIPTION}
      </p>
    </div>
  );
}
