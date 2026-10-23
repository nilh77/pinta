import type { ReactNode } from "react";
import { ClientArea } from "@/components/jobs/client-area";

export default function MyJobsLayout({ children }: { children: ReactNode }) {
  return <ClientArea>{children}</ClientArea>;
}
