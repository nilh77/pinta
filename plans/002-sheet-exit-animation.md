# 002 — Mantener montada la hoja del editor mientras se cierra, para que se vea su salida

- **Status**: DONE (2026-11-11)
- **Commit**: 02c3f9b
- **Severity**: HIGH
- **Category**: Interruptibility / continuidad espacial
- **Estimated scope**: 1 archivo (`web/src/components/quote/schema-step.tsx`), unas 20 líneas
- **Sistema**: `plans/000-motion-system.md` §2 principio 2 y §5 «Cerrar la hoja».

## Problem

En móvil y tableta (< 1024 px), el editor de estancia es una hoja (`Sheet`) que se abre con `?el={id}`. La hoja se monta **solo** mientras hay elemento seleccionado y siempre con `open` fijo:

```tsx
// web/src/components/quote/schema-step.tsx:215-217 — current
      {selected && (
        <SheetEditor key={selected.id} element={selected} name={names[elements.indexOf(selected)]} context={context} onClose={close} onRename={() => setRenaming(selected.id)} />
      )}
```

```tsx
// web/src/components/quote/schema-step.tsx:252-254 — current
    <Sheet
      open
      onOpenChange={(open) => !open && onClose()}
```

Al pulsar Guardar, Cancelar, la X o Escape, `close()` quita `?el=` de la URL, `selected` pasa a `undefined` y React desmonta la hoja **en el mismo render**. La animación de salida que define `web/src/components/ui/sheet.tsx:32` (`data-[state=closed]:animate-out … slide-out-to-bottom`) nunca llega a ejecutarse: hoja y velo desaparecen de golpe. Es la superficie más usada del constructor y la entrada sí anima, así que la asimetría se nota en cada estancia editada.

Radix Dialog sí sabe animar la salida: cuando `open` pasa a `false`, mantiene el contenido montado hasta que acaba la animación `animate-out`. Solo hace falta no desmontar el componente.

## Target

- `SheetEditor` permanece montado después de cerrar, con `open={false}`, para que Radix reproduzca la salida.
- Cada apertura vuelve a montar el editor con un estado de trabajo limpio. Si el usuario cancela y vuelve a abrir la misma estancia, no deben reaparecer los cambios cancelados.
- Si la estancia abierta se elimina, la hoja puede desaparecer sin animación (caso límite aceptado).

## Repo conventions to follow

- Componentes de función con hooks al principio. En `SchemaBuilder` (`schema-step.tsx:46-59`) los `useState` van antes del `if (!draft) return null;` de la línea 63.
- `selected` se calcula en la línea 69: `const selected = elements.find((e) => e.id === selectedParam);`.
- Para guardar información del render anterior, el patrón es el de la documentación de React («Storing information from previous renders»): un `setState` condicionado durante el render, sin `useEffect`.

## Steps

1. En `SchemaBuilder`, después de `const [renaming, setRenaming] = useState<string | null>(null);` (línea 55), añade:

```tsx
  // The sheet stays mounted while it closes so its exit animation plays; `session` remounts the editor on every opening.
  const [sheet, setSheet] = useState<{ id: string; session: number; open: boolean } | null>(null);
```

2. Justo después de `const editing = desktop ? (selected ?? elements[0]) : selected;` (línea 71), añade:

```tsx
  if (selected && (!sheet?.open || sheet.id !== selected.id)) {
    setSheet({ id: selected.id, session: (sheet?.session ?? 0) + 1, open: true });
  } else if (!selected && sheet?.open) {
    setSheet({ ...sheet, open: false });
  }
  const sheetElement = sheet ? elements.find((e) => e.id === sheet.id) : undefined;
```

3. Sustituye el bloque de las líneas 215-217 por:

```tsx
      {sheet && sheetElement && (
        <SheetEditor
          key={`${sheet.id}-${sheet.session}`}
          open={sheet.open}
          element={sheetElement}
          name={names[elements.indexOf(sheetElement)]}
          context={context}
          onClose={close}
          onRename={() => setRenaming(sheetElement.id)}
        />
      )}
```

4. En `SheetEditor` (línea 239), añade la prop `open` a la firma y pásala a `Sheet`:

```tsx
function SheetEditor({ element, name, context, open, onClose, onRename }: EditorProps & { open: boolean; onClose: () => void }) {
```

y en la línea 253 cambia `open` (booleano implícito) por `open={open}`.

5. No cambies `InlineEditor` (escritorio ≥ 1024 px): allí no hay hoja.

## Boundaries

- Solo `web/src/components/quote/schema-step.tsx`. No toques `web/src/components/ui/sheet.tsx` (eso es el plan 003) ni `room-editor.tsx`.
- No cambies cómo se abre (`open(id)`, línea 73) ni la URL `?el=`.
- No uses `useEffect` ni refs leídas durante el render para esto. `eslint-plugin-react-hooks` 7 las marca.
- Si `pnpm lint` marca `react-hooks/set-state-in-render` (u otra regla del compilador) en el paso 2, PARA e informa con el mensaje exacto; no lo silencies con `eslint-disable`.
- Si el código no coincide con los extractos (cambios desde 02c3f9b), PARA e informa.

## Verification

- **Mechanical**: `cd web && pnpm lint && pnpm typecheck && pnpm test`: todo en verde.
- **Feel check** (viewport 390 px; con la API en marcha mediante `dotnet run --project src/Pinta.AppHost`, o en el despliegue de Railway tras publicar):
  - Cotizar → elige tipo → añade una estancia. La hoja sube. Pulsa **Cancelar**: la hoja **baja** y el velo se desvanece; no desaparecen de golpe.
  - Repite con **Guardar**, con la X, con Escape (teclado físico) y tocando el velo: en todos los casos hay salida animada.
  - Cambia la anchura de una estancia, pulsa Cancelar y vuelve a abrir la **misma** estancia: el valor es el guardado, no el cancelado.
  - Abre la estancia A, ciérrala y abre la B enseguida: la B aparece con sus datos, sin restos de A.
  - Elimina desde el menú ⋯ la estancia que está abierta (si es posible) o abre con `?el=` una que luego eliminas: no hay errores en consola.
  - En DevTools → Animations, a velocidad 10 %: la salida empieza en la posición abierta y termina fuera de la pantalla.
- **Done when**:
  - [ ] Guardar, Cancelar, la X, Escape y el velo muestran la salida de 240 ms (revisión 2).
  - [ ] Reabrir una estancia parte siempre del estado guardado; cambiar de estancia no deja restos.
  - [ ] `pnpm lint && pnpm typecheck && pnpm test` en verde, sin `eslint-disable` nuevos.
  - [ ] Entrada en `CHANGELOG.md` bajo **Sin publicar**: «La hoja del editor se cierra con animación (C2.3)».
