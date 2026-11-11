# 006 — Toasts con las duraciones del documento: éxito 3 s, error hasta cerrarlo

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Notificaciones (el documento manda sobre el código)
- **Estimated scope**: 1 archivo nuevo, 1 test nuevo, `toaster.tsx`, `eslint.config.mjs` y 17 importaciones (cambio mecánico)
- **Sistema**: `plans/000-motion-system.md` §5 «Toasts».

## Problem

`docs/screens.md:279`: «Toasts: éxito (3 s), error (hasta cerrarlo) y deshacer (5 s).»

```tsx
// web/src/components/toaster.tsx:10-13 — current
    <Sonner
      position={desktop ? "bottom-right" : "top-center"}
      toastOptions={{ classNames: { toast: "!rounded-2xl !border-border !bg-card !text-foreground !font-sans", actionButton: "!bg-primary !text-primary-foreground !font-semibold" } }}
    />
```

Nadie fija la duración, así que Sonner usa 4000 ms para todo (`TOAST_LIFETIME` en `sonner/dist/index.mjs:417`). Los errores se van solos a los 4 s, aunque un error tiene que poder leerse con calma. El toast de deshacer sí cumple (`web/src/components/quote/schema-step.tsx:82-85`, `duration: 5000`).

Sonner 2.0.7 no admite duración por tipo en el `Toaster` (`toastOptions.duration` es una sola para todos), así que el error necesita su propia duración en la llamada.

## Target

- `Toaster` con `duration={3000}` y botón de cierre estilado con la paleta.
- Un módulo `web/src/lib/toast.ts` que reexporta el `toast` de Sonner, con `toast.error` envuelto en `{ duration: Infinity, closeButton: true }` (lo que se pase en la llamada sigue mandando).
- Todo el código importa `toast` desde `@/lib/toast`, y ESLint impide volver a importarlo desde `"sonner"`.

## Repo conventions to follow

- Utilidades en `web/src/lib/`, con su test al lado (p. ej. `web/src/lib/format.ts` + `web/src/lib/format.test.ts`, Vitest).
- Comentarios de código en inglés; referencia al documento entre paréntesis, como en `toaster.tsx:6`.

## Steps

1. Crea `web/src/lib/toast.ts`:

```ts
import { toast as sonner, type ExternalToast } from "sonner";

/**
 * Sonner with the durations of docs/screens.md (global states): success 3 s (set on the Toaster), undo 5 s (per call),
 * errors stay until the user closes them. Import `toast` from here, never from "sonner".
 */
export const toast: typeof sonner = Object.assign((...args: Parameters<typeof sonner>) => sonner(...args), sonner, {
  error: ((message, data?: ExternalToast) => sonner.error(message, { duration: Infinity, closeButton: true, ...data })) as typeof sonner.error,
});
```

2. Crea `web/src/lib/toast.test.ts`:

```ts
import { describe, expect, it, vi } from "vitest";

vi.mock("sonner", () => {
  const base = Object.assign(vi.fn(), { error: vi.fn(), success: vi.fn() });
  return { toast: base };
});

import { toast as sonner } from "sonner";
import { toast } from "./toast";

describe("toast", () => {
  it("keeps errors until closed", () => {
    toast.error("boom");
    expect(sonner.error).toHaveBeenCalledWith("boom", { duration: Infinity, closeButton: true });
  });

  it("lets a call override the error options", () => {
    toast.error("boom", { duration: 8000 });
    expect(sonner.error).toHaveBeenLastCalledWith("boom", { duration: 8000, closeButton: true });
  });

  it("passes success through untouched", () => {
    toast.success("ok");
    expect(sonner.success).toHaveBeenCalledWith("ok");
  });
});
```

3. En `web/src/components/toaster.tsx`, sustituye el JSX de las líneas 10-13 por:

```tsx
    <Sonner
      position={desktop ? "bottom-right" : "top-center"}
      duration={3000}
      toastOptions={{
        classNames: {
          toast: "!rounded-2xl !border-border !bg-card !text-foreground !font-sans",
          actionButton: "!bg-primary !text-primary-foreground !font-semibold",
          closeButton: "!border-border !bg-card !text-foreground",
        },
      }}
    />
```

   y cambia el comentario de la línea 6 a:
   `/** Toasts at the top on phones and bottom right on desktop; success 3 s, errors until closed (docs/screens.md, global states). */`

4. En estos 17 archivos, cambia `import { toast } from "sonner";` por `import { toast } from "@/lib/toast";`. Solo la importación; las llamadas no cambian:
   `components/account/account-screen.tsx`, `components/admin/coverage-areas/coverage-screen.tsx`, `components/admin/finance/finance-screen.tsx`, `components/admin/jobs/assignment-tab.tsx`, `components/admin/jobs/job-actions.tsx`, `components/admin/painters/new-painter-screen.tsx`, `components/admin/painters/painter-screen.tsx`, `components/admin/settings/settings-screen.tsx`, `components/jobs/my-job-screen.tsx`, `components/painter/painter-job-screen.tsx`, `components/painter/painter-jobs-screen.tsx`, `components/painter/painter-payouts-screen.tsx`, `components/painter/painter-visit-screen.tsx`, `components/painter/photo-picker.tsx`, `components/payments/payment-screen.tsx`, `components/quote/photos-step.tsx`, `components/quote/schema-step.tsx` (todas bajo `web/src/`).

5. En `web/eslint.config.mjs`, añade dos objetos al array de `defineConfig([...])`, antes de `globalIgnores(...)`:

```js
  {
    rules: {
      "no-restricted-imports": ["error", { paths: [{ name: "sonner", importNames: ["toast"], message: "Import toast from @/lib/toast (durations from docs/screens.md)." }] }],
    },
  },
  { files: ["src/lib/toast.ts", "src/lib/toast.test.ts"], rules: { "no-restricted-imports": "off" } },
```

## Boundaries

- No cambies textos, tipos de toast ni dónde se lanzan.
- No cambies la posición ni el aspecto salvo el `closeButton` del paso 3.
- El toast de deshacer (`schema-step.tsx:82-85`) conserva su `duration: 5000`.
- No toques animaciones internas de Sonner: ya respeta `prefers-reduced-motion`.
- No añadas dependencias. Si `pnpm lint` encuentra otro import de `toast` desde `"sonner"` que no esté en la lista, cámbialo igual y anótalo en el informe.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde, incluido `toast.test.ts`. `grep -rn 'toast } from "sonner"' web/src` solo encuentra `web/src/lib/toast.ts`.
- **Feel check**:
  - Guarda los datos en Cuenta (éxito): el toast se va a los ~3 s.
  - Provoca un error (p. ej. en Fotos, sube un archivo `.txt` o de formato no admitido): el toast **no** se va solo y tiene una X para cerrarlo; al tocarla desaparece con la animación de Sonner.
  - Elimina una estancia: el toast de deshacer dura ~5 s y «Deshacer» funciona.
  - En móvil (390 px), desliza un toast de error hacia arriba: se descarta.
- **Done when**:
  - [ ] Éxito 3 s, deshacer 5 s y error hasta cerrarlo, con botón de cierre.
  - [ ] `grep -rn 'toast } from "sonner"' web/src` solo devuelve `web/src/lib/toast.ts`, y ESLint falla si se añade otro.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde, incluido `toast.test.ts`.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**: «Los toasts siguen las duraciones de docs/screens.md».
