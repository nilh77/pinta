"use client";

import { Check, DoorOpen, House, Lock, PaintRoller, ShieldCheck, SquareDashed } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useRouter } from "@/i18n/navigation";
import { startDraft, type Draft, type JobType } from "@/lib/quote/draft";
import { cn } from "@/lib/utils";
import { QuoteShell, type QuoteContext } from "./quote-shell";

const OPTIONS = [
  { type: "surface", icon: SquareDashed },
  { type: "room", icon: DoorOpen },
  { type: "home", icon: House },
] as const satisfies readonly { type: JobType; icon: unknown }[];

/** Screen 1 · Tipo de trabajo (board Tipo) [C2.1]. */
export function TypeStep() {
  const t = useTranslations("quote");
  return (
    <QuoteShell step={1} back={{ href: "/", label: t("type.back") }}>
      {(context) => <TypeChooser {...context} />}
    </QuoteShell>
  );
}

const hasWork = (draft: Draft | null) => !!draft && draft.elements.some((e) => e.described);
const schemaPath = (draft: Draft) =>
  draft.type !== "home" && draft.elements.length === 1 && !draft.elements[0].described ? `/cotizar/esquema?el=${draft.elements[0].id}` : "/cotizar/esquema";

/** Where "Continuar" takes a draft in progress. */
const resumePath = (draft: Draft) =>
  ({ estimate: "/cotizar/estimacion", photos: "/cotizar/fotos", address: "/cotizar/direccion", account: "/cotizar/cuenta" })[
    draft.step as "estimate" | "photos" | "address" | "account"
  ] ?? schemaPath(draft);

function TypeChooser({ draft, info, update, reset }: QuoteContext) {
  const t = useTranslations("quote.type");
  const router = useRouter();
  const [selected, setSelected] = useState<JobType | null>(draft?.type ?? null);
  const [confirming, setConfirming] = useState(false);
  const [showError, setShowError] = useState(false);

  const start = (type: JobType) => {
    const fresh = startDraft(type, info, Date.now());
    update(fresh);
    router.push(schemaPath(fresh));
  };

  const onContinue = () => {
    if (!selected) {
      setShowError(true);
      return;
    }
    if (draft && draft.type === selected) router.push(schemaPath(draft));
    else if (hasWork(draft)) setConfirming(true);
    else start(selected);
  };

  return (
    <div className="flex flex-1 flex-col pb-28 lg:pb-12">
      {hasWork(draft) && (
        <div role="status" className="mb-5 flex flex-col gap-3 rounded-2xl border border-accent bg-accent-soft p-4 text-accent-ink sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm font-semibold">{t("resume.title")}</p>
          <div className="flex gap-2">
            <Button size="sm" onClick={() => router.push(resumePath(draft!))}>
              {t("resume.continue")}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => { reset(); setSelected(null); }}>
              {t("resume.restart")}
            </Button>
          </div>
        </div>
      )}

      <h1 className="text-[28px] leading-tight font-bold tracking-tight">{t("title")}</h1>
      <p className="mt-2 text-sm text-muted-foreground">{t("subtitle")}</p>

      <div role="radiogroup" aria-label={t("title")} aria-describedby={showError ? "type-error" : undefined} className="mt-5 flex flex-col gap-3">
        {OPTIONS.map(({ type, icon: Icon }) => (
          <button
            key={type}
            type="button"
            role="radio"
            aria-checked={selected === type}
            onClick={() => {
              setSelected(type);
              setShowError(false);
            }}
            className={cn(
              "flex items-center gap-3.5 rounded-[18px] border-[1.5px] border-border bg-card p-3.5 text-left transition-colors motion-reduce:transition-none",
              "aria-checked:border-2 aria-checked:border-primary aria-checked:bg-primary-soft focus-visible:ring-[3px] focus-visible:ring-ring/60 focus-visible:outline-none",
            )}
          >
            <span aria-hidden="true" className="flex size-16 shrink-0 items-center justify-center rounded-[14px] bg-background text-primary">
              <Icon className="size-8" strokeWidth={1.6} />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-base font-semibold">{t(`options.${type}.title`)}</span>
              <span className="mt-0.5 block text-[13px] text-muted-foreground">{t(`options.${type}.body`)}</span>
            </span>
            <span
              aria-hidden="true"
              className={cn(
                "flex size-[22px] shrink-0 items-center justify-center rounded-full border-2",
                selected === type ? "border-primary bg-primary text-primary-foreground" : "border-[#b9c9c4]",
              )}
            >
              {selected === type && <Check className="size-3" strokeWidth={3.2} />}
            </span>
          </button>
        ))}
      </div>
      {showError && (
        <p id="type-error" role="alert" className="mt-2 text-sm font-medium text-destructive">
          {t("required")}
        </p>
      )}

      <ul className="mt-6 grid grid-cols-3 gap-2 rounded-2xl border border-border bg-card p-4">
        {(
          [
            { key: "material", icon: PaintRoller },
            { key: "painters", icon: ShieldCheck },
            { key: "payment", icon: Lock },
          ] as const
        ).map(({ key, icon: Icon }) => (
          <li key={key} className="flex flex-col items-center gap-1.5 text-center">
            <span aria-hidden="true" className="flex size-9 items-center justify-center rounded-full bg-primary-soft text-primary">
              <Icon className="size-[18px]" />
            </span>
            <span className="text-xs leading-tight font-medium">{t(`trust.${key}`)}</span>
          </li>
        ))}
      </ul>

      <div className="fixed inset-x-0 bottom-0 bg-background/95 px-4 pt-3 pb-[max(1.5rem,env(safe-area-inset-bottom))] backdrop-blur sm:px-6 lg:static lg:mt-6 lg:bg-transparent lg:p-0 lg:backdrop-blur-none">
        <div className="mx-auto max-w-2xl">
          <Button size="lg" className="w-full" onClick={onContinue}>
            {t("continue")}
          </Button>
        </div>
      </div>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t("confirm.title")}
        description={t("confirm.body")}
        confirmLabel={t("confirm.yes")}
        cancelLabel={t("confirm.no")}
        onConfirm={() => selected && start(selected)}
      />
    </div>
  );
}
