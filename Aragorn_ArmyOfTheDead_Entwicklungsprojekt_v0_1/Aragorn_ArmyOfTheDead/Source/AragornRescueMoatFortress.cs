using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AragornArmy
{
    // Aragorn's rescue is a real two-ring prison FORTRESS, not a side annex.
    // 1) A deep-water moat encloses the 53x41 outer walls on all four sides.
    // 2) The moat has NO crossing when the map first appears; players must
    //    prepare a temporary causeway from the southern bank.
    // 3) Four Profaned archer squads hold real barricade lines outside.
    // 4) Aggressive skeletons defend the vaulted keep with a nested prison.
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

            // The moat is an unbroken rectangle of real WaterDeep terrain.
            // The map starts with NO installed bridge, ford or walkway.
            // In vanilla RimWorld 1.6 players cannot bridge deep water, so
            // a quest-specific command will prepare a crossing on demand.
            for (int x = -MoatOuterX; x <= MoatOuterX; x++)
            for (int z = -MoatOuterZ; z <= MoatOuterZ; z++)
            {
                if (Math.Abs(x) < MoatInnerX && Math.Abs(z) < MoatInnerZ)
                    continue;
                map.terrainGrid.SetTerrain(At(center, x, z), deepWater);
            }

            // No crossing is built here. The selected-colonist gizmo
            // allows a deliberate crossing only once the player reaches
            // the SOUTHERN bank; otherwise this fortress is an island.
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
                if (At(center, x, z).Standable(map))
                    throw new InvalidOperationException(
                        "[Aragorn] Rescue moat must initially be fully impassable.");

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

            // Four open-air defensive lines replace ALL four watchhouses.
            // Each Profaned archer squad starts on walkable squares BEHIND
            // its own barricades, then guards that exact strongpoint.
            // The central 5-cell route to the keep doors stays clear.
            AragornCryptInteriors.BarricadeRow(map, center,
                -23, -12, -16);
            AragornCryptInteriors.BarricadeRow(map, center,
                12, 23, -16);
            AragornCryptInteriors.BarricadeRow(map, center,
                -23, -12, 5);
            AragornCryptInteriors.BarricadeRow(map, center,
                12, 23, 5);

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

            // Strong runtime consistency checks BEFORE prisoners and enemies
            // spawn. Water stays deep and impassable until a player consciously
            // initiates the crossing action from the exterior southern bank.
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
                if (At(center, x, z).Standable(map))
                    throw new InvalidOperationException(
                        "[Aragorn] Premade bridge detected: moat should be closed.");
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
            Log.Message("[Aragorn] Rescue fortress v0.39.1: 77x65 site, " +
                "UNBROKEN deep-water moat, four Profaned archer barricade lines, " +
                "roofed central keep with skeleton defenders and nested 13x9 prison at " +
                prisoner + ". Three aligned gates validated; no prebuilt crossing.");
            return prisoner;
        }

        // South shore outside the moat. This square is dry because the
        // water ring ends at z=-28, and the prepared site spans to z=-35.
        public static IntVec3 SouthBank(Map map) => At(map.Center, 0, -30);

        public static bool CrossingIsPrepared(Map map)
        {
            if (map == null) return false;
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
                if (!At(map.Center, x, z).Standable(map)) return false;
            return true;
        }

        // A mission-specific act of preparing the route. In vanilla,
        // bridges cannot be constructed over actual WaterDeep, so this
        // action builds a makeshift 3x7 stone causeway. There is NO bridge
        // on initial arrival, and the player must initiate this action.
        public static void PrepareCrossing(Map map)
        {
            if (map == null || CrossingIsPrepared(map)) return;
            TerrainDef bridge = DefDatabase<TerrainDef>.GetNamedSilentFail("Bridge");
            TerrainDef road = DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate")
                ?? TerrainDefOf.Soil;
            for (int z = -MoatOuterZ; z <= -MoatInnerZ; z++)
            for (int x = -1; x <= 1; x++)
            {
                IntVec3 pos = At(map.Center, x, z);
                if (bridge != null)
                {
                    try { map.terrainGrid.SetTerrain(pos, bridge); }
                    catch (Exception e)
                    {
                        Log.WarningOnce("[Aragorn] Causeway bridge terrain: " +
                            e.Message, 391801);
                    }
                }
                if (!pos.Standable(map))
                    map.terrainGrid.SetTerrain(pos, road);
                if (!pos.Standable(map))
                    throw new InvalidOperationException(
                        "[Aragorn] Rescue crossing cell impassable after building: " +
                        pos);
            }
            if (!CrossingIsPrepared(map))
                throw new InvalidOperationException(
                    "[Aragorn] Rescue crossing did not finish correctly.");
            Log.Message("[Aragorn] The player prepared the 3x7 rescue crossing.");
        }

        public static List<IntVec3> ArcherPosts(Map map)
        {
            IntVec3 c = map.Center;
            return new List<IntVec3> {
                At(c, -18, -13),
                At(c,  18, -13),
                At(c, -18,   8),
                At(c,  18,   8)
            };
        }

        public static List<IntVec3> ArcherCoverPositions(Map map, int postIndex)
        {
            int[] xs = { -18, 18, -18, 18 };
            int[] zs = { -16, -16, 5, 5 };
            return AragornCryptInteriors.HoldBehindRow(
                map.Center, xs[postIndex], zs[postIndex]);
        }

        public static List<IntVec3> SkeletonPositions(Map map, bool east)
        {
            var cells = new List<IntVec3>();
            // The corridors beside the jail, INSIDE the roofed keep.
            // All positions are outside the 13x9 prison's walls.
            for (int z = -7; z <= 7; z++)
            for (int n = 8; n <= 11; n++)
                cells.Add(At(map.Center, east ? n : -n, z));
            return cells;
        }


    }
}
