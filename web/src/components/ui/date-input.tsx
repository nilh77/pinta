"use client";

import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Popover } from "radix-ui";
import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import {
  addDays,
  addMonths,
  clamp,
  dayOfMonth,
  formatFieldDate,
  formatLongDate,
  formatMonth,
  isIsoDate,
  isSelectable,
  monthGrid,
  startOfMonth,
  weekday,
  weekdayNames,
  type IsoDate,
} from "@/lib/calendar";
import { cn } from "@/lib/utils";

type Props = {
  id?: string;
  value: string;
  onChange: (value: IsoDate) => void;
  min?: string;
  max?: string;
  disabled?: boolean;
  invalid?: boolean;
  placeholder?: string;
  className?: string;
  "aria-label"?: string;
  "aria-describedby"?: string;
};

/** Today in Lisbon, the day the calendar opens on when nothing is chosen yet. */
const todayInLisbon = () => new Date().toLocaleDateString("en-CA", { timeZone: "Europe/Lisbon" });

/**
 * Pinta's date field (docs/screens.md, «Formularios»): a field with the date written in the language, and a panel with
 * the month, arrows and days instead of the browser's own picker. Keyboard: arrows move a day or a week, Page Up/Down a
 * month, Home/End the start or end of the week, Enter or Space picks, Escape closes.
 */
export function DateInput({ id, value, onChange, min, max, disabled, invalid, placeholder, className, ...aria }: Props) {
  const t = useTranslations("common.calendar");
  const locale = useLocale();
  const [open, setOpen] = useState(false);
  const selected = isIsoDate(value) ? value : null;
  const [focused, setFocused] = useState<IsoDate>(() => clamp(selected ?? todayInLisbon(), min, max));
  const month = startOfMonth(focused);
  const grid = useRef<HTMLDivElement>(null);

  // Opening starts on the chosen day (or today, within the limits).
  const onOpenChange = (next: boolean) => {
    if (next) setFocused(clamp(selected ?? todayInLisbon(), min, max));
    setOpen(next);
  };

  // The focused day keeps the keyboard focus as it moves, also across months.
  useEffect(() => {
    if (open) grid.current?.querySelector<HTMLButtonElement>(`[data-day="${focused}"]`)?.focus();
  }, [open, focused]);

  const pick = (day: IsoDate) => {
    if (!isSelectable(day, min, max)) return;
    onChange(day);
    setOpen(false);
  };

  const onKeyDown = (event: KeyboardEvent) => {
    const moves: Record<string, () => IsoDate> = {
      ArrowLeft: () => addDays(focused, -1),
      ArrowRight: () => addDays(focused, 1),
      ArrowUp: () => addDays(focused, -7),
      ArrowDown: () => addDays(focused, 7),
      PageUp: () => addMonths(focused, -1),
      PageDown: () => addMonths(focused, 1),
      Home: () => addDays(focused, -weekday(focused)),
      End: () => addDays(focused, 6 - weekday(focused)),
    };
    const move = moves[event.key];
    if (move) {
      event.preventDefault();
      setFocused(clamp(move(), min, max));
    }
  };

  const canGoBack = !min || addMonths(month, -1) >= startOfMonth(min);
  const canGoForward = !max || addMonths(month, 1) <= max;
  const names = weekdayNames(locale, "narrow");
  const fullNames = weekdayNames(locale, "long");

  return (
    <Popover.Root open={open} onOpenChange={onOpenChange}>
      <Popover.Trigger
        id={id}
        type="button"
        disabled={disabled}
        aria-invalid={invalid || undefined}
        aria-label={aria["aria-label"] ? `${aria["aria-label"]}${selected ? `, ${formatLongDate(locale, selected)}` : ""}` : undefined}
        aria-describedby={aria["aria-describedby"]}
        className={cn(
          "flex h-12 w-full min-w-0 items-center gap-2 rounded-xl border-[1.5px] border-input bg-card px-3.5 text-left text-base outline-none",
          "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive disabled:opacity-50",
          className,
        )}
      >
        <span className={cn("flex-1 truncate", !selected && "text-muted-foreground")}>
          {selected ? formatFieldDate(locale, selected) : (placeholder ?? t("choose"))}
        </span>
        <CalendarDays aria-hidden="true" className="size-[18px] shrink-0 text-primary" />
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          align="start"
          sideOffset={6}
          collisionPadding={16}
          // Focus starts on the chosen day, not on the first button (the previous-month arrow).
          onOpenAutoFocus={(event) => {
            event.preventDefault();
            grid.current?.querySelector<HTMLButtonElement>('[tabindex="0"]')?.focus();
          }}
          className="z-50 w-[296px] rounded-2xl border border-border bg-card p-3 text-foreground shadow-[0_12px_32px_-8px_rgb(16_32_29/0.25)] outline-none"
        >
          <div className="mb-2 flex items-center justify-between gap-2">
            <button
              type="button"
              disabled={!canGoBack}
              onClick={() => setFocused(clamp(addMonths(focused, -1), min, max))}
              aria-label={t("previous")}
              className="grid size-9 place-items-center rounded-full text-foreground hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none disabled:opacity-30"
            >
              <ChevronLeft aria-hidden="true" className="size-5" />
            </button>
            <p aria-live="polite" className="font-display text-[15px] font-bold first-letter:uppercase">
              {formatMonth(locale, month)}
            </p>
            <button
              type="button"
              disabled={!canGoForward}
              onClick={() => setFocused(clamp(addMonths(focused, 1), min, max))}
              aria-label={t("next")}
              className="grid size-9 place-items-center rounded-full text-foreground hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none disabled:opacity-30"
            >
              <ChevronRight aria-hidden="true" className="size-5" />
            </button>
          </div>
          <div ref={grid} role="grid" aria-label={formatMonth(locale, month)} onKeyDown={onKeyDown} className="flex flex-col gap-0.5">
            <div role="row" className="grid grid-cols-7">
              {names.map((name, i) => (
                <span key={i} role="columnheader" aria-label={fullNames[i]} className="grid h-8 place-items-center text-xs font-semibold text-muted-foreground uppercase">
                  {name}
                </span>
              ))}
            </div>
            {monthGrid(month).map((week) => (
              <div key={week[0]} role="row" className="grid grid-cols-7">
                {week.map((day) => {
                  const inMonth = day.startsWith(month.slice(0, 7));
                  const enabled = isSelectable(day, min, max);
                  const isSelected = day === selected;
                  const isToday = day === todayInLisbon();
                  return (
                    <span key={day} role="gridcell" aria-selected={isSelected}>
                      <button
                        type="button"
                        data-day={day}
                        tabIndex={day === focused ? 0 : -1}
                        disabled={!enabled}
                        aria-label={formatLongDate(locale, day)}
                        aria-current={isToday ? "date" : undefined}
                        onClick={() => pick(day)}
                        onFocus={() => setFocused(day)}
                        className={cn(
                          "mx-auto grid size-9 place-items-center rounded-full text-sm tabular-nums outline-none",
                          "hover:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/50",
                          !inMonth && "text-muted-foreground/60",
                          isToday && !isSelected && "font-bold text-primary",
                          isSelected && "bg-primary font-bold text-primary-foreground hover:bg-primary",
                          !enabled && "pointer-events-none opacity-30",
                        )}
                      >
                        {dayOfMonth(day)}
                      </button>
                    </span>
                  );
                })}
              </div>
            ))}
          </div>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
