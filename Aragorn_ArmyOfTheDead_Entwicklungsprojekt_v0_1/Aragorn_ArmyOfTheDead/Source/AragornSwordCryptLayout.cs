using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // Fixed 52x32 layout approved before coding the sword-spirit trial.
    // Grid coordinates are from the north-west. The seven original skeleton
    // positions are preserved; TWO skeletons per post are the new x1 baseline.
    // We do not change the layout or ghost weapon behaviour.
    internal static class AragornSwordCryptLayout
    {
        public const int Width = 52;
        public const int Height = 32;

        private static readonly IntVec2[] SkeletonAnchors = {
            new IntVec2(7, 15), new IntVec2(8, 21),
            new IntVec2(18, 16), new IntVec2(21, 20),
            new IntVec2(26, 14), new IntVec2(24, 7),
            new IntVec2(30, 17)
        };

        private static readonly IntVec2[] EliteAnchors = {
            new IntVec2(19, 12), new IntVec2(27, 22),
            new IntVec2(33, 17), new IntVec2(42, 23)
        };

        private static readonly IntVec2[] GraveAnchors = {
            new IntVec2(41, 10), new IntVec2(46, 15), new IntVec2(41, 22)
        };

        public static IntVec3 Cell(Map map, int x, int y)
        {
            // Keep a single orientation, so the blueprint and enemy positions
            // never drift from one another during procedural map generation.
            return new IntVec3(map.Center.x - Width / 2 + x, 0,
                map.Center.z + Height / 2 - 1 - y);
        }

        public static IntVec3 Grave(Map map, int index)
        {
            IntVec2 cell = GraveAnchors[index];
            return Cell(map, cell.x, cell.z);
        }

        public static List<Pawn> SpawnDefenders(Map map, Faction faction)
        {
            var result = new List<Pawn>();
            // Seven original skeleton posts, doubled to 14 baseline skeletons.
            // SpawnThemedGuards uses AragornThreat.ScaleCount, which reads the
            // existing global quest-danger multiplier and storyteller wealth.
            foreach (IntVec2 post in SkeletonAnchors)
                result.AddRange(ProfanedEncounterUtility.SpawnThemedGuards(
                    map, Cell(map, post.x, post.z), faction, 2, "skeletons"));

            // Preserve the four strong Profaned posts drawn in the blueprint.
            foreach (IntVec2 post in EliteAnchors)
                result.AddRange(ProfanedEncounterUtility.SpawnThemedGuards(
                    map, Cell(map, post.x, post.z), faction, 1, "elite"));

            return result;
        }

        private static char[,] CreateBlueprint()
        {
            var cells = new char[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) cells[x, y] = 'R';

            void Paint(int x, int y, char symbol)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    cells[x, y] = symbol;
            }

            void Fill(int minX, int minY, int maxX, int maxY, char symbol)
            {
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++) Paint(x, y, symbol);
            }

            void Room(int x1, int y1, int x2, int y2)
            {
                Fill(x1, y1, x2, y2, 'W');
                Fill(x1 + 1, y1 + 1, x2 - 1, y2 - 1, 'P');
            }

            // Unchanged schematic: entrance, corridor, central guard halls,
            // north side-crypt, long passage, and the eastern three-grave hall.
            Room(2, 11, 12, 24);
            Room(12, 16, 17, 19);
            Room(16, 9, 29, 25);
            Room(20, 3, 26, 10);
            Room(28, 16, 37, 19);
            Room(36, 5, 50, 27);

            Fill(9, 17, 16, 18, 'P');
            Fill(25, 17, 38, 18, 'P');
            Fill(22, 9, 23, 12, 'P');

            Paint(2, 18, 'D');
            Paint(11, 18, 'D');
            Paint(16, 18, 'D');
            Paint(23, 9, 'D');
            Paint(29, 18, 'D');
            Paint(37, 18, 'D');
            return cells;
        }

        private static void ClearNaturalObstacles(Map map, IntVec3 at)
        {
            if (!at.InBounds(map)) return;
            foreach (Thing thing in at.GetThingList(map).ToList())
            {
                if (thing.def.category == ThingCategory.Plant ||
                    thing.def.defName.StartsWith("Mineable", StringComparison.OrdinalIgnoreCase))
                {
                    try { thing.Destroy(DestroyMode.Vanish); }
                    catch (Exception e)
                    {
                        Log.WarningOnce("[Aragorn] Could not clear cave cell: " + e.Message, 1542501);
                    }
                }
            }
        }

        private static void PlaceBuilding(Map map, IntVec3 cell,
            ThingDef def, ThingDef stuff)
        {
            if (!cell.InBounds(map) || def == null ||
                cell.GetThingList(map).Any(t => t is Building)) return;
            try
            {
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff : null);
                GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Aragorn] Sword crypt wall failed: " + e.Message, 1542502);
            }
        }

        private static bool AdjacentToRoom(char[,] grid, int x, int y)
        {
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || xx >= Width || yy < 0 || yy >= Height) continue;
                if (grid[xx, yy] != 'R') return true;
            }
            return false;
        }

        private static bool SupportedRoof(char[,] grid, int x, int y)
        {
            for (int dx = -5; dx <= 5; dx++)
            for (int dy = -5; dy <= 5; dy++)
            {
                if (dx * dx + dy * dy > 25) continue;
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || xx >= Width || yy < 0 || yy >= Height) continue;
                if (grid[xx, yy] == 'W' || grid[xx, yy] == 'R') return true;
            }
            return false;
        }

        public static IntVec3 Build(Map map)
        {
            var grid = CreateBlueprint();
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            ThingDef rock = DefDatabase<ThingDef>.GetNamedSilentFail("MineableSlate");
            TerrainDef cursed = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");
            if (wall == null || door == null || stone == null)
                throw new InvalidOperationException("Sword crypt requires vanilla stone walls and doors.");

            // Pass one: clear the layout and lay the selected Profaned floor.
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (grid[x, y] == 'R') continue;
                IntVec3 cell = Cell(map, x, y);
                if (!cell.InBounds(map))
                    throw new InvalidOperationException("Quest map is too small for the approved 52x32 crypt.");
                ClearNaturalObstacles(map, cell);
                if (cursed != null && cell.Standable(map))
                    map.terrainGrid.SetTerrain(cell, cursed);
            }

            // Pass two: cave-rock band outside the drawn masonry. Never block
            // the west entrance, including its short outside approach.
            if (rock != null)
                for (int y = 1; y < Height - 1; y++)
                for (int x = 1; x < Width - 1; x++)
                {
                    if (grid[x, y] != 'R' || !AdjacentToRoom(grid, x, y) ||
                        (x <= 2 && y >= 16 && y <= 20)) continue;
                    IntVec3 cell = Cell(map, x, y);
                    if (cell.InBounds(map) && cell.Standable(map))
                        PlaceBuilding(map, cell, rock, null);
                }

            // Pass three: original walls and the six drawn doorways.
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                IntVec3 at = Cell(map, x, y);
                if (grid[x, y] == 'W') PlaceBuilding(map, at, wall, stone);
                else if (grid[x, y] == 'D') PlaceBuilding(map, at, door, stone);
            }

            // Two unobtrusive stone roof supports in the wide end chamber.
            // The floor plan, three tomb positions and all routes stay intact.
            var supports = new[] { new IntVec2(44, 12), new IntVec2(44, 20) };
            foreach (var support in supports)
            {
                grid[support.x, support.z] = 'W';
                PlaceBuilding(map, Cell(map, support.x, support.z), wall, stone);
            }

            // Only roof cells with a nearby support; avoids cave-ins. The
            // original 52x32 walkability structure is otherwise unchanged.
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (grid[x, y] != 'P' || !SupportedRoof(grid, x, y)) continue;
                IntVec3 cell = Cell(map, x, y);
                if (cell.InBounds(map) && cell.Walkable(map))
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            }

            // Reopen the route from the outside to the entrance.
            for (int x = 0; x <= 1; x++)
                ClearNaturalObstacles(map, Cell(map, x, 18));

            Log.Message("[Aragorn] Approved 52x32 sword crypt generated; " +
                "7 skeleton posts x2 = 14 base skeletons (global danger scaling), 4 elite posts.");
            return Cell(map, Width / 2, Height / 2);
        }
    }
}
