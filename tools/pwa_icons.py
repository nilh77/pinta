"""Generates the PWA icons (docs/screens.md, PWA): a white "p" with the yellow dot of the wordmark on palette B teal.

Pure Python (zlib only). Run from the repository root: python3 tools/pwa_icons.py
"""
import math
import struct
import zlib
from pathlib import Path

OUT = Path("web/public/icons")
TEAL = (0x0F, 0x76, 0x6E)
WHITE = (0xFF, 0xFF, 0xFF)
YELLOW = (0xF2, 0xB7, 0x05)
SAMPLES = 4  # supersampling per axis, for smooth edges


def png(path, size, pixels):
    raw = b"".join(b"\x00" + bytes(row) for row in pixels)
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def color_at(x, y, scale):
    """The glyph in a unit square centred on (0.5, 0.5); scale < 1 keeps it inside the maskable safe zone."""
    u, v = (x - 0.5) / scale + 0.5, (y - 0.5) / scale + 0.5
    # Bowl of the "p": a ring.
    d = math.hypot(u - 0.52, v - 0.44)
    if 0.12 <= d <= 0.22:
        return WHITE
    # Stem, from the top of the bowl down past the baseline.
    if 0.30 <= u <= 0.40 and 0.22 <= v <= 0.86:
        return WHITE
    # The yellow dot of the wordmark.
    if math.hypot(u - 0.80, v - 0.74) <= 0.075:
        return YELLOW
    return TEAL


def icon(name, size, scale):
    rows = []
    for py in range(size):
        row = bytearray()
        for px in range(size):
            acc = [0, 0, 0]
            for sy in range(SAMPLES):
                for sx in range(SAMPLES):
                    c = color_at((px + (sx + 0.5) / SAMPLES) / size, (py + (sy + 0.5) / SAMPLES) / size, scale)
                    acc = [a + b for a, b in zip(acc, c)]
            row += bytes(round(a / SAMPLES ** 2) for a in acc)
        rows.append(row)
    png(OUT / name, size, rows)
    print(OUT / name)


OUT.mkdir(parents=True, exist_ok=True)
icon("icon-192.png", 192, 0.9)
icon("icon-512.png", 512, 0.9)
icon("icon-maskable-512.png", 512, 0.7)
icon("apple-touch-icon.png", 180, 0.8)
icon("favicon-32.png", 32, 1.0)
