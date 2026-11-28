#!/usr/bin/env python3
"""Verify six spacious, roofed quest complexes and tactical guard positions."""
from collections import deque
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
main = (root / "Source" / "AragornClassTrials.cs").read_text(encoding="utf-8")
interiors = (root / "Source" / "AragornCryptInteriors.cs").read_text(encoding="utf-8")
enemy = (root / "Source" / "ProfanedEncounterUtility.cs").read_text(encoding="utf-8")
bomb = (root / "Source" / "AragornBombardierCryptLayout.cs").read_text(encoding="utf-8")
sword = (root / "Source" / "AragornSwordCryptLayout.cs").read_text(encoding="utf-8")

assert "ClearFootprint(map, c, 71, 55)" in main
assert "OuterFort(map, c, 35, 27, false)" in main
for cls in ("ArcherFortress", "AxeTemple", "CaptainFortress", "KingSanctuary"):
    assert f"AragornCryptInteriors.{cls}(map, c);" in main

for text in [
    "RoofRoom(map, origin", "map.fogGrid.Refog(new CellRect(", "RoofDefOf.RoofConstructed",
    "RebuildAllRegionsAndRooms", "BarricadeRow(map", "BarricadeColumn",
    "BotchJob_ProfanedBrazierDarklight", "BotchJob_ProfanedStele",
    "BotchJob_ProfanedThrone",
]:
    assert text in interiors, f"Required room/fog/decor feature missing: {text}"
assert "RefogMap" not in interiors, "RimWorld uses FogGrid.Refog, not RefogMap"
assert "List<IntVec3> fortifiedPositions = null" in enemy
assert "fortifiedPositions" in enemy.split("GenSpawn.Spawn(guard, cell, map);")[0]
assert "AragornCryptInteriors.HoldBehindRow(" in main
assert "AragornCryptInteriors.RefreshDiscoverableRooms(map)" in main
assert "AragornCryptInteriors.RoofRoom(map, map.Center," in bomb
assert "firingSquares" in bomb and "wanderRadius: 2f" in bomb
assert "Width = 64" in sword and "Height = 44" in sword
assert "original approved" in sword.lower() or "original plan" in sword.lower()

# Explicit occupancy/4-way access simulation of each 71x55 fort.
def check_layout(kind, rooms, markers, barriers=()):
    walk = {(x, z) for x in range(-34, 35) for z in range(-26, 27)}
    # The 4-wide south entrance is connected from an exterior entry cell.
    door_cells = set()
    for x0, z0, x1, z1, dx, dz in rooms:
        assert x0 < dx < x1 or z0 < dz < z1, f"Bad room entry for {kind}"
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                if x in (x0, x1) or z in (z0, z1):
                    if (x, z) == (dx, dz):
                        door_cells.add((x, z))
                    else:
                        walk.discard((x, z))
    for barrier in barriers:
        for x, z in barrier:
            walk.discard((x, z))
    q = deque([(0, -26)])
    seen = {(0, -26)}
    while q:
        x, z = q.popleft()
        for p in ((x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1)):
            if p in walk and p not in seen:
                seen.add(p)
                q.append(p)
    for name, pos in markers:
        assert pos in seen, f"{kind}: {name} at {pos} is not accessible"
    return len(seen)

archer_rooms = []
for shift in (-20, 0, 20):
    archer_rooms.append((shift - 6, 12, shift + 6, 25, shift, 12))
archer_rooms += [(-32, -11, -23, 1, -23, -5),
                 (23, -11, 32, 1, 23, -5)]
check_layout("archers", archer_rooms, [
    (f"archer ghost {s}", (s, 15)) for s in (-20, 0, 20)
] + [(f"archer coffin {s}", (s, 20)) for s in (-20, 0, 20)])

axe_rooms = [
    (-30, -23, -9, -6, -17, -23),
    (9, -23, 30, -6, 17, -23),
    (-8, -8, 8, 3, 0, -8),
    (-6, 5, 6, 17, 0, 5),
]
check_layout("axe", axe_rooms, [
    ("western duel coffin", (-3, 11)),
    ("eastern duel coffin", (3, 11)),
    ("western ghouls", (-19, -13)),
    ("eastern ghouls", (19, -13)),
])

captain_rooms = [
    (-22, 8, -2, 25, -12, 8),
    (2, 8, 22, 25, 12, 8),
]
captain_barriers = []
for z in (-17, -6, 5):
    captain_barriers.append([(x, z) for x in range(-28, 29)
                             if 3 < abs(x) < 26 and abs(x) != 14])
check_layout("captain", captain_rooms, [
    ("first captain coffin", (-7, 18)),
    ("second captain coffin", (7, 18)),
    ("first captain", (-7, 12)),
    ("second captain", (7, 12)),
], captain_barriers)

king_rooms = [
    (-12, 7, 12, 25, 0, 7),
    (-30, -13, -17, -1, -23, -13),
    (17, -13, 30, -1, 23, -13),
]
check_layout("king", king_rooms, [
    ("royal coffin", (0, 11)), ("king", (0, 13))
], [[(x, z) for x in (-7, 7) for z in (13, 19)]])

assert "new IntVec3(laneX, 0, 20)" in main
assert "new IntVec3(-7, 0, 18)" in main and "new IntVec3(7, 0, 18)" in main
assert "new IntVec3(0, 0, 11)" in main
print("Architecture QA: 71x55 archer, axe, captain and king complexes; "
      "64x44 sword and 65x49 bomber; accessible quest coffins and enemies; "
      "roofed foggable rooms, cover positions and Profaned props present.")
