#!/usr/bin/env python3
"""Generates src/OpenCodeUsagePlus/Resources/app.ico.

Pure standard library (no Pillow). Run from the repository root:

    python3 tools/make_icon.py
"""
from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

SIZES = (16, 20, 24, 32, 48, 64, 128, 256)
GREEN = (0x2F, 0x9E, 0x58)
TRACK = (0x8A, 0x8A, 0x93)
SWEEP_DEGREES = 360 * 0.7
SUPERSAMPLE = 4

ROOT = Path(__file__).resolve().parent.parent
OUT_ICO = ROOT / "src" / "OpenCodeUsagePlus" / "Resources" / "app.ico"


def render(size: int) -> bytearray:
    scale = SUPERSAMPLE
    width = size * scale
    center = (width - 1) / 2.0
    radius_out = width * 0.47
    thickness = width * 0.17
    radius_in = radius_out - thickness

    big = bytearray(width * width * 4)
    for y in range(width):
        dy = y - center
        row = y * width
        for x in range(width):
            dx = x - center
            distance = math.hypot(dx, dy)
            if radius_in <= distance <= radius_out:
                angle = (math.degrees(math.atan2(dy, dx)) + 90.0) % 360.0
                color = GREEN if angle <= SWEEP_DEGREES else TRACK
                index = (row + x) * 4
                big[index : index + 3] = bytes(color)
                big[index + 3] = 255

    out = bytearray(size * size * 4)
    block = scale * scale
    for y in range(size):
        for x in range(size):
            r = g = b = a = 0
            for sy in range(scale):
                base = ((y * scale + sy) * width + x * scale) * 4
                for sx in range(scale):
                    index = base + sx * 4
                    alpha = big[index + 3]
                    r += big[index] * alpha
                    g += big[index + 1] * alpha
                    b += big[index + 2] * alpha
                    a += alpha
            index = (y * size + x) * 4
            if a == 0:
                continue
            out[index] = r // a
            out[index + 1] = g // a
            out[index + 2] = b // a
            out[index + 3] = a // block
    return out


def png_bytes(size: int, rgba: bytes) -> bytes:
    stride = size * 4
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        raw += rgba[y * stride : (y + 1) * stride]

    def chunk(tag: bytes, data: bytes) -> bytes:
        checksum = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", checksum)

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b"")
    )


def main() -> None:
    images = {size: png_bytes(size, bytes(render(size))) for size in SIZES}

    count = len(SIZES)
    offset = 6 + count * 16
    directory = bytearray()
    payload = bytearray()
    for size in SIZES:
        data = images[size]
        directory += struct.pack(
            "<BBBBHHII",
            0 if size >= 256 else size,
            0 if size >= 256 else size,
            0,
            0,
            1,
            32,
            len(data),
            offset,
        )
        payload += data
        offset += len(data)

    OUT_ICO.parent.mkdir(parents=True, exist_ok=True)
    OUT_ICO.write_bytes(struct.pack("<HHH", 0, 1, count) + directory + payload)

    print(f"wrote {OUT_ICO} ({OUT_ICO.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
