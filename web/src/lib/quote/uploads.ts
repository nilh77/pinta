import { api } from "@/lib/api/client";
import { ApiError, type Problem } from "@/lib/api/problem";

/** Types and limits of screen 5 (docs/screens.md); the API and storage check them again. */
const IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];
const VIDEO_TYPES = ["video/mp4", "video/quicktime"];
export const ACCEPT = [...IMAGE_TYPES, ...VIDEO_TYPES, ".heic", ".heif", ".mov"].join(",");
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;
export const MAX_PHOTOS = 10;
export const MAX_VIDEOS = 2;
const TARGET_PIXELS = 2_000_000;

export type UploadKind = "image" | "video";

/** Browsers often leave HEIC and MOV without a type: fall back to the extension. */
function contentTypeOf(file: Pick<File, "type" | "name">): string {
  if (file.type) return file.type.toLowerCase();
  const extension = file.name.split(".").pop()?.toLowerCase();
  return { heic: "image/heic", heif: "image/heif", mov: "video/quicktime", mp4: "video/mp4", jpg: "image/jpeg", jpeg: "image/jpeg" }[extension ?? ""] ?? "";
}

/** The kind and type of a picked file, or the translation key of why it cannot be uploaded. */
export function classifyFile(file: Pick<File, "type" | "name" | "size">): { kind: UploadKind; contentType: string } | { error: string } {
  const contentType = contentTypeOf(file);
  const kind: UploadKind | null = IMAGE_TYPES.includes(contentType) ? "image" : VIDEO_TYPES.includes(contentType) ? "video" : null;
  if (!kind) return { error: "quote.photos.errors.format" };
  // Images are compressed before uploading; only videos (and HEIC, uploaded as is) must already fit.
  const compressible = kind === "image" && !contentType.includes("hei");
  if (!compressible && file.size > (kind === "video" ? MAX_VIDEO_BYTES : MAX_IMAGE_BYTES)) return { error: `quote.photos.errors.${kind}TooLarge` };
  return { kind, contentType };
}

/** JPEG, PNG and WebP are scaled to ~2 MP on the device (C3.1); HEIC most browsers cannot decode, so it goes as is. */
export async function prepareImage(file: File, contentType: string): Promise<{ blob: Blob; contentType: string; thumbnail: string | null }> {
  if (contentType.includes("hei")) return { blob: file, contentType, thumbnail: null };
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, Math.sqrt(TARGET_PIXELS / (bitmap.width * bitmap.height)));
  const blob = await draw(bitmap, scale, 0.85);
  const thumbnail = await toDataUrl(await draw(bitmap, Math.min(1, 160 / Math.max(bitmap.width, bitmap.height)), 0.7));
  bitmap.close();
  return blob.size < file.size || contentType !== "image/jpeg" ? { blob, contentType: "image/jpeg", thumbnail } : { blob: file, contentType, thumbnail };
}

async function draw(bitmap: ImageBitmap, scale: number, quality: number): Promise<Blob> {
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  canvas.getContext("2d")!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  return new Promise((resolve, reject) => canvas.toBlob((b) => (b ? resolve(b) : reject(new Error("toBlob"))), "image/jpeg", quality));
}

const toDataUrl = (blob: Blob) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(blob);
  });

function orThrow<T>(result: { data?: T; error?: unknown }): T {
  if (result.error !== undefined) throw new ApiError(result.error as Problem);
  return result.data as T;
}

/** POST /api/media/upload-url, then the signed form straight to storage with progress, then confirm (C3.1). */
export async function uploadFile(options: {
  draftId: string;
  elementKey: string | null;
  kind: UploadKind;
  blob: Blob;
  contentType: string;
  onProgress: (percent: number) => void;
}): Promise<string> {
  const { draftId, elementKey, kind, blob, contentType, onProgress } = options;
  const upload = orThrow(
    await api.POST("/api/media/upload-url", { body: { draftId, elementKey, kind, contentType, sizeBytes: blob.size } }),
  );

  await postToStorage(upload, blob, onProgress);
  orThrow(await api.POST("/api/media/{fileId}/confirm", { params: { path: { fileId: upload.fileId } }, body: { draftId } }));
  return upload.fileId;
}

/** The signed form straight to storage, with upload progress. */
export function postToStorage(
  upload: { url: string; fields: Record<string, string> },
  blob: Blob,
  onProgress: (percent: number) => void,
): Promise<void> {
  const form = new FormData();
  for (const [name, value] of Object.entries(upload.fields)) form.append(name, value);
  form.append("file", blob);
  return new Promise<void>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", upload.url);
    xhr.upload.onprogress = (event) => event.lengthComputable && onProgress(Math.round((event.loaded / event.total) * 100));
    xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`storage ${xhr.status}`)));
    xhr.onerror = () => reject(new Error("network"));
    xhr.send(form);
  });
}

export async function deleteUpload(draftId: string, fileId: string): Promise<void> {
  await api.DELETE("/api/media/{fileId}", { params: { path: { fileId }, query: { draftId } } });
}
