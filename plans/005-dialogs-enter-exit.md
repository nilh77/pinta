# 005 — Diálogos: entrada y salida con fundido y zoom ligero, iguales en toda la app

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: MEDIUM
- **Category**: Cohesion & tokens
- **Estimated scope**: 5 archivos, unas 15 líneas
- **Depends on**: 001 (`overlayMotion` y `dialogMotion` en `web/src/components/ui/motion.ts`)
- **Sistema**: `plans/000-motion-system.md` §4 `overlayMotion`/`dialogMotion` y §5 «Diálogo centrado» y «Visor de fotos».

## Problem

Los diálogos centrados aparecen y desaparecen sin transición, mientras la hoja y los menús sí animan: son dos lenguajes para la misma familia de capas. Ubicaciones (código actual):

```tsx
// web/src/components/ui/confirm-dialog.tsx:22-23
        <AlertDialog.Overlay className="fixed inset-0 z-40 bg-foreground/40" />
        <AlertDialog.Content className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl">

// web/src/components/ui/form-dialog.tsx:44-47
        <Dialog.Overlay className="fixed inset-0 z-40 bg-foreground/40" />
        <Dialog.Content
          className="fixed top-1/2 left-1/2 z-50 max-h-[90dvh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-3xl bg-card p-6 shadow-xl"
        >

// web/src/components/global-states.tsx:60-61 (sesión caducada)
        <AlertDialog.Overlay className="fixed inset-0 z-40 bg-foreground/40" />
        <AlertDialog.Content className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl">

// web/src/components/quote/schema-step.tsx:317-321 (renombrar)
        <Dialog.Overlay className="fixed inset-0 z-50 bg-foreground/40" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-sm -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl"
        >

// web/src/components/admin/jobs/job-files.tsx:41-44 (visor de fotos)
          <Dialog.Overlay className="fixed inset-0 z-40 bg-black/90" />
          <Dialog.Content
            aria-describedby={undefined}
            className="fixed inset-0 z-50 flex items-center justify-center p-4"
```

## Target

- Velo: fundido con `ease-out`, 340 ms al entrar y 180 ms al salir (`overlayMotion`, revisión 2), con `style={overlayStyle(open)}` para que no se coma los toques mientras se va (helper del plan 003).
- Contenido de los modales centrados (`dialogMotion`, revisión 2): entra desde el **centro** con fundido, escala 0.9 y 16 px de subida, con `ease-spring` en 340 ms (llega en ~130 ms); sale con fundido y escala 0.95, `ease-out` en 180 ms. El origen centrado es correcto en modales.
- Visor de fotos: solo fundido **al abrir** y cierre instantáneo. Al cerrar, `current` pasa a `null` y la imagen se vaciaría a mitad de la salida, así que una salida animada mostraría un fondo negro vacío desvaneciéndose.

El centrado (`-translate-x-1/2 -translate-y-1/2`) usa la propiedad CSS `translate` en Tailwind v4 y el keyframe anima `transform`: no se pisan, el diálogo sigue centrado durante la animación.

## Repo conventions to follow

- `cn()` de `@/lib/utils` para componer.
- Constantes en `web/src/components/ui/motion.ts` (plan 001): `overlayMotion`, `dialogMotion`.

## Steps

> Revisión 2: en los pasos 1–4, además de las clases, cada `Overlay` recibe `style={overlayStyle(<abierto>)}` (importado de `motion.ts`): `open` en ConfirmDialog, FormDialog y RenameDialog; `from !== null` en SessionExpiredDialog. El visor (paso 5) no lo necesita: cierra sin animación, así que su velo se desmonta al instante.

1. `web/src/components/ui/confirm-dialog.tsx`: importa `cn` (`import { cn } from "@/lib/utils";`) y `import { dialogMotion, overlayMotion } from "./motion";`. Después:
   - Línea 22: `className={cn("fixed inset-0 z-40 bg-foreground/40", overlayMotion)}`
   - Línea 23: `className={cn("fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl", dialogMotion)}`
2. `web/src/components/ui/form-dialog.tsx`: mismas importaciones. Línea 44 igual que el paso 1. Línea 46: `className={cn("fixed top-1/2 left-1/2 z-50 max-h-[90dvh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-3xl bg-card p-6 shadow-xl", dialogMotion)}`.
3. `web/src/components/global-states.tsx`: importa `cn` y `import { dialogMotion, overlayMotion } from "@/components/ui/motion";`. Líneas 60-61 como en el paso 1.
4. `web/src/components/quote/schema-step.tsx` (`RenameDialog`): importa `cn` (`import { cn } from "@/lib/utils";`; hoy no está) y `import { dialogMotion, overlayMotion } from "@/components/ui/motion";`. Línea 317: `className={cn("fixed inset-0 z-50 bg-foreground/40", overlayMotion)}`. Línea 320: `className={cn("fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-sm -translate-x-1/2 -translate-y-1/2 rounded-3xl bg-card p-6 shadow-xl", dialogMotion)}`.
5. `web/src/components/admin/jobs/job-files.tsx` (sin importaciones nuevas: son cadenas literales). Línea 41: `className="fixed inset-0 z-40 bg-black/90 ease-out data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-layer-in)"`. Línea 44: `className="fixed inset-0 z-50 flex items-center justify-center p-4 ease-out data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:duration-(--duration-layer-in)"`. Sin `animate-out`: cierre instantáneo a propósito.

## Boundaries

- Solo clases de los `Overlay` y `Content` listados. No cambies la estructura, los textos ni los handlers.
- `RenameDialog` usa `key={renaming ?? "none"}` (`schema-step.tsx:166`): al cerrar se vuelve a montar y su salida no se verá. Es aceptable; **no** cambies esa `key` en este plan.
- No conviertas los diálogos en hojas ni cambies sus z-index.
- Si algún extracto no coincide, PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde.
- **Feel check**:
  - Cotizar → cambia el tipo de trabajo con un borrador existente (`ConfirmDialog` de `type-step.tsx:155`): el diálogo sube y se asienta desde el centro con un rebote ligero; al cancelar, sale más rápido de lo que entró.
  - Admin → un trabajo → «Rechazar» o «Cancelar» (`FormDialog`): igual, y el foco va al primer campo como antes.
  - Renombrar una estancia: entra animado.
  - Admin → Archivos de un trabajo → abre una foto: el visor aparece con fundido; al cerrar desaparece al instante y sin parpadeo de imagen vacía.
  - Al 10 % en DevTools → Animations: el diálogo permanece centrado todo el tiempo (sin deriva hacia abajo ni a la derecha).
  - Con `prefers-reduced-motion: reduce`: solo fundido, sin zoom.
- **Done when**:
  - [ ] Confirmar, formulario, renombrar y sesión caducada: entrada con resorte (escala 0.9 y 16 px, 340 ms) y salida de 180 ms; velo y diálogo juntos; el velo deja pasar los toques al salir.
  - [ ] El visor de fotos solo anima al abrir.
  - [ ] Ningún diálogo se descentra durante la animación; el foco va al mismo sitio que antes.
  - [ ] Con movimiento reducido: solo fundido.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**.
