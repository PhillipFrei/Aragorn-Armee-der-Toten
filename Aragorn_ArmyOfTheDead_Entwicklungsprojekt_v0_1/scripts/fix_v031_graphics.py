#!/usr/bin/env python3
"""v0.31 visual-only repair for an UNPACKED Aragorn_ArmyOfTheDead mod folder.

Requires: pip install pillow
Run: python fix_v031_graphics.py /path/to/Aragorn_ArmyOfTheDead

Corrects ONLY eight east-facing spirit sprites that point left in v0.30.
East must face to the viewer's right; RimWorld mirrors east to show west.
Builds About/ModIcon.png (64x64) from the existing ghost king body sprite.
Does not touch weapon textures, Anduril, rendering code or game balance.
All operations are deterministic and prevent accidental double-flipping.
"""
from __future__ import annotations

import hashlib
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance

SOURCE_HASHES = {
    4: "ac28f34b3b176ec05a56",
    5: "ac28f34b3b176ec05a56",
    6: "ac28f34b3b176ec05a56",
    7: "237d1e3a0dc385046456",
    8: "237d1e3a0dc385046456",
    11: "e1e81dd1fea70b101871",
    12: "e1e81dd1fea70b101871",
    13: "28939351bf362339c1f3",
}


def build_mod_icon(ghost_king: Path, target: Path) -> None:
    source = Image.open(ghost_king).convert("RGBA")
    crop = source.crop((71, 22, 309, 260))
    crop = ImageEnhance.Contrast(crop).enhance(1.1)
    crop = ImageEnhance.Sharpness(crop).enhance(1.12)
    crop = crop.resize((248, 248), Image.Resampling.LANCZOS)
    size = 256
    image = Image.new("RGBA", (size, size), (12, 26, 30, 255))
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            dx = (x-size*.5)/(size*.6)
            dy = (y-size*.46)/(size*.68)
            glow = max(0, 1-(dx*dx+dy*dy))
            pixels[x, y] = (
                int(10+15*glow), int(26+41*glow), int(28+38*glow), 255
            )
    image.alpha_composite(crop, (4, 4))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((1,1,254,254), radius=18,
                           outline=(72,200,173,225), width=5)
    draw.rounded_rectangle((6,6,249,249), radius=16,
                           outline=(8,49,51,170), width=2)
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize((64,64), Image.Resampling.LANCZOS).save(target, optimize=True)


def main(root: Path) -> None:
    base = root / "Textures" / "Aragorn" / "Spirits"
    for index, expected_hash in SOURCE_HASHES.items():
        path = base / f"AR_Ghost_{index:02d}_east.png"
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if not digest.startswith(expected_hash):
            print("SKIPPED (already corrected or different version):", path.name)
            continue
        with Image.open(path) as source:
            flipped = source.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            flipped.save(path, optimize=True)
        print("Corrected:", path.name)
    build_mod_icon(base / "AR_Ghost_13_south.png",
                   root / "About" / "ModIcon.png")
    print("Ready: About/ModIcon.png 64x64")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python fix_v031_graphics.py MOD_ROOT")
    main(Path(sys.argv[1]))
