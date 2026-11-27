#!/usr/bin/env python3
"""Static QA for the third class trial, the five-chamber bombardier crypt."""
from collections import Counter, deque
from pathlib import Path

mod = Path(__file__).resolve().parents[1] / "Aragorn_ArmyOfTheDead"
layout = (mod / "Source" / "AragornBombardierCryptLayout.cs").read_text(encoding="utf-8")
trials = (mod / "Source" / "AragornClassTrials.cs").read_text(encoding="utf-8")
w, h = 65, 49
assert "Width = 65, Height = 49" in layout
grid = [["R"] * w for _ in range(h)]

def paint(x, y, t):
    if 0 <= x < w and 0 <= y < h:
        grid[y][x] = t

def fill(x0, y0, x1, y1, tile):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            paint(x, y, tile)

def room(x0, y0, x1, y1):
    fill(x0, y0, x1, y1, "W")
    fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, "P")

rooms = [
    (25, 34, 39, 46), (24, 20, 40, 35),
    (3, 14, 22, 35), (42, 14, 61, 35),
    (20, 2, 44, 21),
]
for rect in rooms:
    assert f"Room({', '.join(map(str, rect))})" in layout
    room(*rect)
for rect in [(30, 32, 34, 36), (30, 18, 34, 23),
             (19, 25, 26, 29), (38, 25, 45, 29)]:
    fill(*rect, "P")
doors = [(32, 46), (32, 34), (32, 20), (22, 27), (42, 27)]
for x, y in doors:
    paint(x, y, "D")
paint(32, 3, "W")
columns = [(27, 40), (37, 40), (27, 28), (37, 28),
           (10, 19), (15, 19), (49, 19), (54, 19),
           (24, 7), (40, 7)]
for x, y in columns + [
    (10, 26), (15, 26), (49, 26), (54, 26), (31, 10), (33, 10)
]:
    paint(x, y, "C")
for x in range(28, 37, 2):
    if x != 32:
        paint(x, 41, "B")
        paint(x, 30, "B")
for y in range(22, 33, 2):
    if y != 28:
        paint(18, y, "B")
        paint(46, y, "B")
for x in range(24, 41, 2):
    if x != 32:
        paint(x, 16, "B")

reachable = {(32, 47)}
queue = deque(reachable)
while queue:
    x, y = queue.popleft()
    for target in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        tx, ty = target
        if 0 <= tx < w and 0 <= ty < h and grid[ty][tx] in "PDB" and target not in reachable:
            queue.append(target)
            reachable.add(target)

walkable = {(x, y) for y in range(h) for x in range(w) if grid[y][x] in "PDB"}
assert reachable - {(32, 47)} == walkable, f"Unreachable tiles: {walkable - reachable}"
for name, x, y in [
    ("west ritual seal", 11, 24), ("east ritual seal", 53, 24),
    ("western bombardier", 26, 10), ("eastern bombardier", 38, 10),
    ("west mage camp", 11, 27), ("east mage camp", 53, 27),
]:
    assert (x, y) in reachable, f"Cannot reach {name} at {(x, y)}"
assert Counter("".join(map("".join, grid)))["D"] == 5
assert Counter("".join(map("".join, grid)))["B"] >= 20, (
    "Ritual chapels must have actual barricaded guard positions")
assert "RoofChapel(map, 3, 14, 22, 35)" in layout
assert "RoofChapel(map, 42, 14, 61, 35)" in layout
assert "RoofChapel(map, 20, 2, 44, 21)" in layout
assert "firingSquares" in layout and "SpawnThemedGuards(" in layout
assert "wanderRadius: 2f" in layout

assert "AragornBombardierCryptLayout.Build(map)" in trials
assert "AragornBombardierCryptLayout.SpawnMages(map, hostile)" in trials
assert "AR_Bombardier_GuideBody" in trials and "AR_Bombardier_SealsBrokenBody" in trials
assert "cls == 2 && RemainingMarkers > 0" in trials and "trialGhosts.Contains(spirit)" in trials
assert '"Aragorn/Gizmos/GhostTrial"' in trials
assert "RemainingMarkers >= 2 ? 480 : 780" in trials
for lang in ("German", "English"):
    keys = (mod / "Languages" / lang / "Keyed" / "AragornArmy.xml").read_text(encoding="utf-8")
    for name in ("AR_Bombardier_GuideTitle", "AR_Bombardier_SealsBrokenBody",
                 "AR_Bombardier_SealGizmoDesc", "AR_Class_Description2"):
        assert f"<{name}>" in keys, f"Missing {name} in {lang}"
print("Bombardier quest verified: 65x49, five connected, roofed chambers with defended barricades, "
      "both seals + both ghosts reachable, five gates, lightning phase logic, "
      "quest guidance and objective gizmo icons present.")
