#!/usr/bin/env python3
"""Package the playable RimWorld mod with the exact original top-level folder."""
from pathlib import Path
import sys
import zipfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
mod = root / "Aragorn_ArmyOfTheDead"
name = "Aragorn_Armee_der_Toten_v0.39.3_Profaned_Bogenschuetzen_Skelette_Eigener_Uebergang_Komplettmod.zip"
out = root / "dist" / name
meta = ET.parse(mod / "About" / "About.xml").getroot()
assert meta.findtext("name") == "Aragorn – Armee der Toten (v0.39.3)"
assert meta.findtext("packageId") == "aragorn.armyofthedead"
assembly = mod / "1.6" / "Assemblies" / "Aragorn_ArmyOfTheDead.dll"
if not assembly.is_file() or assembly.stat().st_size < 4096:
    sys.exit("ERROR: compiled Aragorn_ArmyOfTheDead.dll not found.")
required = ["About/About.xml", "Defs/AragornRescueQuest.xml",
            "Source/AragornTrialSitePreparation.cs",
            "1.6/Assemblies/Aragorn_ArmyOfTheDead.dll"]
for rel in required:
    if not (mod / rel).is_file():
        sys.exit("ERROR: missing " + rel)
out.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=8) as z:
    for file in sorted(mod.rglob("*")):
        if not file.is_file():
            continue
        rel = file.relative_to(mod)
        if any(part in {"obj", "bin", ".git", "__MACOSX", "dist"} for part in rel.parts):
            continue
        if file.name == ".DS_Store":
            continue
        z.write(file, (Path(mod.name) / rel).as_posix())
with zipfile.ZipFile(out) as z:
    assert z.testzip() is None
    names = z.namelist()
    assert all(n.startswith("Aragorn_ArmyOfTheDead/") for n in names)
    assert "Aragorn_ArmyOfTheDead/1.6/Assemblies/Aragorn_ArmyOfTheDead.dll" in names
    assert "Aragorn_ArmyOfTheDead/About/About.xml" in names
    assert z.read("Aragorn_ArmyOfTheDead/About/About.xml").find(b"v0.39.3") >= 0
print("PACKAGE OK:", out, "-", len(names), "files;", out.stat().st_size, "bytes")
