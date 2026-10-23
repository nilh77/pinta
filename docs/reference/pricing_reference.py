"""Reference implementation of the Pinta pricing engine (PRD 6.3).

Used to compute the expected values of the golden test cases. The .NET
implementation (Pinta.Pricing) must produce exactly the same numbers.

All money and areas use Decimal with ROUND_HALF_UP (C#: MidpointRounding.AwayFromZero).
"""
from dataclasses import dataclass, field
from decimal import Decimal as D, ROUND_HALF_UP, ROUND_FLOOR, ROUND_CEILING

TWO = D("0.01")
ONE = D("0.1")


def r2(x: D) -> D:
    return x.quantize(TWO, rounding=ROUND_HALF_UP)


def r1(x: D) -> D:
    return x.quantize(ONE, rounding=ROUND_HALF_UP)


def floor5(x: D) -> D:
    return (x / 5).to_integral_value(rounding=ROUND_FLOOR) * 5


def ceil5(x: D) -> D:
    return (x / 5).to_integral_value(rounding=ROUND_CEILING) * 5


# ---------------------------------------------------------------- seed config
PAINTS = {
    "matte": dict(price=D("8.00"), coverage=D("10"), coats=2),
    "satin": dict(price=D("11.00"), coverage=D("10"), coats=2),
    "anti_damp": dict(price=D("16.00"), coverage=D("8"), coats=2),
    "exterior": dict(price=D("14.00"), coverage=D("9"), coats=2),
}
SURFACES = {
    "interior_wall": dict(client=D("6.00"), painter=D("4.00"), factor=D("1.00")),
    "ceiling": dict(client=D("7.00"), painter=D("4.80"), factor=D("1.15")),
    "facade": dict(client=D("12.00"), painter=D("8.50"), factor=D("1.40")),
    "wood": dict(client=D("9.00"), painter=D("6.00"), factor=D("1.20")),
    "metal": dict(client=D("10.00"), painter=D("7.00"), factor=D("1.30")),
}
OCCUPANCY = {"empty": D("1.00"), "furnished": D("1.10"), "inhabited": D("1.20")}
ROOM_SIZES = {  # (length, width) per size; height = DEFAULT_HEIGHT
    "living": {"S": ("4", "3"), "M": ("5", "4"), "L": ("6", "5")},
    "bedroom": {"S": ("3", "2.5"), "M": ("3.5", "3"), "L": ("4.5", "4")},
    "kitchen": {"S": ("3", "2"), "M": ("3.5", "3"), "L": ("4.5", "3.5")},
    "bathroom": {"S": ("2", "1.5"), "M": ("2.5", "2"), "L": ("3", "2.5")},
    "hallway": {"S": ("3", "1"), "M": ("5", "1.2"), "L": ("7", "1.2")},
    "other": {"S": ("2.5", "2.5"), "M": ("3", "3"), "L": ("4", "3.5")},
}
DEFAULT_HEIGHT = D("2.6")
WASTE = D("0.10")
VAT = D("0.23")
MIN_JOB_GROSS = D("150.00")  # with VAT
MAX_JOB_GROSS = D("15000.00")  # with VAT
PREMIUM_AREA = D("250")
RANGE_LOW = D("0.10")
RANGE_HIGH = D("0.15")
RANGE_EXTRA_SIZE = D("0.10")
RANGE_EXTRA_DAMAGE = D("0.10")
DEPOSIT = D("0.30")


@dataclass
class Element:
    kind: str  # "room" | "surface"
    name: str
    paint: str
    room_type: str = "other"
    size: str | None = None  # S/M/L, rooms only
    length: str | None = None
    width: str | None = None
    height: str | None = None
    walls: tuple = (True, True, True, True)  # A, B, C, D
    ceiling: bool = True
    surface_type: str = "interior_wall"  # surfaces only
    damage: bool = False


@dataclass
class Extra:
    name: str
    client: D
    painter: D


@dataclass
class Job:
    elements: list
    occupancy: str = "empty"
    extras: list = field(default_factory=list)
    manual_subtotal: D | None = None  # admin adjustment (rule "Ajuste manual"): replaces the subtotal
    site_visit_credit: D = D(0)  # paid deductible site visit, VAT included (rule "Visita técnica")


def line(area: D, paint: str, surface: str, occ: D):
    p, s = PAINTS[paint], SURFACES[surface]
    litres = r2(area * p["coats"] / p["coverage"] * (1 + WASTE))
    material = r2(litres * p["price"])
    labour = r2(area * s["client"] * s["factor"] * occ)
    painter = r2(area * s["painter"] * s["factor"] * occ)
    return dict(area=area, litres=litres, material=material, labour=labour, painter=painter)


def element_lines(e: Element, occ: D):
    out = []
    if e.kind == "surface":
        area = r2(D(e.length) * D(e.width))
        out.append(line(area, e.paint, e.surface_type, occ))
        return out
    if e.size:
        l, w = (D(v) for v in ROOM_SIZES[e.room_type][e.size])
        h = DEFAULT_HEIGHT
    else:
        l, w, h = D(e.length), D(e.width), D(e.height or DEFAULT_HEIGHT)
    lengths = [l, w, l, w]  # walls A, B, C, D
    wall_len = sum((x for x, on in zip(lengths, e.walls) if on), D(0))
    if wall_len > 0:
        out.append(line(r2(wall_len * h), e.paint, "interior_wall", occ))
    if e.ceiling:
        out.append(line(r2(l * w), e.paint, "ceiling", occ))
    return out


def quote(job: Job):
    occ = OCCUPANCY[job.occupancy]
    lines = [ln for e in job.elements for ln in element_lines(e, occ)]
    area = sum((ln["area"] for ln in lines), D(0))
    litres = sum((ln["litres"] for ln in lines), D(0))
    material = sum((ln["material"] for ln in lines), D(0))
    labour = sum((ln["labour"] for ln in lines), D(0))
    painter = sum((ln["painter"] for ln in lines), D(0))
    extras_client = sum((x.client for x in job.extras), D(0))
    extras_painter = sum((x.painter for x in job.extras), D(0))
    base = material + labour + extras_client
    min_net = r2(MIN_JOB_GROSS / (1 + VAT))
    if job.manual_subtotal is not None:
        # The admin fixed the subtotal: VAT, total, margin and deposit follow it; the minimum does not apply.
        minimum_applied = False
        subtotal = job.manual_subtotal
    else:
        minimum_applied = base < min_net
        subtotal = max(base, min_net)
    # A paid deductible visit comes off the subtotal without its VAT; the minimum is not applied again.
    subtotal -= r2(job.site_visit_credit / (1 + VAT))
    vat = r2(subtotal * VAT)
    total = subtotal + vat
    margin = subtotal - material - painter - extras_painter
    margin_pct = r1(margin / subtotal * 100)
    high = RANGE_HIGH
    if any(e.kind == "room" and e.size for e in job.elements):
        high += RANGE_EXTRA_SIZE
    if any(e.damage for e in job.elements):
        high += RANGE_EXTRA_DAMAGE
    over_max = total > MAX_JOB_GROSS
    rng = None if over_max else (floor5(total * (1 - RANGE_LOW)), ceil5(total * (1 + high)))
    deposit = r2(total * DEPOSIT)
    return dict(
        area=area, litres=litres, material=material, labour=labour,
        extras=extras_client, base=base, minimum_applied=minimum_applied,
        subtotal=subtotal, vat=vat, total=total, painter=painter + extras_painter,
        margin=margin, margin_pct=margin_pct, range=rng, over_max=over_max,
        premium=area > PREMIUM_AREA, deposit=deposit, balance=total - deposit,
    )


def refund(deposit: D, days_before: int | None) -> D:
    """Cancellation policy: 100% up to 7 days before the scheduled date (or no date yet), 50% after."""
    if days_before is None or days_before >= 7:
        return deposit
    return r2(deposit * D("0.5"))


if __name__ == "__main__":
    E = Element
    humidity_extra = Extra("Tratamiento anti-humedad", D("121.95"), D("75.00"))
    flat = [
        E("room", "Sala", "matte", "living", length="5", width="4"),
        E("room", "Cocina", "satin", "kitchen", length="3.5", width="3"),
        E("room", "Dormitorio 1", "matte", "bedroom", length="4", width="3.5"),
        E("room", "Dormitorio 2", "matte", "bedroom", size="M", walls=(True, True, True, False)),
        E("room", "Baño", "anti_damp", "bathroom", length="2.5", width="2", damage=True),
        E("room", "Pasillo", "matte", "hallway", length="6", width="1.2", ceiling=False),
    ]
    cases = {
        "TC01 Dormitorio exacto 4x3x2,6, 4 paredes + techo, mate, vacía":
            Job([E("room", "Dormitorio", "matte", "bedroom", length="4", width="3")]),
        "TC02 = TC01 con muebles":
            Job([E("room", "Dormitorio", "matte", "bedroom", length="4", width="3")], "furnished"),
        "TC03 = TC01 habitada":
            Job([E("room", "Dormitorio", "matte", "bedroom", length="4", width="3")], "inhabited"),
        "TC04 Dormitorio talla M, paredes A-B-C + techo, mate, vacía":
            Job([E("room", "Dormitorio 2", "matte", "bedroom", size="M", walls=(True, True, True, False))]),
        "TC05 Pared suelta 3x2,6, pared interior, mate (aplica mínimo)":
            Job([E("surface", "Pared", "matte", length="3", width="2.6")]),
        "TC06 Techo suelto 5x4, techo, mate":
            Job([E("surface", "Techo", "matte", length="5", width="4", surface_type="ceiling")]),
        "TC07 Baño exacto 2,5x2x2,4, anti-humedad, con daños":
            Job([E("room", "Baño", "anti_damp", "bathroom", length="2.5", width="2", height="2.4", damage=True)]),
        "TC08 Fachada 12x6, exterior":
            Job([E("surface", "Fachada", "exterior", length="12", width="6", surface_type="facade")]),
        "TC09 Vivienda de 6 estancias con muebles":
            Job(flat, "furnished"),
        "TC10 = TC09 + partida extra anti-humedad (tras revisión)":
            Job(flat, "furnished", [humidity_extra]),
        "TC11 Vivienda grande: 8 dormitorios L + sala L, vacía (Premium)":
            Job([E("room", f"Dormitorio {i}", "matte", "bedroom", size="L") for i in range(1, 9)]
                + [E("room", "Sala", "matte", "living", size="L")]),
        "TC12 Fachada 40x20, exterior (supera el máximo)":
            Job([E("surface", "Fachada", "exterior", length="40", width="20", surface_type="facade")]),
        "TC14 = TC10 con subtotal ajustado a mano a 2.500,00":
            Job(flat, "furnished", [humidity_extra], manual_subtotal=D("2500.00")),
        "TC15 = TC10 tras una visita técnica de 50,00 € descontable":
            Job(flat, "furnished", [humidity_extra], site_visit_credit=D("50.00")),
        # TC13: input validation (walls + ceiling all off) rejects this with 422 before the engine runs;
        # this script does not validate, so its printed numbers for TC13 are meaningless.
        "TC13 Habitación sin paredes ni techo (área 0)":
            Job([E("room", "Vacía", "matte", "bedroom", length="4", width="3", walls=(False,) * 4, ceiling=False)]),
    }
    for name, job in cases.items():
        q = quote(job)
        print(name)
        for k, v in q.items():
            print(f"   {k}: {v}")
    print("REFUNDS on deposit 300.00:", refund(D("300.00"), None), refund(D("300.00"), 7), refund(D("300.00"), 6), refund(D("300.00"), 0))
