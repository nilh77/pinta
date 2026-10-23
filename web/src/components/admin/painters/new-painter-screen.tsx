"use client";

import { ArrowLeft } from "lucide-react";
import { useTranslations } from "next-intl";
import { toast } from "sonner";
import { Link, useRouter } from "@/i18n/navigation";
import { useCreatePainter } from "@/lib/painter/use-admin-painters";
import { PainterForm } from "./painter-form";

/** A4.2: the admin creates the painter and the invitation goes out (72 h). */
export function NewPainterScreen() {
  const t = useTranslations("admin.painters");
  const router = useRouter();
  const create = useCreatePainter();

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <Link href="/admin/pintores" className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-primary">
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t("back")}
      </Link>
      <header>
        <h1 className="text-[28px] font-extrabold tracking-tight">{t("new.title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("new.subtitle")}</p>
      </header>
      <section className="rounded-[20px] border border-border bg-card p-4">
        <PainterForm
          pending={create.isPending}
          error={create.error}
          submitLabel={t("new.submit")}
          onSubmit={(values) =>
            create.mutate(
              {
                name: values.name,
                email: values.email,
                locale: values.locale,
                phone: values.phone,
                taxId: values.taxId,
                specialties: values.specialties,
                municipalities: values.municipalities,
                iban: values.iban,
                notes: values.notes,
              },
              {
              onSuccess: (painter) => {
                toast.success(t("new.sent", { email: painter.email }));
                router.push(`/admin/pintores/${painter.id}`);
                },
              },
            )
          }
        />
      </section>
    </div>
  );
}
