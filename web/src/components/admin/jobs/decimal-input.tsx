"use client";

import { useState, type ComponentProps } from "react";
import { cn } from "@/lib/utils";

const parse = (text: string): number | null => {
  const normalized = text.trim().replace(",", ".");
  return normalized === "" || !/^\d+(\.\d{0,2})?$/.test(normalized) ? null : Number(normalized);
};

type Props = Omit<ComponentProps<"input">, "value" | "onChange"> & { value: number | null; onChange: (value: number | null) => void; invalid?: boolean };

/** Metres typed with comma or dot ("3,5"); reports null while empty or unreadable. */
export function DecimalInput({ value, onChange, invalid, className, ...input }: Props) {
  const [text, setText] = useState(value == null ? "" : String(value).replace(".", ","));
  const [last, setLast] = useState(value);
  // A new value from outside (reset, size → measures) replaces the text.
  if (value !== last && value !== parse(text)) {
    setLast(value);
    setText(value == null ? "" : String(value).replace(".", ","));
  }

  return (
    <input
      inputMode="decimal"
      value={text}
      aria-invalid={invalid || (text !== "" && parse(text) === null) ? true : undefined}
      onChange={(e) => {
        setText(e.target.value);
        const parsed = parse(e.target.value);
        setLast(parsed);
        onChange(parsed);
      }}
      className={cn(
        "h-10 w-full min-w-0 rounded-lg border-[1.5px] border-input bg-card px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive disabled:bg-background disabled:text-muted-foreground",
        className,
      )}
      {...input}
    />
  );
}
