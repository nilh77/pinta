import { useFormatter, useTranslations } from "next-intl";

/** Decorative preview of the home layout builder (board "Inicio"); the real one arrives in H2. */
export function HouseIllustration() {
  const t = useTranslations("home.illustration");
  const format = useFormatter();
  const rooms = [t("kitchen"), t("bathroom"), t("bedroom")];
  const money = (euros: number) => format.number(euros, { style: "currency", currency: "EUR", maximumFractionDigits: 0 });

  return (
    <figure aria-label={t("label")} className="relative rounded-3xl border border-border bg-card px-5 pt-5 pb-8">
      <svg viewBox="0 0 308 34" aria-hidden="true" className="block h-auto w-full">
        <rect x="228" y="8" width="16" height="20" fill="var(--accent-soft)" stroke="var(--accent)" strokeWidth="3" />
        <polygon points="6,32 154,4 302,32" fill="var(--accent-soft)" stroke="var(--accent)" strokeWidth="3" strokeLinejoin="round" />
      </svg>
      <div aria-hidden="true" className="grid grid-cols-2 gap-1.5 rounded-b-[14px] border-[3px] border-t-0 border-accent p-2.5">
        <span className="col-span-2 flex h-11 items-center rounded-[10px] border-[1.5px] border-primary bg-primary-soft px-2.5 text-xs font-semibold">
          {t("living")}
        </span>
        {rooms.map((room) => (
          <span key={room} className="flex h-11 items-center rounded-[10px] border-[1.5px] border-primary bg-primary-soft px-2.5 text-xs font-semibold">
            {room}
          </span>
        ))}
        <span className="flex h-11 items-center rounded-[10px] border-[1.5px] border-dashed border-[#7c918b] bg-card px-2.5 text-xs font-semibold text-muted-foreground">
          + {t("add")}
        </span>
      </div>
      <figcaption className="absolute right-3.5 -bottom-5 rounded-[14px] bg-foreground px-3.5 py-2.5 text-white">
        <span className="block text-[11px] text-[#c9d6d2]">{t("estimate")}</span>
        <span className="font-display text-[17px] font-bold">
          {money(1270)} – {money(1620)}
        </span>
      </figcaption>
    </figure>
  );
}
