#!/usr/bin/env python3
"""Rescue v0.39.1: deep-water barrier, prepared route, archers and skeletons."""
from collections import deque
from pathlib import Path
import xml.etree.ElementTree as ET

base = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
moat = (base / "Source" / "AragornRescueMoatFortress.cs").read_text(encoding="utf-8")
quest = (base / "Source" / "AragornRescueQuest.cs").read_text(encoding="utf-8")
builder = (base / "Source" / "ProfanedEncounterUtility.cs").read_text(encoding="utf-8")
gizmos = (base / "Source" / "CombatOnlyPatches.cs").read_text(encoding="utf-8")

for requirement in [
    "Width = 77", "Height = 65",
    'GetNamedSilentFail("WaterDeep")',
    "ClearFootprint(map, center, Width, Height, 3)",
    "map.terrainGrid.SetTerrain(At(center, x, z), deepWater)",
    "public static void PrepareCrossing(Map map)",
    "public static bool CrossingIsPrepared(Map map)",
    "public static IntVec3 SouthBank(Map map)",
    "AragornCryptInteriors.Room(map, center,",
    "AragornCryptInteriors.RoofRoom(map, center, -13, -10, 13, 10)",
    "AragornCryptInteriors.BarricadeRow(map, center,",
    "ArcherCoverPositions(Map map, int postIndex)",
    "SkeletonPositions(Map map, bool east)",
    "RefreshDiscoverableRooms(map)",
    "AragornTrialSitePreparation.RequireBuilding(",
]:
    assert requirement in moat, f"Missing rescue feature: {requirement}"

# No prefabricated bridge, no old watchtowers.
assert "int bridgeCells = 0;" not in moat
assert "Four small Profaned guard houses" not in moat
assert "four small Profaned guard houses" not in moat.lower()
assert "public static List<IntVec3> GuardPositions" not in moat
assert "AragornCryptBuilder.BuildRescuePrison(map, map.Center" in quest
assert "return AragornRescueMoatFortress.Build(map, near, out guardPost)" in builder
assert "SpawnThemedGuards(" in quest
assert 'perArcherPost, "archers"' in quest
assert 'perSkeletonFlank, "skeletons"' in quest
assert "new LordJob_DefendPoint(at, wanderRadius: 2f" in quest
assert "new LordJob_DefendPoint(post, wanderRadius: 2.5f" in quest
assert "prisoner.health.AddHediff(HediffDefOf.Anesthetic)" in quest
assert "captive.SetFaction(Faction.OfPlayer)" in quest
assert "Scribe_Values.Look(ref moatRequiresPreparation" in quest
assert "Scribe_Values.Look(ref moatCrossingPrepared" in quest
assert "GetMoatGizmos(Pawn pawn)" in quest and "PrepareMoatCrossing(pawn)" in quest
assert "rescue.GetMoatGizmos(pawn)" in gizmos
assert "HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))" in gizmos
assert "AR_Rescue_MoatBody" in quest

# Full water ring blocks passage on arrival. After player action, exactly
# 3x7 formerly WaterDeep tiles are transformed to a walkable causeway.
land = {(x, z) for x in range(-38, 39) for z in range(-32, 33)}
water = {(x, z) for x in range(-34, 35) for z in range(-28, 29)
         if abs(x) >= 28 or abs(z) >= 22}
causeway = {(x, z) for x in range(-1, 2) for z in range(-28, -21)}
assert len(causeway) == 21 and causeway <= water
assert (0, -30) in land
land -= water

# Complete outer wall and the two rooms one inside the other.
walls = set()
doors = set()
def perimeter(x0, z0, x1, z1, access):
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            if x in (x0, x1) or z in (z0, z1):
                (doors if (x, z) in access else walls).add((x, z))

perimeter(-26, -20, 26, 20, {(-1, -20), (0, -20), (1, -20)})
perimeter(-13, -10, 13, 10, {(0, -10)})
perimeter(-6, -4, 6, 4, {(0, -4)})

# Four open cover lines, NEVER inside or crossing keep walls.
cover_rows = [
    (-23, -15, -16), (15, 23, -16),
    (-23, -15, 5), (15, 23, 5)
]
for x0, x1, z in cover_rows:
    for x in range(x0, x1 + 1):
        assert (x, z) not in walls, f"Barricade intersects a wall at {(x, z)}"
    assert f"{x0}, {x1}, {z});" in moat

land -= walls
def reachable(terrain, origin=(0, -31)):
    assert origin in terrain
    q = deque([origin])
    reached = {origin}
    while q:
        x, z = q.popleft()
        for p in ((x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1)):
            if p in terrain and p not in reached:
                reached.add(p)
                q.append(p)
    return reached

# Unprepared water is truly impassable; a rescue is not instantly possible.
not_yet = reachable(land)
assert (0, 0) not in not_yet, "The map contains a premade approach through deep water"
assert (0, -30) in not_yet, "Player cannot reach the southern shore"

# Preparing the single causeway makes all three gates and the captive reachable.
prepared = reachable(land | causeway)
for p in [(0, -28), (0, -22), (0, -20), (0, -10), (0, -4), (0, 0)]:
    assert p in prepared, f"Prepared route blocked at {p}"
for p in [(-18, -13), (18, -13), (-18, 8), (18, 8),
          (-9, 0), (9, 0)]:
    assert p in prepared, f"Guardian's strongpoint inaccessible: {p}"

# Skeletons may spawn only in the keep corridors, not in Aragorn's inner cell.
for east in (True, False):
    for z in range(-7, 8):
        for n in range(8, 12):
            x = n if east else -n
            assert -13 < x < 13 and -10 < z < 10
            assert (x, z) in prepared and (x, z) not in walls
            assert not (-6 <= x <= 6 and -4 <= z <= 4)

for lang in ["German", "English"]:
    xml = ET.parse(base / "Languages" / lang / "Keyed" / "AragornArmy.xml")
    values = {e.tag: e.text or "" for e in xml.getroot()}
    for key in ["AR_Rescue_MoatTitle", "AR_Rescue_MoatBody",
                "AR_Rescue_PrepareCrossing", "AR_Rescue_PrepareCrossingDesc",
                "AR_Rescue_GoToSouthBank", "AR_Rescue_CrossingReadyBody",
                "AR_Rescue_Description", "AR_Rescue_ArrivalBody"]:
        assert values.get(key), f"Missing {lang} {key}"
print("Rescue QA OK: no bridge on arrival, 77x65 deep-water ring, "
      "only the player-prepared 3x7 causeway unlocks 3 nested gates; "
      "4 barricaded Profaned archer lines, 2 inner-keep skeleton garrisons, "
      "all targets reachable, and no active watchhouses.")
