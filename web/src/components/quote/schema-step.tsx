"use client";

import { Pencil } from "lucide-react";
import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { Dialog } from "radix-ui";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Sheet } from "@/components/ui/sheet";
import { useRouter } from "@/i18n/navigation";
import { formatArea } from "@/lib/format";
import {
  addRoom,
  addSurface,
  displayNames,
  duplicateElement,
  elementAreaOf,
  removeElement,
  renameElement,
  restoreElement,
  saveElement,
  toEstimateRequest,
  validateElement,
  type DraftElement,
} from "@/lib/quote/draft";
import { useEstimate, useIsDesktop } from "@/lib/quote/use-quote-data";
import { EstimateSummary } from "./estimate-bar";
import { OccupancyPicker } from "./occupancy-picker";
import { QuoteShell, type QuoteContext } from "./quote-shell";
import { RoomEditor } from "./room-editor";
import { RoomSchema } from "./room-schema";

const PATH = "/cotizar/esquema";

/** Screens 2 (Esquema) and 3 (Editor, `?el=`), and the desktop builder D2 [C2.2–C2.6]. */
export function SchemaStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={1} back={{ href: "/cotizar", label: t("schema.back") }} wide>
      {(context) => <SchemaBuilder {...context} />}
    </QuoteShell>
  );
}

function SchemaBuilder(context: QuoteContext) {
  const { draft, catalog, info, update } = context;
  const t = useTranslations("quote");
  const locale = useLocale();
  const router = useRouter();
  const desktop = useIsDesktop();
  const selectedParam = useSearchParams().get("el");
  const [occupancyError, setOccupancyError] = useState(false);
  const [blocked, setBlocked] = useState<string | null>(null);
  const [renaming, setRenaming] = useState<string | null>(null);

  useEffect(() => {
    if (!draft) router.replace("/cotizar");
  }, [draft, router]);

  const request = useMemo(() => (draft ? toEstimateRequest(draft, info) : null), [draft, info]);
  const estimate = useEstimate(request);
  if (!draft) return null;

  const elements = draft.elements;
  const names = displayNames(elements, Object.fromEntries(catalog.roomTypes.map((r) => [r.id, r.name])), t("schema.surfaceName"));
  const described = elements.filter((e) => e.described);
  const area = described.reduce((sum, e) => sum + (elementAreaOf(e, info)?.total ?? 0), 0);
  const selected = elements.find((e) => e.id === selectedParam);
  // The desktop editor always shows an element: the one in the URL or the first.
  const editing = desktop ? (selected ?? elements[0]) : selected;

  const open = (id: string) => router.replace(`${PATH}?el=${id}`, { scroll: false });
  const close = () => router.replace(PATH, { scroll: false });

  const remove = (id: string) => {
    const index = elements.findIndex((e) => e.id === id);
    const { draft: next, removed } = removeElement(draft, id);
    if (!removed) return;
    update(next);
    if (selectedParam === id) close();
    toast(t("schema.removed", { name: names[index] }), {
      duration: 5000,
      action: { label: t("schema.undo"), onClick: () => update((d) => restoreElement(d, removed)) },
    });
  };

  const add = (next: typeof draft) => {
    update(next);
    const created = next.elements.at(-1);
    if (created && next.elements.length > elements.length) open(created.id);
  };

  const seePrice = () => {
    if (draft.occupancy === null) {
      setOccupancyError(true);
      document.getElementById("occupancy")?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    const invalid = described.some((e) => Object.keys(validateElement(e)).length > 0 || (elementAreaOf(e, info)?.total ?? 0) === 0);
    if (invalid) return setBlocked(t("schema.fixInvalid"));
    if (!request) return setBlocked(t("schema.describeOne"));
    update((d) => ({ ...d, step: "estimate" }));
    router.push("/cotizar/estimacion");
  };

  const schema = (
    <RoomSchema
      elements={elements}
      names={names}
      catalog={catalog}
      info={info}
      selectedId={editing?.id}
      layout={desktop ? "list" : "house"}
      onOpen={open}
      onRename={setRenaming}
      onDuplicate={(id) => update((d) => duplicateElement(d, id))}
      onRemove={remove}
      onAddRoom={(roomTypeId) => add(addRoom(draft, roomTypeId, info))}
      onAddSurface={() => add(addSurface(draft, info))}
    />
  );
  const occupancy = (
    <OccupancyPicker
      catalog={catalog}
      value={draft.occupancy}
      error={occupancyError}
      onChange={(value) => {
        setOccupancyError(false);
        update((d) => ({ ...d, occupancy: value }));
      }}
    />
  );
  const summary = (
    <EstimateSummary
      described={described.length}
      total={elements.length}
      area={area}
      estimate={estimate.estimate}
      isStale={estimate.isStale}
      isError={estimate.isError}
      isPaused={estimate.isPaused}
      onRetry={estimate.retry}
    />
  );
  const seePriceButton = (
    <>
      {blocked && (
        <p role="alert" className="text-sm font-medium text-destructive">
          {blocked}
        </p>
      )}
      <Button size="lg" className="w-full" onClick={seePrice}>
        {t("schema.seePrice")}
      </Button>
    </>
  );
  const heading = (
    <div>
      <h1 className="text-[26px] leading-tight font-bold tracking-tight">{t(`schema.title.${draft.type}`)}</h1>
      <p className="mt-1.5 text-sm text-muted-foreground">{t(desktop ? "schema.subtitleDesktop" : "schema.subtitle")}</p>
    </div>
  );
  const renameDialog = (
    <RenameDialog
      key={renaming ?? "none"}
      open={renaming !== null}
      initial={renaming ? (elements.find((e) => e.id === renaming)?.customName ?? names[elements.findIndex((e) => e.id === renaming)]) : ""}
      onClose={() => setRenaming(null)}
      onSave={(name) => {
        if (renaming) update((d) => renameElement(d, renaming, name));
        setRenaming(null);
      }}
    />
  );

  if (desktop) {
    return (
      <div className="grid flex-1 items-start gap-6 pb-10 lg:grid-cols-[320px_minmax(0,1fr)_340px]">
        <div className="flex flex-col gap-5">
          {heading}
          {occupancy}
          {elements.length === 0 && <EmptySchema />}
          {schema}
        </div>
        <section aria-label={t("editor.label")} className="rounded-3xl border border-border bg-card p-6">
          {editing ? (
            <InlineEditor key={editing.id} element={editing} name={names[elements.indexOf(editing)]} context={context} onRename={() => setRenaming(editing.id)} />
          ) : (
            <p className="py-16 text-center text-sm text-muted-foreground">{t("editor.pick")}</p>
          )}
        </section>
        <aside className="sticky top-6 flex flex-col gap-4 rounded-3xl border border-border bg-card p-5">
          {summary}
          {seePriceButton}
          <p className="text-center text-xs text-muted-foreground">{t("schema.partialHint")}</p>
        </aside>
        {renameDialog}
      </div>
    );
  }

  return (
    <div className="flex flex-1 flex-col gap-5 pb-48">
      {heading}
      {occupancy}
      {elements.length === 0 && <EmptySchema />}
      {schema}
      <div className="fixed inset-x-0 bottom-0 z-30 rounded-t-[20px] border-t border-border bg-card px-5 pt-4 pb-[max(1.5rem,env(safe-area-inset-bottom))]">
        <div className="mx-auto flex max-w-2xl flex-col gap-3">
          {summary}
          {seePriceButton}
        </div>
      </div>
      {selected && (
        <SheetEditor key={selected.id} element={selected} name={names[elements.indexOf(selected)]} context={context} onClose={close} onRename={() => setRenaming(selected.id)} />
      )}
      {renameDialog}
      <span className="sr-only" aria-live="polite">
        {formatArea(locale, area)}
      </span>
    </div>
  );
}

function EmptySchema() {
  const t = useTranslations("quote.schema");
  return (
    <div className="rounded-2xl border border-dashed border-border bg-card px-4 py-8 text-center">
      <p className="font-semibold">{t("emptyTitle")}</p>
      <p className="mt-1 text-sm text-muted-foreground">{t("emptyBody")}</p>
    </div>
  );
}

type EditorProps = { element: DraftElement; name: string; context: QuoteContext; onRename: () => void };

/** Screen 3 on phones and tablets: changes apply on "Guardar" (C2.3: at least a wall or the ceiling). */
function SheetEditor({ element, name, context, onClose, onRename }: EditorProps & { onClose: () => void }) {
  const t = useTranslations("quote.editor");
  const [working, setWorking] = useState(element);
  const [showErrors, setShowErrors] = useState(false);
  const errors = validateElement(working);

  const save = () => {
    if (Object.keys(errors).length > 0) return setShowErrors(true);
    context.update((d) => saveElement(d, working));
    onClose();
  };

  return (
    <Sheet
      open
      onOpenChange={(open) => !open && onClose()}
      title={name}
      closeLabel={t("cancel")}
      actions={<RenameButton onClick={onRename} />}
      footer={
        <div className="flex gap-2">
          <Button variant="ghost" size="lg" onClick={onClose}>
            {t("cancel")}
          </Button>
          <Button size="lg" className="flex-1" onClick={save}>
            {t("save")}
          </Button>
        </div>
      }
    >
      <RoomEditor element={working} onChange={setWorking} errors={showErrors ? errors : {}} catalog={context.catalog} info={context.info} />
    </Sheet>
  );
}

/** Desktop D2: the editor is always open and every valid change is saved at once. */
function InlineEditor({ element, name, context, onRename }: EditorProps) {
  const t = useTranslations("quote.editor");
  const [working, setWorking] = useState(element);
  const errors = validateElement(working);

  const change = (next: DraftElement) => {
    setWorking(next);
    if (Object.keys(validateElement(next)).length === 0) context.update((d) => saveElement(d, next));
  };

  return (
    <div className="flex flex-col gap-5">
      <div className="flex items-center gap-2">
        <h2 className="min-w-0 flex-1 truncate font-display text-2xl font-bold">{name}</h2>
        <RenameButton onClick={onRename} />
      </div>
      {!element.described && <p className="text-sm text-muted-foreground">{t("autosaveHint")}</p>}
      <RoomEditor element={working} onChange={change} errors={errors} catalog={context.catalog} info={context.info} />
    </div>
  );
}

function RenameButton({ onClick }: { onClick: () => void }) {
  const t = useTranslations("quote.editor");
  return (
    <button
      type="button"
      aria-label={t("rename")}
      onClick={onClick}
      className="flex size-11 items-center justify-center rounded-xl text-muted-foreground hover:bg-primary-soft hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <Pencil aria-hidden="true" className="size-4" />
    </button>
  );
}

function RenameDialog({ open, initial, onClose, onSave }: { open: boolean; initial: string; onClose: () => void; onSave: (name: string) => void }) {
  const t = useTranslations("quote.rename");
  const [value, setValue] = useState(initial);
  return (
    <Dialog.Root open={open} onOpenChange={(next) => !next && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-foreground/40" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-sm -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl"
        >
          <Dialog.Title className="font-display text-xl font-bold">{t("title")}</Dialog.Title>
          <form
            className="mt-4 flex flex-col gap-4"
            onSubmit={(e) => {
              e.preventDefault();
              onSave(value);
            }}
          >
            <label className="flex flex-col gap-1.5 text-sm font-semibold">
              {t("label")}
              <input
                autoFocus
                maxLength={80}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                className="h-12 rounded-xl border-[1.5px] border-input bg-card px-3 text-base font-normal outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
              />
            </label>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="ghost" onClick={onClose}>
                {t("cancel")}
              </Button>
              <Button type="submit">{t("save")}</Button>
            </div>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
