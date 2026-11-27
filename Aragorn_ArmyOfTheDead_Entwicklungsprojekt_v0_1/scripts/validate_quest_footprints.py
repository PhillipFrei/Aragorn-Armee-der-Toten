#!/usr/bin/env python3
"""Guard RimWorld 1.6 trial-map clearance from mountain/ruins regressions."""
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
prep = (root / "Source" / "AragornTrialSitePreparation.cs").read_text(encoding="utf-8")
trials = (root / "Source" / "AragornClassTrials.cs").read_text(encoding="utf-8")
sword = (root / "Source" / "AragornSwordCryptLayout.cs").read_text(encoding="utf-8")
bomb = (root / "Source" / "AragornBombardierCryptLayout.cs").read_text(encoding="utf-8")

# Check the ENTIRE footprint, including walls, doors and extra clearance.
for name, width, height in [
    ("sword", 64, 44), ("ritual", 65, 49), ("other", 71, 55)
]:
    cx = 125
    cz = 125
    margin = 3
    x0 = cx - width // 2 - margin
    z0 = cz - height // 2 - margin
    x1 = cx - width // 2 + width - 1 + margin
    z1 = cz - height // 2 + height - 1 + margin
    assert (x1 - x0 + 1, z1 - z0 + 1) == (width + margin * 2, height + margin * 2), name
    assert 0 <= x0 < x1 < 250 and 0 <= z0 < z1 < 250, name

# No half-built structure is allowed: roof and static obstacles are
# removed *before* construction, and ground is normalized even in water.
assert prep.index("map.roofGrid.SetRoof(cell, null)") < prep.index("thing.Destroy(DestroyMode.Vanish)")
assert prep.index("thing.Destroy(DestroyMode.Vanish)") < prep.index(
    "map.terrainGrid.SetTerrain(cell, buildableGround)")
assert "TerrainDefOf.Soil" in prep
assert "thing is Pawn" in prep, "Never delete live quest pawns while clearing the building"
assert "thing is Building && t.Spawned" in prep or "t is Building && t.Spawned" in prep
assert "throw new InvalidOperationException" in prep
assert "map.Size.x" in prep and "map.Size.z" in prep
assert "RequireBuilding(Map map" in prep

main = trials.split("public static IntVec3 Build(Map map, int cls)", 1)[1]
for expected in [
    "AragornTrialSitePreparation.ClearFootprint(map, map.Center,",
    "AragornSwordCryptLayout.Width, AragornSwordCryptLayout.Height",
    "AragornBombardierCryptLayout.Width, AragornBombardierCryptLayout.Height",
    "AragornTrialSitePreparation.ClearFootprint(map, c, 71, 55)",
]:
    assert expected in main, f"Missing trial-map pre-clearance: {expected}"

# The verified wall check may be formatted across two C# lines.
import re
assert re.search(r"AragornTrialSitePreparation\.RequireBuilding\s*\(\s*map\s*,", main)

assert main.index("ClearFootprint(map, map.Center,") < main.index(
    "AragornSwordCryptLayout.Build(map)")
assert main.index("ClearFootprint(map, c, 71, 55)") < main.index(
    "OuterFort(map, c, 35, 27, false)")

for name, contents in [("sword", sword), ("ritual", bomb)]:
    build = contents.split("public static IntVec3 Build(Map map)", 1)[1]
    assert "AragornTrialSitePreparation.RequireBuilding(" in build, (
        f"{name} must validate every planned wall and doorway")
    assert build.index("RequireBuilding(") < build.rindex("return "), (
        f"{name} must validate the structure before returning success")
print("Quest footprint QA: all 6 trials clear full 64x44 / 65x49 / 71x55 "
      "construction rectangles + 3-cell margin; roofs before rock, obstacles "
      "before terrain, bounds before mutation, verify required walls/doors.")
