#!/usr/bin/env python3
"""Fix original Aragorn v0.23 spirit artwork with left-facing 'east' views.

Usage: pip install pillow
       python fix_spirit_east_facing.py /path/to/Aragorn_ArmyOfTheDead

Only known input files are modified. Re-running the script leaves modified
files untouched, so sprites are never accidentally flipped twice.
"""
import hashlib
import sys
from pathlib import Path
from PIL import Image

ORIGINAL_HASH_PREFIX = {
    "01": "1f9771135c21f7c3",
    "02": "f16ce84bd40ae285",
    "03": "06664079d36fa6d4",
    "04": "d580473241a6d4bd",
    "05": "03ee0832ad33026c",
    "06": "bcb0eeadd93e3ea3",
    "07": "fe57d9f70a72b220",
    "08": "791c35b275b0118d",
    "09": "1428bd416eaf865b",
    "10": "4ce8a8a41b50e943",
    "11": "5728d4f6a6e30321",
    "12": "6d7636d90239ead1",
    "13": "566d302c9398adb2",
}

def main(mod_root: Path) -> None:
    folder = mod_root / "Textures" / "Aragorn" / "Spirits"
    changed = 0
    for number, expected_hash in ORIGINAL_HASH_PREFIX.items():
        path = folder / f"AR_Ghost_{number}_east.png"
        if not path.is_file():
            print("MISSING", path)
            continue
        actual_hash = hashlib.sha256(path.read_bytes()).hexdigest()
        if not actual_hash.startswith(expected_hash):
            print("SKIP (not the original left-facing asset)", path.name)
            continue
        with Image.open(path) as img:
            img.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(path)
        changed += 1
        print("FIXED", path.name)
    print(f"Corrected {changed} east-facing spirit sprites.")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python fix_spirit_east_facing.py MOD_DIRECTORY")
    main(Path(sys.argv[1]))
