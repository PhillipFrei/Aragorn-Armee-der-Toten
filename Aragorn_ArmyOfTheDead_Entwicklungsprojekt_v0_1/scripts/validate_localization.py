#!/usr/bin/env python3
"""Checks DE/EN text parity, XML validity, and every AragornText.T/F call.

Run from any directory: python .../scripts/validate_localization.py
No external dependencies.
"""
from __future__ import annotations
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

MOD = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
source_keys: set[str] = set()
for path in (MOD / "Source").glob("*.cs"):
    source_keys.update(re.findall(r'AragornText\.(?:T|F)\("([^"]+)"', path.read_text(encoding="utf8")))

data: dict[str, dict[str, str]] = {}
for language in ("German", "English"):
    folder = MOD / "Languages" / language
    values: dict[str, str] = {}
    for path in sorted(folder.rglob("*.xml")):
        parsed = ET.parse(path)
        seen: set[str] = set()
        for el in parsed.getroot():
            if el.tag in seen:
                raise SystemExit(f"Duplicate key in {path}: {el.tag}")
            seen.add(el.tag)
            if path.parent.name == "Keyed":
                if el.tag in values:
                    raise SystemExit(f"Duplicate keyed value {language}: {el.tag}")
                values[el.tag] = el.text or ""
    data[language] = values

missing = {lang: sorted(source_keys - set(values)) for lang, values in data.items()}
extra = set(data["German"]) ^ set(data["English"])
if extra:
    print("DE/EN mismatch:", sorted(extra))
if any(missing.values()):
    for lang, keys in missing.items():
        if keys:
            print(f"Missing {lang} keys: {keys}")
    raise SystemExit(1)
if extra:
    raise SystemExit(1)

for key in data["German"]:
    patterns = []
    for lang in ("German", "English"):
        text = data[lang][key]
        indices = sorted(set(re.findall(r"\{\d+\}", text)))
        patterns.append(indices)
    if patterns[0] != patterns[1]:
        raise SystemExit(f"Placeholder mismatch in {key}: DE {patterns[0]}, EN {patterns[1]}")

for lang in ("German", "English"):
    injected = MOD / "Languages" / lang / "DefInjected"
    filenames = {p.relative_to(injected).as_posix() for p in injected.rglob("*.xml")}
    otherlang = "English" if lang == "German" else "German"
    peer = MOD / "Languages" / otherlang / "DefInjected"
    peerfiles = {p.relative_to(peer).as_posix() for p in peer.rglob("*.xml")}
    assert filenames == peerfiles, f"DefInjected file mismatch {lang}"
    for filename in filenames:
        a = {el.tag for el in ET.parse(injected / filename).getroot()}
        b = {el.tag for el in ET.parse(peer / filename).getroot()}
        assert a == b, f"DefInjected key mismatch {filename}"

for xml in (MOD / "Defs").glob("*.xml"):
    ET.parse(xml)
for xml in (MOD / "Patches").glob("*.xml"):
    ET.parse(xml)
ET.parse(MOD / "About" / "About.xml")

print(f"Localization verified: {len(source_keys)} source references, "
      f"{len(data['German'])} keyed strings per language, "
      f"German and English DefInjected files present, XML valid.")
