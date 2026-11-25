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

# The built map contains two small support pillars inside the end chamber.
walkable = {(x, y) for y in range(32) for x in range(52)
            if grid[y][x] not in ("R", "W")}
walkable.discard((44, 12))
walkable.discard((44, 20))
start = (5, 18)
queue = deque([start])
reached = {start}
while queue:
    x, y = queue.popleft()
    for nxt in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if nxt in walkable and nxt not in reached:
            reached.add(nxt)
            queue.append(nxt)
assert reached == walkable, f"Disconnected map: {len(walkable - reached)} cells"
for grave in ((41, 10), (46, 15), (41, 22)):
    assert grave in reached, f"Unreachable grave {grave}"

code = source.read_text(encoding="utf-8")
assert "Width = 52" in code and "Height = 32" in code
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

print("Sword crypt layout OK: 52x32; 7 posts x4 = 28 baseline skeletons (14 at minimum x0.5); "
      "4 elite posts; 3 reachable tombs; 6 doors; no disconnected cells.")
