"""Check the binary CUR/ANI files before publishing a Windows package."""
from __future__ import annotations

import io
import struct
import sys
from pathlib import Path
from PIL import Image

from export_render_sources import ani_frames

SIZES = (32, 48, 64, 96, 128, 256)
STATIC = ("arrow", "help", "crosshair", "ibeam", "pen", "no", "sizens",
          "sizewe", "sizenwse", "sizenesw", "sizeall", "uparrow", "hand",
          "pin", "person", "grab", "grabbing")


def check_cur(data: bytes, label: str) -> None:
    if len(data) < 6 or struct.unpack_from("<HHH", data) != (0, 2, 6):
        raise ValueError(f"{label}: expected six CUR images")
    sizes = []
    for i in range(6):
        width, height, _, _, hot_x, hot_y, length, offset = struct.unpack_from(
            "<BBBBHHII", data, 6 + i * 16)
        n = width or 256
        if n != (height or 256) or not (0 <= hot_x < n and 0 <= hot_y < n):
            raise ValueError(f"{label}: invalid size or hotspot in frame {i}")
        if offset + length > len(data):
            raise ValueError(f"{label}: image exceeds file length")
        with Image.open(io.BytesIO(data[offset:offset + length])) as image:
            if image.size != (n, n):
                raise ValueError(f"{label}: PNG size mismatch")
            image.verify()
        sizes.append(n)
    if tuple(sizes) != SIZES:
        raise ValueError(f"{label}: unexpected sizes {sizes}")


def main() -> None:
    root = Path(sys.argv[1])
    presets = sorted((root / "presets").glob("s*-b*"))
    if len(presets) != 21:
        raise ValueError(f"Expected 21 presets, found {len(presets)}")
    count = 0
    for preset in presets:
        assets = preset / "assets"
        for name in STATIC:
            path = assets / (name + ".cur")
            check_cur(path.read_bytes(), str(path))
            count += 1
        for path in (assets / "motion").glob("*.cur"):
            check_cur(path.read_bytes(), str(path))
            count += 1
        for name in ("busy", "working"):
            frames = ani_frames((assets / (name + ".ani")).read_bytes())
            if len(frames) != 30:
                raise ValueError(f"{preset.name}/{name}: expected 30 frames")
            for i, frame in enumerate(frames):
                check_cur(frame, f"{preset.name}/{name}/{i}")
                count += 1
        print(preset.name, "passed", flush=True)
    print(f"Validated {count} CUR frames across {len(presets)} presets")


if __name__ == "__main__":
    main()
