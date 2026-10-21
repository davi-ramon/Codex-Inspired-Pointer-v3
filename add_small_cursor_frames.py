"""Add native Windows 32–96 px entries to an existing generated package.

The 128/256 px source frames are preserved byte-for-byte. This migration is
faster than redrawing every preset and keeps old artwork and motion frames.
"""
from __future__ import annotations

import argparse
import io
import struct
from concurrent.futures import ProcessPoolExecutor, as_completed
from functools import lru_cache
from pathlib import Path
from PIL import Image, ImageChops


SMALL = (32, 48, 64, 96)


@lru_cache(maxsize=8)
def fade_mask(n: int) -> Image.Image:
    width = max(3, n // 8)
    values = []
    for y in range(n):
        for x in range(n):
            distance = min(x, y, n - 1 - x, n - 1 - y)
            t = min(1.0, distance / width)
            values.append(round(255 * t * t * (3 - 2 * t)))
    mask = Image.new("L", (n, n))
    mask.putdata(values)
    return mask


def fade(image: Image.Image) -> Image.Image:
    image.putalpha(ImageChops.multiply(image.getchannel("A"), fade_mask(image.width)))
    return image


def repack_cur(data: bytes) -> bytes:
    if struct.unpack_from("<HH", data, 0) != (0, 2):
        raise ValueError("Invalid CUR header")
    count = struct.unpack_from("<H", data, 4)[0]
    entries = {}
    for index in range(count):
        width, height, _, _, hot_x, hot_y, length, offset = struct.unpack_from(
            "<BBBBHHII", data, 6 + 16 * index
        )
        n = width or 256
        if n != (height or 256) or offset + length > len(data):
            raise ValueError("Invalid CUR entry")
        entries[n] = (data[offset:offset + length], hot_x, hot_y)
    if 128 not in entries or 256 not in entries:
        raise ValueError("Missing high-resolution CUR entries")
    full_bytes, full_x, full_y = entries[256]
    full = Image.open(io.BytesIO(full_bytes)).convert("RGBA")
    if full.size != (256, 256):
        raise ValueError("Expected 256 px PNG")
    for n in SMALL:
        crop = n * 2
        left = (256 - crop) // 2
        top = left
        if full_x < left + 4:
            left = max(0, full_x - 4)
        elif full_x > left + crop - 4:
            left = min(256 - crop, full_x - crop + 4)
        if full_y < top + 4:
            top = max(0, full_y - 4)
        elif full_y > top + crop - 4:
            top = min(256 - crop, full_y - crop + 4)
        image = full.crop((left, top, left + crop, top + crop)).resize(
            (n, n), Image.Resampling.LANCZOS
        )
        fade(image)
        output = io.BytesIO()
        image.save(output, "PNG", compress_level=1)
        entries[n] = (output.getvalue(), round((full_x - left) / 2),
                      round((full_y - top) / 2))
    sizes = (*SMALL, 128, 256)
    result = bytearray(struct.pack("<HHH", 0, 2, len(sizes)))
    offset = 6 + len(sizes) * 16
    parts = []
    for n in sizes:
        blob, hot_x, hot_y = entries[n]
        result += struct.pack("<BBBBHHII", n if n < 256 else 0,
                              n if n < 256 else 0, 0, 0, hot_x, hot_y,
                              len(blob), offset)
        parts.append(blob)
        offset += len(blob)
    return bytes(result) + b"".join(parts)


def chunk(kind: bytes, payload: bytes) -> bytes:
    return kind + struct.pack("<I", len(payload)) + payload + (b"\0" if len(payload) & 1 else b"")


def rewrite_chunks(data: bytes) -> bytes:
    out = bytearray()
    pos = 0
    while pos + 8 <= len(data):
        kind = data[pos:pos + 4]
        length = struct.unpack_from("<I", data, pos + 4)[0]
        body = data[pos + 8:pos + 8 + length]
        if len(body) != length:
            raise ValueError("Truncated RIFF chunk")
        if kind == b"icon":
            body = repack_cur(body)
        elif kind == b"LIST":
            body = body[:4] + rewrite_chunks(body[4:])
        out += chunk(kind, body)
        pos += 8 + length + (length & 1)
    if pos != len(data):
        raise ValueError("RIFF padding error")
    return bytes(out)


def repack_ani(data: bytes) -> bytes:
    if data[:4] != b"RIFF" or data[8:12] != b"ACON":
        raise ValueError("Invalid ANI")
    body = b"ACON" + rewrite_chunks(data[12:])
    return b"RIFF" + struct.pack("<I", len(body)) + body


def update_preset(preset: Path) -> tuple[str, int]:
    assets = preset / "assets"
    cursors = list(assets.glob("*.cur")) + list((assets / "motion").glob("*.cur"))
    for path in cursors:
        path.write_bytes(repack_cur(path.read_bytes()))
    for path in assets.glob("*.ani"):
        path.write_bytes(repack_ani(path.read_bytes()))
    return preset.name, len(cursors)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    presets = sorted((args.package / "presets").glob("s*-b*"))
    if len(presets) != 21:
        raise ValueError(f"Expected 21 presets; found {len(presets)}")
    total = 0
    with ProcessPoolExecutor(max_workers=4) as executor:
        jobs = [executor.submit(update_preset, preset) for preset in presets]
        for future in as_completed(jobs):
            name, count = future.result()
            total += count
            print(name, count, "CUR files and wait animations updated", flush=True)
    print(f"Updated {total} CUR files across {len(presets)} presets")


if __name__ == "__main__":
    main()
