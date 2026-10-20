"""Extract lossless 256px art from CUR/ANI files for the live motion helper.

Run after build_package.py. This does not resize the already drawn artwork.
"""
from __future__ import annotations

import argparse
import struct
from pathlib import Path
import build_package as art

ROOT = Path(__file__).resolve().parent
STATIC = ("arrow", "ibeam", "crosshair", "uparrow", "sizenwse", "sizenesw",
          "sizewe", "sizens", "sizeall", "no", "hand", "help", "pen", "pin", "person")


def cur_image(data: bytes) -> tuple[bytes, int, int]:
    if len(data) < 22 or struct.unpack_from("<HH", data, 0) != (0, 2):
        raise ValueError("Invalid CUR frame")
    count = struct.unpack_from("<H", data, 4)[0]
    entries = []
    for i in range(count):
        pos = 6 + i * 16
        width, height, _, _, hot_x, hot_y, length, offset = struct.unpack_from(
            "<BBBBHHII", data, pos)
        width = width or 256
        height = height or 256
        entries.append((width * height, length, offset, hot_x, hot_y))
    _, length, offset, hot_x, hot_y = max(entries)
    png = data[offset:offset + length]
    if not png.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("Expected PNG-backed CUR")
    return png, hot_x, hot_y


def ani_frames(data: bytes) -> list[bytes]:
    if not data.startswith(b"RIFF") or data[8:12] != b"ACON":
        raise ValueError("Invalid ANI")
    frames = []

    def scan(start: int, end: int) -> None:
        pos = start
        while pos + 8 <= end:
            kind = data[pos:pos + 4]
            length = struct.unpack_from("<I", data, pos + 4)[0]
            body = pos + 8
            limit = body + length
            if limit > len(data):
                raise ValueError("Truncated ANI chunk")
            if kind == b"LIST":
                scan(body + 4, limit)
            elif kind == b"icon":
                frames.append(data[body:limit])
            pos = limit + (length & 1)

    scan(12, len(data))
    return frames


def export_one(preset: Path) -> None:
    assets = preset / "assets"
    render = assets / "render"
    render.mkdir(exist_ok=True)
    for name in STATIC:
        png, x, y = cur_image((assets / (name + ".cur")).read_bytes())
        (render / (name + ".png")).write_bytes(png)
        (render / (name + ".hot")).write_text(f"{x},{y}", encoding="ascii")
    for name in ("working", "busy"):
        directory = render / name
        directory.mkdir(exist_ok=True)
        frames = ani_frames((assets / (name + ".ani")).read_bytes())
        if len(frames) != 30:
            raise ValueError(f"Expected 30 frames for {name}; got {len(frames)}")
        for i, frame in enumerate(frames):
            png, x, y = cur_image(frame)
            (directory / f"{i:03d}.png").write_bytes(png)
        (render / (name + ".hot")).write_text(f"{x},{y}", encoding="ascii")
    # These two are application cursors (for the map demo), not Windows system roles.
    size, border = preset.name.split("-")
    art.SIZE_FACTOR = .5 * int(size[1:]) / 100
    art.OUTLINE_PX = int(border[1:]) / 100
    shapes = {
        "grab": [("M",13,32),("Q",13,27,18,27),("L",20,30),
                 ("L",20,13),("Q",20,9,24,9),("Q",28,9,28,13),
                 ("L",28,27),("L",29,10),("Q",29,6,33,6),
                 ("Q",37,6,37,10),("L",37,27),("L",38,14),
                 ("Q",38,10,42,10),("Q",46,10,46,14),
                 ("L",46,33),("L",48,27),("Q",50,23,53,25),
                 ("Q",56,27,53,35),("L",49,48),("Q",46,55,37,55),
                 ("L",24,55),("Q",17,54,14,47),("L",10,38),
                 ("Q",8,33,13,32)],
        "grabbing": [("M",13,29),("Q",13,25,18,24),("L",19,25),
                     ("L",19,19),("Q",19,15,23,15),("Q",27,15,27,19),
                     ("L",27,22),("Q",28,17,32,17),("Q",36,17,36,21),
                     ("Q",38,17,42,19),("Q",45,20,45,24),
                     ("Q",48,22,51,26),("Q",53,30,50,40),
                     ("L",47,48),("Q",44,54,37,54),("L",23,54),
                     ("Q",17,53,14,47),("L",10,38),
                     ("Q",8,33,13,29)]
    }
    for name, commands in shapes.items():
        points = art.path(commands)
        picture = art.theme(art.polygon(points), 1.1,
                            vector_points=art.transformed_points(points))
        png, x, y = cur_image(art.cursor(picture, (32, 30)))
        (assets / (name + ".cur")).write_bytes(art.cursor(picture, (32, 30)))
        (render / (name + ".png")).write_bytes(png)
        (render / (name + ".hot")).write_text(f"{x},{y}", encoding="ascii")
    print(preset.name, "17 roles exported")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    args = parser.parse_args()
    for preset in sorted((args.root / "presets").glob("s*-b*")):
        export_one(preset)
