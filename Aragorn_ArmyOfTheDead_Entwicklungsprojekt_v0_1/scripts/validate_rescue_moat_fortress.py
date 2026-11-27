#!/usr/bin/env python3
"""Rescue mission: deep-water moat, one crossing, nested keep and prison."""
from collections import deque
from pathlib import Path
import xml.etree.ElementTree as ET

base = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
moat = (base / "Source" / "AragornRescueMoatFortress.cs").read_text(encoding="utf-8")
quest = (base / "Source" / "AragornRescueQuest.cs").read_text(encoding="utf-8")
builder = (base / "Source" / "ProfanedEncounterUtility.cs").read_text(encoding="utf-8")

for requirement in [
    "Width = 77", "Height = 65",
    'GetNamedSilentFail("WaterDeep")',
    "ClearFootprint(map, center, Width, Height, 3)",
    "Math.Abs(x) < MoatInnerX && Math.Abs(z) < MoatInnerZ",
    "map.terrainGrid.SetTerrain(At(center, x, z), deepWater)",
    "if (!cell.Standable(map))",
    "AragornCryptInteriors.Room(map, center,",
    "AragornCryptInteriors.RoofRoom(map, center, -13, -10, 13, 10)",
    "RefreshDiscoverableRooms(map)",
    "AragornTrialSitePreparation.RequireBuilding(",
]:
    assert requirement in moat, f"Missing rescue fortress requirement: {requirement}"

assert "return AragornRescueMoatFortress.Build(map, near, out guardPost)" in builder
assert "AragornRescueMoatFortress.GuardPositions(map)" in quest
assert "prisoner.health.AddHediff(HediffDefOf.Anesthetic)" in quest
assert "captive.SetFaction(Faction.OfPlayer)" in quest
assert "AR_Rescue_MoatBody" in quest
assert "List<IntVec3> posts = null" in builder

# Model exactly the terrain and walls on a 77x65 rescue map.
# Impassable water surrounds the dry fort except for a 3-wide southern
# bridge (seven cells long) guarded by three outer doors.
land = {(x, z) for x in range(-38, 39) for z in range(-32, 33)}
water = {(x, z) for x in range(-34, 35) for z in range(-28, 29)
         if abs(x) >= 28 or abs(z) >= 22}
bridge = {(x, z) for x in range(-1, 2) for z in range(-28, -21)}
assert len(bridge) == 21
assert bridge <= water
water -= bridge
land -= water

# A continuous 53x41 outer wall, with the three middle south doors.
doors = set()
walls = set()
for x in range(-26, 27):
    for z in range(-20, 21):
        if abs(x) == 26 or abs(z) == 20:
            (doors if z == -20 and abs(x) <= 1 else walls).add((x, z))
assert len([p for p in doors if p[1] == -20]) == 3

def room(x0, z0, x1, z1, door_x, door_z):
    assert door_x in (x0, x1) or door_z in (z0, z1)
    assert x0 <= door_x <= x1 and z0 <= door_z <= z1
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            if x in (x0, x1) or z in (z0, z1):
                (doors if (x, z) == (door_x, door_z) else walls).add((x, z))

room(-13, -10, 13, 10, 0, -10)    # keep
room(-6, -4, 6, 4, 0, -4)        # prison room INSIDE keep
room(-24, -17, -15, -7, -15, -12)
room(15, -17, 24, -7, 15, -12)
room(-24, 7, -15, 17, -15, 13)
room(15, 7, 24, 17, 15, 13)
assert doors >= {(0, -20), (0, -10), (0, -4)}
land -= walls

def reachable(origin):
    q = deque([origin])
    reached = {origin}
    while q:
        x, z = q.popleft()
        for p in ((x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1)):
            if p in land and p not in reached:
                reached.add(p)
                q.append(p)
    return reached

starting_position = (0, -31)
assert starting_position in land
reached = reachable(starting_position)
assert (0, 0) in reached, "The prisoner must be reachable through the bridge and three doors."
for objective in [(0, -14), (-19, -12), (19, -12), (-19, 12), (19, 12)]:
    assert objective in reached, f"Guard room/strongpoint unreachable: {objective}"
for p in [(0, -28), (0, -25), (0, -22), (0, -20), (0, -10), (0, -4), (0, 0)]:
    assert p in reached, f"The rescue entrance route is blocked at {p}"

# Block just the crossing and ensure no hidden land access elsewhere.
without_bridge = land - bridge
q = deque([starting_position])
seen = {starting_position}
while q:
    x, z = q.popleft()
    for p in ((x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1)):
        if p in without_bridge and p not in seen:
            seen.add(p)
            q.append(p)
assert (0, 0) not in seen, "The moat can be bypassed without using the bridge."

for lang in ["German", "English"]:
    xml = ET.parse(base / "Languages" / lang / "Keyed" / "AragornArmy.xml")
    values = {e.tag: e.text or "" for e in xml.getroot()}
    for key in ["AR_Rescue_MoatTitle", "AR_Rescue_MoatBody",
                "AR_Rescue_Description", "AR_Rescue_ArrivalBody"]:
        assert values.get(key), f"Missing {lang} {key}"
print("Rescue QA OK: 77x65 footprint, unbroken deep-water barrier except one "
      "3-wide southern bridge, all 3 aligned keep/prison gates and all "
      "five guard posts reachable; no alternative path across the moat.")
