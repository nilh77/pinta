import type { ReactNode } from "react";
import { PainterArea } from "@/components/painter/painter-area";

export default function PainterLayout({ children }: { children: ReactNode }) {
  return <PainterArea>{children}</PainterArea>;
}
