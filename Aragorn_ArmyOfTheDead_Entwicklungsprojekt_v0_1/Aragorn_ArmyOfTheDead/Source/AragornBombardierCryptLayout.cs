using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AragornArmy
{
    // Third CLASS trial (indices 6 and 7): a substantial ritual crypt.
    // Unlike the old empty square, this is a connected five-chamber level:
    // gatehouse -> central hall -> left/right sorcerer shrines -> ghost sanctum.
    // The two side braziers are the seals; lightning ends when both go out.
    internal static class AragornBombardierCryptLayout
    {
        public const int Width = 65, Height = 49;

        private static IntVec3 Point(Map map, int x, int y)
        {
            return new IntVec3(map.Center.x - Width / 2 + x, 0,
                map.Center.z + Height / 2 - 1 - y);
        }

        public static IntVec3 Entrance(Map map) => Point(map, 32, 39);
        public static IntVec3 WestShrine(Map map) => Point(map, 11, 24);
        public static IntVec3 EastShrine(Map map) => Point(map, 53, 24);
        public static IntVec3 FirstGhost(Map map) => Point(map, 26, 10);
        public static IntVec3 SecondGhost(Map map) => Point(map, 38, 10);
        public static IntVec3 Sanctum(Map map) => Point(map, 32, 9);

        private static char[,] Blueprint()
        {
            char[,] b = new char[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) b[x, y] = 'R';

            void Paint(int x, int y, char tile)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    b[x, y] = tile;
            }

            void Fill(int x0, int y0, int x1, int y1, char tile)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++) Paint(x, y, tile);
            }

            void Room(int x0, int y0, int x1, int y1)
            {
                Fill(x0, y0, x1, y1, 'W');
                Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, 'P');
            }

            // Five real rooms, deliberately separated by narrow approaches.
            Room(25, 34, 39, 46);  // Gatehouse: first enemies
            Room(24, 20, 40, 35);  // Crossroads: cursed guardians
            Room(3, 14, 22, 35);   // Western mage chapel and first seal
            Room(42, 14, 61, 35);  // Eastern mage chapel and second seal
            Room(20, 2, 44, 21);   // Northern sanctum, two bombardier ghosts

            // A wide but controlled central path and two branch corridors.
            Fill(30, 32, 34, 36, 'P');
            Fill(30, 18, 34, 23, 'P');
            Fill(19, 25, 26, 29, 'P');
            Fill(38, 25, 45, 29, 'P');

            // Six distinct gates mark the transitions. The north hall is
            // only reached through the crossroads; shrines can be approached
            // in either order.
            Paint(32, 46, 'D'); // entry
            Paint(32, 34, 'D'); // gatehouse -> crossroads
            Paint(32, 20, 'D'); // crossroads -> sanctum
            Paint(22, 27, 'D'); // left shrine
            Paint(42, 27, 'D'); // right shrine
            Paint(32, 3, 'W');  // altar-room northern wall stays closed

            // Interior supports/covers, deliberately off the main routes.
            foreach (var p in new [] {
                new IntVec2(27, 40), new IntVec2(37, 40),
                new IntVec2(27, 28), new IntVec2(37, 28),
                new IntVec2(10, 19), new IntVec2(15, 19),
                new IntVec2(49, 19), new IntVec2(54, 19),
                new IntVec2(24, 7), new IntVec2(40, 7)
            }) Paint(p.x, p.z, 'C');
            return b;
        }

        private static void ClearCell(Map map, IntVec3 cell)
        {
            foreach (Thing thing in cell.GetThingList(map).ToList())
            {
                if (thing.def.category == ThingCategory.Plant ||
                    thing.def.defName.StartsWith("Mineable", StringComparison.OrdinalIgnoreCase))
                {
                    try { thing.Destroy(DestroyMode.Vanish); }
                    catch (Exception e)
                    {
                        Log.WarningOnce("[Aragorn] Ritual crypt tile cleanup: " +
                            e.Message, 3490808);
                    }
                }
            }
        }

        private static ThingDef Stone => DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
        private static ThingDef Wall => DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
        private static ThingDef Door => DefDatabase<ThingDef>.GetNamedSilentFail("Door");

        private static void Structure(Map map, IntVec3 cell, ThingDef def)
        {
            if (def == null || !cell.InBounds(map)) return;
            if (cell.GetThingList(map).Any(t => t is Building)) return;
            try
            {
                Thing thing = ThingMaker.MakeThing(def,
                    def.MadeFromStuff ? Stone : null);
                GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Aragorn] Ritual crypt construction: " +
                    e.Message, 3490809);
            }
        }

        public static IntVec3 Build(Map map)
        {
            if (map == null || Wall == null || Door == null || Stone == null)
                throw new InvalidOperationException("Ritual crypt needs vanilla stone and doors.");

            char[,] b = Blueprint();
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");

            // Clear the original natural ground first. Do not import foreign
            // KCSG layouts, modded mountain generation, or any new dependencies.
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (b[x, y] == 'R') continue;
                    IntVec3 p = Point(map, x, y);
                    if (!p.InBounds(map))
                        throw new InvalidOperationException("Map too small for the 65x49 ritual crypt.");
                    ClearCell(map, p);
                    if (floor != null && p.Standable(map))
                        map.terrainGrid.SetTerrain(p, floor);
                }

            // Build only the floor plan. Some rooms remain uncovered:
            // actual vanilla thunder/lightning must be able to strike.
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    IntVec3 p = Point(map, x, y);
                    switch (b[x, y])
                    {
                        case 'W': Structure(map, p, Wall); break;
                        case 'D': Structure(map, p, Door); break;
                        case 'C': Structure(map, p, Wall); break;
                    }
                }

            // Lamps are visual dressing, not mission targets or kill objectives.
            // Their definitions come from the already-required Profaned mod;
            // missing decorative objects never prevent the quest from starting.
            ThingDef light = DefDatabase<ThingDef>.GetNamedSilentFail("BotchJob_ProfanedLantern")
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("BotchJob_ProfanedBrazier");
            foreach (var p in new [] {
                new IntVec2(28, 37), new IntVec2(36, 37),
                new IntVec2(8, 30), new IntVec2(57, 30),
                new IntVec2(27, 16), new IntVec2(37, 16)
            }) Structure(map, Point(map, p.x, p.z), light);

            // Clear a 3-cell arrival approach outside the south door.
            for (int y = 47; y <= 48; y++)
                for (int x = 30; x <= 34; x++)
                {
                    IntVec3 cell = Point(map, x, y);
                    if (cell.InBounds(map)) ClearCell(map, cell);
                }

            Log.Message("[Aragorn] Third class trial: 65x49 gatehouse, " +
                "two mage chapels and two-seal sanctum generated.");
            return Point(map, 32, 26);
        }

        private static void StandGuard(Map map, IntVec3 at, Faction faction,
            List<Pawn> pawns)
        {
            if (pawns.Count == 0) return;
            LordMaker.MakeNewLord(faction,
                new LordJob_DefendPoint(at, wanderRadius: 4f, defendRadius: 20f,
                    isCaravanSendable: false, addFleeToil: false),
                map, pawns);
        }

        public static List<Pawn> SpawnMages(Map map, Faction hostile)
        {
            var all = new List<Pawn>();
            if (map == null || hostile == null) return all;

            // Base counts are scaled independently by the existing global
            // quest danger multiplier, just like the sword-crypt patrols.
            var posts = new [] {
                new { x = 32, y = 39, n = 4, role = "undead" }, // entrance
                new { x = 32, y = 27, n = 5, role = "mages" },  // crossroads
                new { x = 11, y = 27, n = 6, role = "mages" },  // west shrine
                new { x = 53, y = 27, n = 6, role = "mages" },  // east shrine
                new { x = 26, y = 13, n = 4, role = "mages" },  // left guard
                new { x = 38, y = 13, n = 4, role = "mages" }   // right guard
            };
            foreach (var post in posts)
            {
                IntVec3 position = Point(map, post.x, post.y);
                List<Pawn> group = ProfanedEncounterUtility.SpawnThemedGuards(
                    map, position, hostile, post.n, post.role);
                all.AddRange(group);
                StandGuard(map, position, hostile, group);
            }
            return all;
        }

        public static void AssignBombardierGuard(Map map, Faction hostile,
            Pawn first, Pawn second)
        {
            var ghosts = new [] { first, second }
                .Where(p => p != null && p.Spawned && !p.Dead).ToList();
            StandGuard(map, Sanctum(map), hostile, ghosts);
        }
    }
}
