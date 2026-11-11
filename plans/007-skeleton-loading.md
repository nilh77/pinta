# 007 — Skeletons compartidos en lugar de spinners (área admin y listas)

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Loading states (el documento manda sobre el código)
- **Estimated scope**: 1 componente nuevo, 1 test nuevo y 7 pantallas (cambio de JSX de carga)
- **Alcance**: confirmado por el usuario el 2026-11-11 (área admin y las 6 listas; las pantallas de detalle quedan para otro plan).
- **Sistema**: `plans/000-motion-system.md` §5 «Carga» y §7.

## Problem

`docs/screens.md:18`: «Carga: esqueletos con la forma del contenido, nunca un spinner a pantalla completa.» `docs/screens.md:275` lista un componente compartido `Skeleton`, que no existe. El código hace lo contrario:

```tsx
// web/src/components/admin/admin-shell.tsx:34-41 — current (full-screen spinner while the session loads)
  if (me.isPending || me.data === null) {
    return (
      <div role="status" className="flex min-h-dvh items-center justify-center gap-2 text-muted-foreground">
        <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
        {t("loading")}
      </div>
    );
  }
```

Y las listas cargan con una línea de spinner y texto; luego las tarjetas aparecen de golpe y empujan el contenido. Ejemplo:

```tsx
// web/src/components/jobs/my-jobs-screen.tsx:53-57 — current
        {jobs.isPending ? (
          <p role="status" className="flex items-center gap-2 py-10 text-muted-foreground">
            <Loader2 aria-hidden="true" className="size-5 animate-spin motion-reduce:animate-none" />
            {t("loading")}
          </p>
```

Mismo patrón en: `web/src/components/admin/jobs/jobs-board.tsx:129-133`, `web/src/components/painter/painter-jobs-screen.tsx:56-60`, `web/src/components/admin/painters/painters-screen.tsx:30-34`, `web/src/components/admin/review/review-screen.tsx:44-48` y `web/src/components/admin/finance/finance-screen.tsx:67-71`.

Los skeletons que ya existen (p. ej. `web/src/components/quote/quote-shell.tsx:93-101`) son la referencia visual correcta: bloques `bg-primary-soft` con `animate-pulse motion-reduce:animate-none`.

## Target

- `web/src/components/ui/skeleton.tsx` con `Skeleton` (un bloque) y `CardListSkeleton` (estado de carga con forma de lista de tarjetas, `role="status"` y texto solo para lectores de pantalla).
- El área admin carga con la forma de su layout (barra lateral + cabecera + tarjetas) en lugar del spinner a pantalla completa.
- Las seis listas cargan con `CardListSkeleton`.

## Repo conventions to follow

- Componentes de `ui/` pequeños y sin estado, como `web/src/components/ui/card.tsx`; `cn()` de `@/lib/utils`.
- Mismo aspecto que `quote-shell.tsx:95-99`: `animate-pulse rounded-… bg-primary-soft motion-reduce:animate-none`.
- Las tarjetas de lista usan `rounded-[20px]` (p. ej. `my-jobs-screen.tsx:121`).
- Tests de componentes con Testing Library (p. ej. `web/src/lib/arrow-keys.test.tsx`).

## Steps

1. Crea `web/src/components/ui/skeleton.tsx`:

```tsx
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
```

2. Crea `web/src/components/ui/skeleton.test.tsx`: renderiza `<CardListSkeleton label="Loading" count={2} />` y comprueba que `getByRole("status")` contiene el texto «Loading» y que hay 2 elementos `[data-slot="skeleton"]`.

3. `web/src/components/admin/admin-shell.tsx`: sustituye el `return` de las líneas 35-40 por

```tsx
    return (
      <div role="status" className="flex min-h-dvh flex-col lg:flex-row">
        <span className="sr-only">{t("loading")}</span>
        <div className="flex flex-col gap-3 border-b border-border bg-card px-4 py-3 lg:h-dvh lg:w-56 lg:shrink-0 lg:border-r lg:border-b-0 lg:px-3.5 lg:py-5">
          <Skeleton className="h-8 w-32" />
          <div className="flex gap-1 overflow-hidden lg:flex-col">
            {Array.from({ length: 5 }, (_, i) => (
              <Skeleton key={i} className="h-11 w-28 shrink-0 lg:w-full" />
            ))}
          </div>
        </div>
        <div className="flex min-w-0 flex-1 flex-col gap-4 px-4 py-6 sm:px-6 lg:px-8 lg:py-7">
          <Skeleton className="h-9 w-56" />
          {Array.from({ length: 3 }, (_, i) => (
            <Skeleton key={i} className="h-28 rounded-[20px]" />
          ))}
        </div>
      </div>
    );
```

   Importa `Skeleton` desde `@/components/ui/skeleton` y quita `Loader2` de la importación de `lucide-react` en la línea 3 (ya no se usa en este archivo).

4. En cada una de estas pantallas, sustituye el bloque `<p role="status" …><Loader2 … />{t("loading")}</p>` de la rama `isPending` por `<CardListSkeleton label={t("loading")} />`, importa `CardListSkeleton` desde `@/components/ui/skeleton` y quita `Loader2` de la importación de `lucide-react` **solo si** ya no se usa en el archivo:
   - `web/src/components/jobs/my-jobs-screen.tsx:54-57` (quita `Loader2`)
   - `web/src/components/painter/painter-jobs-screen.tsx:57-60` (**mantén** `Loader2`: se usa en la línea 159)
   - `web/src/components/admin/jobs/jobs-board.tsx:130-133` (quita `Loader2`)
   - `web/src/components/admin/painters/painters-screen.tsx:31-34` (quita `Loader2`)
   - `web/src/components/admin/review/review-screen.tsx:45-48` (quita `Loader2`)
   - `web/src/components/admin/finance/finance-screen.tsx:68-71` (quita `Loader2` si `grep -n Loader2` ya no da usos)

   En las que el `<p>` tenía `py-10`, pasa `className="py-2"` a `CardListSkeleton` para conservar el aire vertical.

5. No cambies las ramas de error ni de vacío.

## Boundaries

- Solo los archivos listados. Las pantallas de detalle (`admin-job-view.tsx`, `painter-job-screen.tsx`, `painter-visit-screen.tsx`, `painter-area.tsx`, `payment-screen.tsx`, `painter-screen.tsx`, `settings-screen.tsx`, `coverage-screen.tsx`, `painter-onboarding.tsx`, `painter-payouts-screen.tsx`, `confirm-email.tsx`) quedan para un plan posterior: cada una necesita su propia forma.
- No sustituyas los spinners **dentro de botones** (acciones en curso): son correctos.
- No migres en este plan los skeletons ad hoc que ya existen (`quote-shell.tsx`, `estimate-step.tsx`, `account-step.tsx`, `pricing-screen.tsx`, `site-header.tsx`).
- No cambies textos ni claves de traducción: se reutilizan las existentes (`t("loading")`).
- Si un extracto no coincide, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde, incluido `skeleton.test.tsx`.
- **Feel check** (DevTools → Network → «Slow 4G» para ver la carga):
  - Entra como admin: mientras carga la sesión se ve la silueta de la barra lateral y del contenido, no un spinner centrado; al cargar, el contenido real ocupa el mismo sitio sin salto grande.
  - Mis trabajos, Trabajos del pintor, Tablero admin, Pintores, Revisión y Finanzas: bloques con forma de tarjeta que laten suavemente.
  - Con `prefers-reduced-motion: reduce`: los bloques no laten, pero se ven.
  - VoiceOver o NVDA anuncian el texto de carga una vez.
  - 390 px y 1440 px: sin scroll horizontal (la fila de navegación del skeleton admin en móvil se recorta con `overflow-hidden`).
- **Done when**:
  - [ ] No queda ningún spinner a pantalla completa; el área admin carga con la silueta de su layout.
  - [ ] Las seis listas cargan con `CardListSkeleton` y el texto de carga se anuncia una vez.
  - [ ] Sin scroll horizontal a 390 px ni a 1440 px.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde, incluido `skeleton.test.tsx`.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**: «Skeletons en el área admin y en las listas».
