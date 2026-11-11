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

/**
 * Loading state for a detail page (a job, an account, a painter): a title, a status line and the stacked cards those
 * pages are made of. `label` is read by screen readers only.
 */
export function DetailSkeleton({ label, className }: { label: string; className?: string }) {
  return (
    <div role="status" className={cn("flex flex-col gap-4", className)}>
      <span className="sr-only">{label}</span>
      <div className="flex flex-col gap-2">
        <Skeleton className="h-8 w-2/3 max-w-80" />
        <Skeleton className="h-4 w-1/3 max-w-40 rounded-lg" />
      </div>
      <Skeleton className="h-24 rounded-[20px]" />
      <Skeleton className="h-44 rounded-[20px]" />
      <Skeleton className="h-24 rounded-[20px]" />
    </div>
  );
}
