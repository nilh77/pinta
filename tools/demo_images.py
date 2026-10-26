"""Generates the demo photos (docs/plan.md, H8): simple illustrations of rooms in Pinta's palette, no licences involved.

Pure Python (zlib only). Run from the repository root: python3 tools/demo_images.py
"""
import struct
import zlib
from pathlib import Path

W, H = 800, 600
OUT = Path("src/Pinta.Api/Seed/Images")


def png(path, pixels):
    raw = b"".join(b"\x00" + bytes(row) for row in pixels)
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def canvas(color):
    return [bytearray(color * W) for _ in range(H)]


def rect(img, x0, y0, x1, y1, color):
    for y in range(max(0, y0), min(H, y1)):
        img[y][max(0, x0) * 3:min(W, x1) * 3] = bytes(color) * (min(W, x1) - max(0, x0))


def blob(img, cx, cy, rx, ry, color):
    for y in range(max(0, cy - ry), min(H, cy + ry)):
        for x in range(max(0, cx - rx), min(W, cx + rx)):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1:
                img[y][x * 3:x * 3 + 3] = bytes(color)


def room(wall, floor=(176, 140, 104), window=True, door=False):
    img = canvas(wall)
    rect(img, 0, 470, W, H, floor)                      # floor
    rect(img, 0, 455, W, 470, (240, 236, 228))          # skirting
    if window:
        rect(img, 470, 110, 690, 330, (210, 228, 236))  # glass
        rect(img, 470, 215, 690, 225, (250, 250, 248))
        rect(img, 575, 110, 585, 330, (250, 250, 248))
    if door:
        rect(img, 90, 170, 230, 455, (140, 104, 72))
    return img


def save(name, img):
    png(OUT / name, img)
    print(OUT / name)


OUT.mkdir(parents=True, exist_ok=True)
# Request photos (PT-0101): the bedroom as the client sees it, with a damp patch and a worn wall.
img = room((214, 206, 190)); blob(img, 250, 260, 90, 60, (176, 168, 140)); blob(img, 280, 300, 50, 35, (150, 142, 116)); save("request-1.png", img)
img = room((214, 206, 190), window=False, door=True); save("request-2.png", img)
img = canvas((226, 220, 208)); blob(img, 400, 300, 160, 110, (190, 182, 160)); save("request-3.png", img)       # ceiling stain
img = room((200, 194, 180)); rect(img, 120, 140, 125, 420, (150, 146, 136)); save("request-4.png", img)          # crack
# Progress photos (PT-0107): half the ceiling painted.
img = canvas((222, 218, 210)); rect(img, 0, 0, 430, H, (248, 247, 243)); rect(img, 425, 0, 440, H, (236, 234, 228)); save("progress-1.png", img)
img = canvas((222, 218, 210)); rect(img, 0, 0, 700, H, (248, 247, 243)); save("progress-2.png", img)
# Result photos (PT-0108): the bedroom freshly painted.
save("result-1.png", room((236, 240, 234)))
save("result-2.png", room((236, 240, 234), window=False, door=True))
save("result-3.png", room((15, 118, 110), floor=(176, 140, 104)))                                                 # accent wall in palette B
