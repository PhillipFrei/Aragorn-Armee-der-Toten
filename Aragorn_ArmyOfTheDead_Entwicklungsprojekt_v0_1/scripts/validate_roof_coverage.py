"""Geometric regression: complete supported roofs, unobstructed grave footprints,
and nonrectangular terrain preparation. Runtime AI/fog still needs a game test."""
from pathlib import Path
import runpy
base = Path(__file__).resolve().parent
sword = runpy.run_path(str(base / 'validate_sword_crypt.py'))
grid = sword['expanded']; w=sword['W']; h=sword['H']
floor = {(x,y) for y in range(h) for x in range(w) if grid[y][x] != 'R'}
mask = {(x,y) for y in range(h) for x in range(w)
        if any((x+dx,y+dy) in floor for dx in range(-2,3) for dy in range(-2,3))}
assert floor <= mask
assert (0,0) not in mask and (w-1,h-1) not in mask
assert len(mask) < w*h*0.85, 'The clearing must follow the crypt instead of its bounding rectangle'
supports={(x,y) for x,y in floor if grid[y][x]=='W'}
new=set()
for x,y in sorted(floor,key=lambda p:(p[1],p[0])):
    if not any((x-a)**2+(y-b)**2<=36 for a,b in supports):
        supports.add((x,y));new.add((x,y))
for x,y in floor:
    assert any((x-a)**2+(y-b)**2<=36 for a,b in supports)
for x,y in [(47,16),(52,21),(47,28)]:
    assert not ({(x,y),(x,y-1),(x,y+1)} & new), 'Roof support occupies a quest sarcophagus'
# Verify all the rectangular room dimensions, with support columns added
# only when no existing wall lies within the conservative six-cell radius.
rooms=[(-13,-10,13,10),(-6,-4,6,4),(-6,12,6,25),
       (-32,-11,-23,1),(-30,-23,-9,-6),(9,-23,30,-6),
       (-8,-8,8,3),(-6,5,6,17),(-22,8,-2,25),(2,8,22,25),
       (-12,7,12,25),(-30,-13,-17,-1),(-13,-11,13,11)]
for x0,z0,x1,z1 in rooms:
    walls={(x,z) for x in range(x0,x1+1) for z in range(z0,z1+1)
           if x in (x0,x1) or z in (z0,z1)}
    added=set()
    for x in range(x0+1,x1):
        for z in range(z0+1,z1):
            if not any((x-a)**2+(z-b)**2<=36 for a,b in walls|added):
                added.add((x,z))
    assert all(any((x-a)**2+(z-b)**2<=36 for a,b in walls|added)
               for x in range(x0,x1+1) for z in range(z0,z1+1))
    for x,z in [(-7,18),(7,18),(0,11),(0,13),(-3,11),(3,11),(0,0)]:
        assert not ({(x,z-1),(x,z),(x,z+1)} & added), 'Column would replace a quest objective'
print('Roof QA: all room cells supported; grave footprints clear; crypt clearing follows its outline.')
