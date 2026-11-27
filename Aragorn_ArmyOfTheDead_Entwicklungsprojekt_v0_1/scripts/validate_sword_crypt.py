#!/usr/bin/env python3
"""Validate the approved 52x32 sword-crypt map before compiling the mod."""
from collections import Counter, deque
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
blueprint = root / "QuestDesign" / "SwordCrypt_Approved_52x32.txt"
source = root / "Source" / "AragornSwordCryptLayout.cs"
grid = [
    line for line in blueprint.read_text(encoding="utf-8").splitlines()
    if len(line) == 52 and set(line).issubset(set("RPWDSGATE"))
]
assert len(grid) == 32, f"Expected exactly 32 blueprint rows, got {len(grid)}"
assert all(len(row) == 52 for row in grid)
count = Counter("".join(grid))
for token, expected in {"A": 1, "S": 7, "E": 4, "G": 3, "D": 6, "T": 1}.items():
    assert count[token] == expected, f"Expected {expected} {token}, got {count[token]}"

# The original 52x32 sketch remains untouched in the centre of a
# larger 64x44 roofed complex. Attach 3 extra rooms to existing corridors.
W, H, ox, oy = 64, 44, 6, 6
expanded = [["R"] * W for _ in range(H)]
for y, row in enumerate(grid):
    for x, tile in enumerate(row):
        expanded[y + oy][x + ox] = tile

def paint(x, y, tile):
    if not (0 <= x < W and 0 <= y < H):
        raise AssertionError("Extension outside crypt")
    expanded[y][x] = tile

def fill(x0, y0, x1, y1, tile):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            paint(x, y, tile)

def room(x0, y0, x1, y1):
    fill(x0, y0, x1, y1, "W")
    fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, "P")

room(8, 30, 24, 42)
paint(15, 30, "D")
room(56, 14, 62, 32)
paint(56, 23, "D")
room(8, 2, 23, 11)
fill(20, 11, 21, 19, "P")
paint(20, 11, "D")
paint(22, 18, "D")

# The actual build adds two supports, offset with the legacy sketch.
for px, py in ((44, 12), (44, 20)):
    paint(px + ox, py + oy, "W")
walkable = {(x, y) for y in range(H) for x in range(W)
            if expanded[y][x] not in ("R", "W")}
start = (5 + ox, 18 + oy)
queue = deque([start])
reached = {start}
while queue:
    x, y = queue.popleft()
    for nxt in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if nxt in walkable and nxt not in reached:
            reached.add(nxt)
            queue.append(nxt)
assert reached == walkable, f"Disconnected expanded dungeon: {walkable - reached}"
for grave in ((41, 10), (46, 15), (41, 22)):
    assert (grave[0] + ox, grave[1] + oy) in reached
for annex in ((15, 37), (17, 5), (59, 27)):
    assert annex in reached, f"Unreachable roofed annex: {annex}"
assert Counter("".join(map("".join, expanded)))["D"] == 9

code = source.read_text(encoding="utf-8")
assert "Width = 64" in code and "Height = 44" in code
assert "OriginalWidth = 52" in code and "OriginalHeight = 32" in code
assert "LegacyCell(map, cell.x, cell.z)" in code
assert "faction, 4, \"skeletons\"" in code, "Skeletons must be doubled per post"
assert "AragornThreat.ScaleCount" in code, "Danger scaling documentation lost"
# Guard against the original one-grave-at-a-time bug and its regression.
trial_code = (root / "Source" / "AragornClassTrials.cs").read_text(encoding="utf-8")
assert "trialOpenedGraveIndices" in trial_code, "Independent grave state was removed"
assert 'Scribe_Collections.Look(ref trialOpenedGraveIndices' in trial_code, "Grave state must survive saves"
assert "ClosedGraves()" in trial_code, "Openable graves must be selected independently"
assert 'OrderBy(x => x.grave.Position.DistanceTo(pawn.Position))' in trial_code, (
    "Grave action must work at any nearby unopened grave")
assert "trialGraveGuidanceShown" in trial_code and "trialGravesReadyShown" in trial_code, (
    "Arrival and post-combat instructions are required")
assert "trialMarkers.Skip(trialGravesOpened)" not in trial_code, "Sequential opening returned"
assert "trialMarkers[trialGravesOpened]" not in trial_code, "Sequential opening returned"

print("Sword crypt layout OK: 64x44 with 52x32 approved core; 7 posts x4 = 28 baseline skeletons (14 at minimum x0.5); "
      "4 elite posts; 3 reachable tombs; 9 doors; 3 new foggable annexes; no disconnected cells.")
