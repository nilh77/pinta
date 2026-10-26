"use client";

import { ImagePlus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRef } from "react";
import { toast } from "sonner";
import { prepareImage } from "@/lib/quote/uploads";

const PHOTO_TYPES = ["image/jpeg", "image/png", "image/webp"];

/** A photo being uploaded (no `fileId` yet), uploaded, or failed. */
export type PickedPhoto = { key: string; thumbnail: string | null; progress: number; fileId: string | null; failed: boolean };

/**
 * Photos taken on site (result in P2, visit in P3): resized on the device and sent straight to storage with `upload`.
 * The parent keeps the list, so it can persist it or require a minimum.
 */
export function PhotoPicker({
  photos,
  onChange,
  upload,
}: {
  photos: PickedPhoto[];
  onChange: (update: (photos: PickedPhoto[]) => PickedPhoto[]) => void;
  upload: (blob: Blob, contentType: string, onProgress: (percent: number) => void) => Promise<string>;
}) {
  const t = useTranslations("painter.photos");
  const input = useRef<HTMLInputElement>(null);
  const update = (key: string, change: Partial<PickedPhoto>) => onChange((all) => all.map((p) => (p.key === key ? { ...p, ...change } : p)));

  const add = async (files: FileList | null) => {
    for (const file of Array.from(files ?? [])) {
      if (!PHOTO_TYPES.includes(file.type)) {
        toast.error(t("format", { name: file.name }));
        continue;
      }

      const key = crypto.randomUUID();
      onChange((all) => [...all, { key, thumbnail: null, progress: 0, fileId: null, failed: false }]);
      try {
        const prepared = await prepareImage(file, file.type);
        update(key, { thumbnail: prepared.thumbnail });
        const fileId = await upload(prepared.blob, prepared.contentType, (progress) => update(key, { progress }));
        update(key, { fileId, progress: 100 });
      } catch {
        update(key, { failed: true });
        toast.error(t("failed", { name: file.name }));
      }
    }
  };

  return (
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
            onClick={() => onChange((all) => all.filter((x) => x.key !== p.key))}
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
          accept={PHOTO_TYPES.join(",")}
          multiple
          hidden
          onChange={(event) => {
            void add(event.target.files);
            event.target.value = "";
          }}
        />
      </li>
    </ul>
  );
}
