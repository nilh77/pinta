import type { components } from "@/lib/api/schema";
import { centsFromInput, parseDecimal } from "@/lib/numbers";

type Schemas = components["schemas"];
export type SimulateRequest = Schemas["SimulateRequest"];
export type QuoteBreakdown = Schemas["QuoteBreakdownDto"];
export type Occupancy = Schemas["Occupancy"];
export type RoomSize = Schemas["RoomSize"];

/** One element of the simulator's sample job, as typed. Rooms use a size or exact measures. */
export type SampleElement = {
  kind: Schemas["ElementKind"];
  roomTypeId: string;
  surfaceTypeId: string;
  size: RoomSize | "";
  length: string;
  width: string;
  height: string;
  walls: [boolean, boolean, boolean, boolean];
  ceiling: boolean;
  paintId: string;
  damage: boolean;
};

export type SampleExtra = { description: string; clientPrice: string; painterPay: string };

export type SampleJob = { occupancy: Occupancy; elements: SampleElement[]; extras: SampleExtra[] };

export const newRoom = (): SampleElement => ({
  kind: "room",
  roomTypeId: "bedroom",
  surfaceTypeId: "interior_wall",
  size: "",
  length: "4",
  width: "3",
  height: "",
  walls: [true, true, true, true],
  ceiling: true,
  paintId: "matte",
  damage: false,
});

export const newSurface = (): SampleElement => ({
  ...newRoom(),
  kind: "surface",
  length: "3",
  width: "2,6",
  walls: [false, false, false, false],
  ceiling: false,
});

/** Starts as golden case TC01 (bedroom 4 × 3, matte, empty), the board's example. */
export const defaultSampleJob = (): SampleJob => ({ occupancy: "empty", elements: [newRoom()], extras: [] });

const measure = (text: string) => (text.trim() === "" ? null : parseDecimal(text));

/** The sample job as the API expects it. Unparseable numbers go as null so the API reports them per field. */
export function toSimulateRequest(job: SampleJob): SimulateRequest {
  return {
    occupancy: job.occupancy,
    elements: job.elements.map((e) =>
      e.kind === "room"
        ? {
            kind: "room",
            name: null,
            roomTypeId: e.roomTypeId,
            size: e.size || null,
            length: e.size ? null : measure(e.length),
            width: e.size ? null : measure(e.width),
            height: e.size ? null : measure(e.height),
            walls: e.walls,
            ceiling: e.ceiling,
            surfaceTypeId: null,
            paintId: e.paintId,
            damage: e.damage,
          }
        : {
            kind: "surface",
            name: null,
            roomTypeId: null,
            size: null,
            length: measure(e.length),
            width: measure(e.width),
            height: null,
            walls: null,
            ceiling: false,
            surfaceTypeId: e.surfaceTypeId,
            paintId: e.paintId,
            damage: e.damage,
          },
    ),
    extras: job.extras.map((x) => ({
      description: x.description.trim(),
      clientPriceCents: centsFromInput(x.clientPrice) ?? -1,
      painterPayCents: centsFromInput(x.painterPay) ?? -1,
    })),
  };
}
