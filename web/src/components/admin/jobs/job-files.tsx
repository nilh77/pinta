"use client";

import { ChevronLeft, ChevronRight, Film, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { Dialog } from "radix-ui";
import { useState } from "react";
import type { Schemas } from "@/lib/jobs/api";

type JobFile = Schemas["JobFile"];

/** Photos and videos with a full-screen viewer (A2.2). URLs are signed for 15 minutes; the page refetches them. */
export function JobFiles({ files, emptyText }: { files: JobFile[]; emptyText?: string }) {
  const t = useTranslations("admin.job.files");
  const [open, setOpen] = useState<number | null>(null);
  if (files.length === 0) return <p className="text-sm text-muted-foreground">{emptyText ?? t("empty")}</p>;
  const current = open === null ? null : files[open];

  return (
    <>
      <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4 lg:grid-cols-6">
        {files.map((file, i) => (
          <li key={file.id}>
            <button
              type="button"
              onClick={() => setOpen(i)}
              aria-label={t("open", { index: i + 1 })}
              className="relative flex aspect-square w-full items-center justify-center overflow-hidden rounded-xl border border-border bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              {file.kind === "video" ? (
                <Film aria-hidden="true" className="size-7 text-primary" />
              ) : (
                // eslint-disable-next-line @next/next/no-img-element -- signed storage URLs, not optimisable by next/image
                <img src={file.url} alt="" loading="lazy" className="size-full object-cover" />
              )}
            </button>
          </li>
        ))}
      </ul>
      <Dialog.Root open={current != null} onOpenChange={(next) => !next && setOpen(null)}>
        <Dialog.Portal>
          <Dialog.Overlay className="fixed inset-0 z-40 bg-black/90" />
          <Dialog.Content
            aria-describedby={undefined}
            className="fixed inset-0 z-50 flex items-center justify-center p-4"
            onKeyDown={(e) => {
              if (open === null) return;
              if (e.key === "ArrowRight") setOpen((open + 1) % files.length);
              if (e.key === "ArrowLeft") setOpen((open - 1 + files.length) % files.length);
            }}
          >
            <Dialog.Title className="sr-only">{t("viewer", { index: (open ?? 0) + 1, total: files.length })}</Dialog.Title>
            {current &&
              (current.kind === "video" ? (
                <video src={current.url} controls className="max-h-full max-w-full rounded-xl" />
              ) : (
                // eslint-disable-next-line @next/next/no-img-element -- signed storage URLs
                <img src={current.url} alt={t("viewer", { index: (open ?? 0) + 1, total: files.length })} className="max-h-full max-w-full rounded-xl object-contain" />
              ))}
            <Dialog.Close aria-label={t("close")} className="absolute top-4 right-4 flex size-11 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25">
              <X aria-hidden="true" className="size-5" />
            </Dialog.Close>
            {files.length > 1 && open !== null && (
              <>
                <button
                  type="button"
                  aria-label={t("previous")}
                  onClick={() => setOpen((open - 1 + files.length) % files.length)}
                  className="absolute left-4 flex size-11 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
                >
                  <ChevronLeft aria-hidden="true" className="size-6" />
                </button>
                <button
                  type="button"
                  aria-label={t("next")}
                  onClick={() => setOpen((open + 1) % files.length)}
                  className="absolute right-4 flex size-11 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
                >
                  <ChevronRight aria-hidden="true" className="size-6" />
                </button>
              </>
            )}
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog.Root>
    </>
  );
}
