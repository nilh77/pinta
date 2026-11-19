"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Download, FileText, Paperclip } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useId, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { api } from "@/lib/api/client";
import { useErrorText } from "@/lib/api/use-error-text";
import { formatDateTime } from "@/lib/format";
import { orThrow } from "@/lib/jobs/api";
import { MAX_IMAGE_BYTES, postToStorage } from "@/lib/quote/uploads";
import { cn } from "@/lib/utils";

const TYPES = ["application/pdf", "image/jpeg", "image/png", "image/webp"];
const MAX_FILES = 4;

function fileType(file: File) {
  if (file.type) return file.type.toLowerCase();
  const extension = file.name.split(".").at(-1)?.toLowerCase();
  switch (extension) {
    case "pdf": return "application/pdf";
    case "jpg":
    case "jpeg": return "image/jpeg";
    case "png": return "image/png";
    case "webp": return "image/webp";
    default: return "";
  }
}

function validSelection(files: File[]) {
  if (!files.length || files.length > MAX_FILES) return false;
  return files.every((file) => {
    const type = fileType(file);
    return TYPES.includes(type) && (type !== "application/pdf" || files.length === 1)
      && file.size > 0 && file.size <= MAX_IMAGE_BYTES;
  });
}

export function JobProtocol({ id, audience, finished }: {
  id: string;
  audience: "admin" | "painter";
  finished: boolean;
}) {
  const t = useTranslations("jobProtocol");
  const common = useTranslations("common");
  const errorText = useErrorText();
  const locale = useLocale();
  const headingId = useId();
  const fileHelpId = useId();
  const fileInput = useRef<HTMLInputElement>(null);
  const [selection, setSelection] = useState<File[]>([]);
  const [confirming, setConfirming] = useState(false);
  const queryClient = useQueryClient();
  const queryKey = ["jobProtocol", audience, id, finished] as const;
  const params = { path: { id } };
  const statusPath = audience === "admin" ? "/api/admin/jobs/{id}/protocol" : "/api/painter/jobs/{id}/protocol";
  const protocol = useQuery({
    queryKey,
    queryFn: async () => {
      const result = await api.GET(statusPath, { params });
      return result.response.status === 404 ? null : orThrow(result);
    },
    refetchOnMount: "always",
    refetchInterval: (query) => {
      if (query.state.data?.status === "pending" && audience === "admin") return 30_000;
      return query.state.data?.status === "uploaded" ? 300_000 : false;
    },
  });
  const canUpload = validSelection(selection);

  const receipt = useMutation({
    mutationFn: async () => {
      if (audience === "admin") {
        return orThrow(await api.POST("/api/admin/jobs/{id}/protocol/receive-in-person", { params }));
      }

      const fileIds: string[] = [];
      for (const file of selection) {
        const upload = orThrow(await api.POST("/api/painter/jobs/{id}/protocol/upload-url", {
          params,
          body: { contentType: fileType(file), sizeBytes: file.size },
        }));
        await postToStorage(upload, file, () => {});
        fileIds.push(upload.fileId);
      }
      return orThrow(await api.POST("/api/painter/jobs/{id}/protocol/submit", { params, body: { fileIds } }));
    },
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result);
      setSelection([]);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["jobProtocol", audience, id] }),
  });

  if (protocol.data === null) return null;

  const data = protocol.data;
  return (
    <section aria-labelledby={headingId} className="flex min-w-0 flex-col gap-3 rounded-[20px] border border-border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 id={headingId} className="flex items-center gap-2 font-bold">
          <FileText aria-hidden="true" className="size-4 shrink-0 text-primary" />
          {t("title")}
        </h3>
        {data && (
          <span role="status" className={cn("rounded-full px-2.5 py-1 text-xs font-semibold",
            data.status === "pending" ? "bg-accent-soft text-accent-ink" : "bg-primary-soft text-primary")}>
            {t(`status.${data.status}`)}
          </span>
        )}
      </div>
      {protocol.isPending && <p role="status" className="text-sm text-muted-foreground">{t("loading")}</p>}
      {protocol.isError && (
        <div className="flex flex-wrap items-center gap-2">
          <p role="alert" className="text-sm text-destructive">{errorText(protocol.error)}</p>
          <Button type="button" variant="outline" size="sm" onClick={() => protocol.refetch()}>{t("retry")}</Button>
        </div>
      )}
      {data && (
        <>
          {audience === "painter" && data.status === "pending" && (
            <p className="text-sm text-muted-foreground">{t("signing")}</p>
          )}
          {data.receivedAt && data.receivedBy && (
            <p className="text-sm text-muted-foreground">{t("recorded", {
              name: data.receivedBy,
              date: formatDateTime(locale, data.receivedAt),
            })}</p>
          )}
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline" size="sm">
              <a href={`/api/${audience}/jobs/${encodeURIComponent(id)}/protocol/pdf`} download>
                <Download aria-hidden="true" />
                {t("download")}
              </a>
            </Button>
            {data.files.map((file, index) => (
              <Button key={file.id} asChild variant="outline" size="sm">
                <a href={file.url} target="_blank" rel="noreferrer">
                  {file.kind === "document" ? t("signedPdf") : t("signedPhoto", { number: index + 1 })}
                </a>
              </Button>
            ))}
          </div>
          {data.canReceive && (audience === "painter" ? (
            <form className="flex flex-col gap-2 border-t border-border pt-3" onSubmit={(event) => {
              event.preventDefault();
              if (canUpload && !receipt.isPending) receipt.mutate();
            }}>
              <p className="text-sm font-semibold">{t("choose")}</p>
              <p id={fileHelpId} className="text-sm text-muted-foreground">{t("fileHelp")}</p>
              <Button type="button" variant="outline" size="sm" className="self-start" disabled={receipt.isPending}
                aria-describedby={fileHelpId} onClick={() => fileInput.current?.click()}>
                <Paperclip aria-hidden="true" />
                {common("chooseFiles")}
              </Button>
              <input ref={fileInput} type="file" multiple accept={TYPES.join(",")} hidden
                onChange={(event) => {
                  receipt.reset();
                  setSelection(Array.from(event.currentTarget.files ?? []));
                  event.currentTarget.value = "";
                }}
              />
              {selection.length > 0 && (
                <ul className="list-inside list-disc break-all text-sm text-muted-foreground">
                  {selection.map((file, index) => <li key={index}>{file.name}</li>)}
                </ul>
              )}
              {selection.length > 0 && !canUpload && <p role="alert" className="text-sm text-destructive">{t("invalidFiles")}</p>}
              <Button type="submit" size="sm" className="self-start" disabled={!canUpload} pending={receipt.isPending}>{t("upload")}</Button>
            </form>
          ) : (
            <>
              <Button type="button" size="sm" className="self-start" pending={receipt.isPending} onClick={() => {
                receipt.reset();
                setConfirming(true);
              }}>
                <Check aria-hidden="true" />
                {t("markReceived")}
              </Button>
              <ConfirmDialog
                open={confirming}
                onOpenChange={setConfirming}
                title={t("markReceived")}
                description={t("confirmReceipt")}
                confirmLabel={t("confirm")}
                cancelLabel={t("cancel")}
                onConfirm={() => receipt.mutate()}
              />
            </>
          ))}
          {receipt.isError && <p role="alert" className="text-sm text-destructive">{errorText(receipt.error)}</p>}
        </>
      )}
    </section>
  );
}
