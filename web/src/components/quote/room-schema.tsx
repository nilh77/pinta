"use client";

import { Check, Copy, MoreHorizontal, Pencil, Plus, Trash2 } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { DropdownMenu } from "radix-ui";
import { formatArea } from "@/lib/format";
import { elementAreaOf, MAX_ELEMENTS, validateElement, type CatalogInfo, type DraftElement } from "@/lib/quote/draft";
import type { PublicCatalog } from "@/lib/quote/use-quote-data";
import { cn } from "@/lib/utils";

const PAINT_COLOURS = ["#ffffff", "#f3f1ea", "#e6f0f4", "#f6e7d8", "#e8eef0", "#efe8f3"];

type RoomSchemaProps = {
  elements: DraftElement[];
  names: string[];
  catalog: PublicCatalog;
  info: CatalogInfo;
  selectedId?: string;
  /** "house": the mobile schema under a roof (board Main); "list": the desktop column (board DeskConstructor). */
  layout: "house" | "list";
  onOpen: (id: string) => void;
  onRename: (id: string) => void;
  onDuplicate: (id: string) => void;
  onRemove: (id: string) => void;
  onAddRoom: (roomTypeId: string) => void;
  onAddSurface: () => void;
};

/** RoomSchema (docs/screens.md, screen 2): a simple layout of the home, not a plan to scale. It is also a list. */
export function RoomSchema(props: RoomSchemaProps) {
  const { elements, names, layout } = props;
  const t = useTranslations("quote.schema");
  const full = elements.length >= MAX_ELEMENTS;

  const list = (
    <ul aria-label={t("listLabel")} className={cn("grid gap-2", layout === "house" ? "grid-cols-2" : "grid-cols-1")}>
      {elements.map((element, index) => (
        <li key={element.id} className={cn(layout === "house" && index === 0 && "col-span-2")}>
          <RoomCard {...props} element={element} name={names[index]} wide={layout === "list" || index === 0} />
        </li>
      ))}
      <li className={cn(layout === "house" && "col-span-2")}>
        <AddMenu {...props} disabled={full} />
        {full && <p className="mt-1.5 text-xs text-muted-foreground">{t("maxReached", { max: MAX_ELEMENTS })}</p>}
      </li>
    </ul>
  );

  if (layout === "list") return list;
  return (
    <div>
      <svg viewBox="0 0 350 44" aria-hidden="true" className="block h-auto w-full">
        <rect x="262" y="10" width="20" height="24" fill="var(--accent-soft)" stroke="var(--accent)" strokeWidth="3" />
        <polygon points="6,42 175,4 344,42" fill="var(--accent-soft)" stroke="var(--accent)" strokeWidth="3" strokeLinejoin="round" />
      </svg>
      <div className="rounded-b-[18px] border-[3px] border-t-0 border-accent bg-card p-3">{list}</div>
    </div>
  );
}

function RoomCard({
  element,
  name,
  wide,
  catalog,
  info,
  selectedId,
  onOpen,
  onRename,
  onDuplicate,
  onRemove,
}: RoomSchemaProps & { element: DraftElement; name: string; wide: boolean }) {
  const t = useTranslations("quote.schema");
  const locale = useLocale();
  const area = elementAreaOf(element, info);
  const invalid = element.described && (Object.keys(validateElement(element)).length > 0 || !area || area.total === 0);
  const paintIndex = Math.max(0, catalog.paints.findIndex((p) => p.id === element.paintId));
  const paintName = catalog.paints[paintIndex]?.name ?? element.paintId;
  const summary = !element.described
    ? t("notDescribed")
    : invalid
      ? t("fixIt")
      : `${formatArea(locale, area!.total)} · ${element.kind === "surface" ? (catalog.surfaceTypes.find((s) => s.id === element.surfaceTypeId)?.name ?? "") : wallsSummary(element, t)}`;

  return (
    <div
      className={cn(
        "group relative flex rounded-2xl border-[1.5px] transition-colors motion-reduce:transition-none",
        element.described && !invalid ? "border-primary bg-primary-soft" : "border-dashed border-[#7c918b] bg-card",
        invalid && "border-solid border-destructive bg-destructive/5",
        selectedId === element.id && "ring-[3px] ring-ring/40",
        wide ? "min-h-[72px] items-center" : "min-h-[104px]",
      )}
    >
      <button
        type="button"
        onClick={() => onOpen(element.id)}
        aria-current={selectedId === element.id ? "true" : undefined}
        className={cn(
          "flex min-w-0 flex-1 gap-3 rounded-2xl p-3 pr-12 text-left focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none",
          wide ? "items-center" : "flex-col justify-end",
        )}
      >
        <span className="min-w-0">
          <span className="flex flex-wrap items-center gap-x-1.5 gap-y-1">
            <span className="line-clamp-2 text-[15px] font-semibold break-words">{name}</span>
            {element.damage && <span className="shrink-0 rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-ink">{t("damageBadge")}</span>}
          </span>
          <span className={cn("mt-0.5 flex items-center gap-1.5 text-xs", invalid ? "font-medium text-destructive" : "text-muted-foreground")}>
            {element.described && !invalid && (
              <span aria-hidden="true" className="size-2.5 shrink-0 rounded-full border border-[#b9c9c4]" style={{ background: PAINT_COLOURS[paintIndex % PAINT_COLOURS.length] }} />
            )}
            <span className="truncate">{summary}</span>
            {element.described && !invalid && <span className="sr-only">, {paintName}</span>}
          </span>
        </span>
        {!element.described && <span className={cn("text-[11px] font-semibold text-primary", wide && "ml-auto")}>{t("describe")}</span>}
      </button>
      {element.described && !invalid && (
        <span aria-hidden="true" className={cn("pointer-events-none absolute flex size-6 items-center justify-center rounded-full bg-primary text-primary-foreground", wide ? "right-12" : "top-3 left-3")}>
          <Check className="size-3.5" strokeWidth={3} />
        </span>
      )}
      <DropdownMenu.Root>
        <DropdownMenu.Trigger
          aria-label={t("menu", { name })}
          className="absolute top-1.5 right-1.5 flex size-11 items-center justify-center rounded-xl text-muted-foreground hover:bg-card hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none"
        >
          <MoreHorizontal aria-hidden="true" className="size-5" />
        </DropdownMenu.Trigger>
        <DropdownMenu.Portal>
          <DropdownMenu.Content align="end" sideOffset={4} className={menuClass}>
            <DropdownMenu.Item onSelect={() => onRename(element.id)} className={itemClass}>
              <Pencil aria-hidden="true" className="size-4" />
              {t("rename")}
            </DropdownMenu.Item>
            <DropdownMenu.Item onSelect={() => onDuplicate(element.id)} className={itemClass}>
              <Copy aria-hidden="true" className="size-4" />
              {t("duplicate")}
            </DropdownMenu.Item>
            <DropdownMenu.Item onSelect={() => onRemove(element.id)} className={cn(itemClass, "text-destructive data-[highlighted]:text-destructive")}>
              <Trash2 aria-hidden="true" className="size-4" />
              {t("remove")}
            </DropdownMenu.Item>
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>
    </div>
  );
}

function wallsSummary(element: DraftElement, t: ReturnType<typeof useTranslations<"quote.schema">>) {
  const walls = element.walls.filter(Boolean).length;
  if (walls === 0) return t("ceilingOnly");
  return t(element.ceiling ? "wallsAndCeiling" : "wallsOnly", { count: walls });
}

function AddMenu({ catalog, onAddRoom, onAddSurface, disabled }: RoomSchemaProps & { disabled: boolean }) {
  const t = useTranslations("quote.schema");
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        disabled={disabled}
        className="flex h-13 w-full items-center justify-center gap-2 rounded-2xl border-[1.5px] border-dashed border-accent bg-[#fffbeb] text-sm font-semibold text-accent-ink hover:bg-accent-soft focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
      >
        <Plus aria-hidden="true" className="size-[18px]" strokeWidth={2.2} />
        {t("add")}
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="center" sideOffset={6} className={menuClass}>
          <DropdownMenu.Label className="px-3 pt-1 pb-1.5 text-xs font-semibold text-muted-foreground">{t("addRoomLabel")}</DropdownMenu.Label>
          {catalog.roomTypes.map((room) => (
            <DropdownMenu.Item key={room.id} onSelect={() => onAddRoom(room.id)} className={itemClass}>
              {room.name}
            </DropdownMenu.Item>
          ))}
          <DropdownMenu.Separator className="my-1 h-px bg-border" />
          <DropdownMenu.Item onSelect={onAddSurface} className={itemClass}>
            {t("addSurface")}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}

const menuClass =
  "z-50 min-w-52 rounded-2xl border border-border bg-card p-1.5 shadow-lg data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 motion-reduce:animate-none";
const itemClass =
  "flex min-h-11 cursor-pointer items-center gap-2.5 rounded-xl px-3 text-sm font-medium outline-none data-[highlighted]:bg-primary-soft";
