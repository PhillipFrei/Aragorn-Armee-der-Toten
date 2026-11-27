using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AragornArmy
{
    // Aragorn's rescue is a real two-ring prison FORTRESS, not a side annex.
    // 1) A deep-water moat encloses the 53x41 outer walls on all four sides.
    // 2) A single 3-wide guarded crossing permits caravans to enter.
    // 3) A vaulted keep contains a separate, enclosed prison room at its centre.
    // Both gates and the inner prison door remain reachable by normal colonists.
    // Existing rescue quest state, prisoner generation and completion are unchanged.
    internal static class AragornRescueMoatFortress
    {
        public const int Width = 77;
        public const int Height = 65;
        private const int WallX = 26;
        private const int WallZ = 20;
        private const int MoatInnerX = 28;
        private const int MoatInnerZ = 22;
        private const int MoatOuterX = 34;
        private const int MoatOuterZ = 28;

        private static IntVec3 At(IntVec3 center, int x, int z) =>
            center + new IntVec3(x, 0, z);

        public static IntVec3 Build(Map map, IntVec3 near, out IntVec3 guardPost)
        {
            guardPost = IntVec3.Invalid;
            if (map == null) throw new ArgumentNullException(nameof(map));
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            TerrainDef deepWater = DefDatabase<TerrainDef>.GetNamedSilentFail("WaterDeep");
            TerrainDef bridge = DefDatabase<TerrainDef>.GetNamedSilentFail("Bridge");
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate")
                ?? TerrainDefOf.Soil;
            if (wall == null || door == null || deepWater == null || floor == null)
                throw new InvalidOperationException(
                    "[Aragorn] Rescue fortress requires Wall, Door and WaterDeep terrain.");

            // This must happen before ALL buildings and deep-water terrain.
            // Previously random mountains suppressed walls, rooms and exits.
            IntVec3 center = map.Center;
            AragornTrialSitePreparation.ClearFootprint(map, center, Width, Height, 3);

            // Fortress courtyard: a large 53x41 inner island.
            for (int x = -WallX; x <= WallX; x++)
            for (int z = -WallZ; z <= WallZ; z++)
                map.terrainGrid.SetTerrain(At(center, x, z), floor);

            // The moat is a rectangular, fully enclosing six-to-seven-cell
            // band of DEEP water. It does not overlap any walls or prison rooms.
            // The only approach is the defended three-cell-wide bridge south.
            for (int x = -MoatOuterX; x <= MoatOuterX; x++)
            for (int z = -MoatOuterZ; z <= MoatOuterZ; z++)
            {
                if (Math.Abs(x) < MoatInnerX && Math.Abs(z) < MoatInnerZ)
                    continue;
                map.terrainGrid.SetTerrain(At(center, x, z), deepWater);
            }

            // Continuous southern crossing. In vanilla the Bridge terrain
            // covers the water; when it cannot support deep-water bridging,
            // use solid flagstone so the site is NEVER an unreachable island.
            // Other than this one crossing, the entire ring stays WaterDeep.
            int bridgeCells = 0;
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
            {
                IntVec3 cell = At(center, x, z);
                if (bridge != null)
                {
                    try { map.terrainGrid.SetTerrain(cell, bridge); }
                    catch (Exception e)
                    {
                        Log.WarningOnce("[Aragorn] Deep-water bridge terrain: " +
                            e.Message, 392001);
                    }
                }
                if (!cell.Standable(map))
                    map.terrainGrid.SetTerrain(cell, floor);
                if (!cell.Standable(map))
                    throw new InvalidOperationException(
                        "[Aragorn] Rescue moat bridge still impassable: " + cell);
                bridgeCells++;
            }

            // Fortified outer ring: three gates face the bridge. All other
            // perimeter cells MUST be stone walls, never an omitted segment.
            for (int x = -WallX; x <= WallX; x++)
            for (int z = -WallZ; z <= WallZ; z++)
            {
                if (Math.Abs(x) != WallX && Math.Abs(z) != WallZ) continue;
                bool gate = z == -WallZ && Math.Abs(x) <= 1;
                AragornCryptInteriors.Build(map, At(center, x, z),
                    gate ? door : wall);
            }

            // A central vaulted keep, itself a room within the fortress.
            // The actual 13x9 jail is an independent room INSIDE this keep.
            // Both rooms have a single southern door, aligned with the bridge.
            AragornCryptInteriors.Room(map, center,
                -13, -10, 13, 10, 0, -10);
            AragornCryptInteriors.Room(map, center,
                -6, -4, 6, 4, 0, -4);
            // Once the inner jail walls exist they support the remaining
            // central roof, making the entire hall a separate covered interior.
            AragornCryptInteriors.RoofRoom(map, center, -13, -10, 13, 10);

            // Four small Profaned guard houses inside the outer ring.
            // The southern doors open to the courtyard, not onto water.
            AragornCryptInteriors.Room(map, center,
                -24, -17, -15, -7, -15, -12);
            AragornCryptInteriors.Room(map, center,
                15, -17, 24, -7, 15, -12);
            AragornCryptInteriors.Room(map, center,
                -24, 7, -15, 17, -15, 13);
            AragornCryptInteriors.Room(map, center,
                15, 7, 24, 17, 15, 13);

            // Low cover inside the southern gateway; the main central route
            // is intentionally clear for attacking caravans and rescuers.
            AragornCryptInteriors.BarricadeRow(map, center, -18, -4, -17);
            AragornCryptInteriors.BarricadeRow(map, center, 4, 18, -17);
            AragornCryptInteriors.BarricadeRow(map, center, -22, -17, 3);
            AragornCryptInteriors.BarricadeRow(map, center, 17, 22, 3);

            // Optional original Profaned ritual furniture, not mission objects.
            // No furniture blocks the two central doors or the prisoner tile.
            foreach (var p in new[] {
                new IntVec2(-9, 6), new IntVec2(9, 6),
                new IntVec2(-9, -6), new IntVec2(9, -6),
                new IntVec2(-19, 13), new IntVec2(19, 13)
            })
                AragornCryptInteriors.Decoration(map, center, p.x, p.z,
                    "BotchJob_ProfanedBrazierDarklight",
                    "BotchJob_ProfanedBrazier",
                    "BotchJob_ProfanedLantern");
            AragornCryptInteriors.Decoration(map, center, 0, 7,
                "BotchJob_ProfanedStele", "BotchJob_ProfanedStela",
                "BotchJob_ProfanedLantern");

            // Prison interiors: keep the captive in the middle, not on a
            // bed or in front of the only door. The bed is purely decorative.
            ThingDef bed = DefDatabase<ThingDef>.GetNamedSilentFail("Bed");
            if (bed != null)
                try
                {
                    AragornCryptInteriors.Build(map, At(center, 3, 2),
                        bed, required: false);
                }
                catch (Exception ex)
                {
                    Log.Warning("[Aragorn] Optional prison bed failed: " +
                        ex.Message);
                }

            // Strong runtime consistency checks before prisoner + guards are
            // spawned: ALL gates, the nested rooms and the dry bridge exist.
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
                if (!At(center, x, z).Standable(map))
                    throw new InvalidOperationException(
                        "[Aragorn] Prison inaccessible: moat crossing missing.");
            foreach (int z in new[] { -WallZ, -10, -4 })
                AragornTrialSitePreparation.RequireBuilding(
                    map, At(center, 0, z), door);
            for (int x = -WallX; x <= WallX; x++)
            for (int z = -WallZ; z <= WallZ; z++)
                if (Math.Abs(x) == WallX || Math.Abs(z) == WallZ)
                    AragornTrialSitePreparation.RequireBuilding(map,
                        At(center, x, z),
                        z == -WallZ && Math.Abs(x) <= 1 ? door : wall);

            IntVec3 prisoner = At(center, 0, 0);
            if (!prisoner.Standable(map))
                throw new InvalidOperationException(
                    "[Aragorn] Centre of the inner prison is not traversable.");

            guardPost = At(center, 0, -14);
            if (!guardPost.Standable(map))
                throw new InvalidOperationException(
                    "[Aragorn] Guard post blocked inside fortress entrance.");

            AragornCryptInteriors.RefreshDiscoverableRooms(map);
            Log.Message("[Aragorn] Rescue fortress: a 77x65 prepared site with " +
                "deep-water moat, " + bridgeCells + " crossing cells, " +
                "outer wall ring, roofed central keep and inner 13x9 prison at " +
                prisoner + ". All three aligned prison gates validated.");
            return prisoner;
        }

        public static List<IntVec3> GuardPositions(Map map)
        {
            IntVec3 origin = map.Center;
            return new List<IntVec3> {
                At(origin, 0, -14),
                At(origin, -19, -12),
                At(origin, 19, -12),
                At(origin, -19, 12),
                At(origin, 19, 12)
            };
        }
    }
}
