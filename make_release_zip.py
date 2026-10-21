"""Package the tested Windows files without local logs or legacy helper files."""
from __future__ import annotations

import argparse
import hashlib
import zipfile
from pathlib import Path

ROOT_FILES = (
    "Configure.exe", "CursorTestPanel.exe", "CursorTheme.exe",
    "Install.cmd", "InstallPackage.ps1",
    "Uninstall.cmd", "UninstallPackage.ps1", "CursorMotionV3.cs",
    "CursorMotionV3.ps1", "CursorMotionLauncher.ps1", "Configurator.cs",
    "CursorTestPanel.cs", "CursorTheme.cs", "build_package.py",
    "add_small_cursor_frames.py", "export_render_sources.py", "verify_assets.py",
    "Preview.png", "Preview-150.png", "README-PACOTE-v3.2.md",
)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    presets = sorted((source / "presets").glob("s*-b*"))
    if len(presets) != 21:
        raise ValueError(f"Expected 21 presets; got {len(presets)}")
    for name in ROOT_FILES:
        if not (source / name).is_file():
            raise FileNotFoundError(source / name)
    for preset in presets:
        assets = preset / "assets"
        for name in ("arrow.cur", "ibeam.cur", "busy.ani", "working.ani",
                     "grab.cur", "grabbing.cur", "render/arrow.png",
                     "render/working/000.png", "render/busy/029.png"):
            if not (assets / name).is_file():
                raise FileNotFoundError(assets / name)
    prefix = "Codex-Inspired-Pointer-Windows-v3.2"
    count = 0
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=6, allowZip64=True) as archive:
        for name in ROOT_FILES:
            target_name = "README.md" if name == "README-PACOTE-v3.2.md" else name
            archive.write(source / name, f"{prefix}/{target_name}")
            count += 1
        for preset in presets:
            for path in sorted(preset.rglob("*")):
                if path.is_file():
                    archive.write(path, f"{prefix}/{path.relative_to(source).as_posix()}")
                    count += 1
    with zipfile.ZipFile(output) as archive:
        bad = archive.testzip()
        if bad is not None:
            raise ValueError(f"Corrupt ZIP entry: {bad}")
    with output.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    print(f"{output.name}: {count} files, {output.stat().st_size} bytes")
    print(f"SHA-256: {digest.upper()}")


if __name__ == "__main__":
    main()
