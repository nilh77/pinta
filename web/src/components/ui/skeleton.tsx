import { cn } from "@/lib/utils";

/** A placeholder block shaped like the content it stands for (docs/screens.md: skeletons, never a full-screen spinner). */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" data-slot="skeleton" className={cn("animate-pulse rounded-xl bg-primary-soft motion-reduce:animate-none", className)} />;
}

/** Loading state for a list of cards; `label` is read by screen readers only. */
export function CardListSkeleton({ label, count = 3, className }: { label: string; count?: number; className?: string }) {
  return (
    <div role="status" className={cn("flex flex-col gap-3", className)}>
      <span className="sr-only">{label}</span>
      {Array.from({ length: count }, (_, i) => (
        <Skeleton key={i} className="h-28 rounded-[20px]" />
      ))}
    </div>
  );
}
