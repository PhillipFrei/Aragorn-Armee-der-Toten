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
                -13, -10, 13, 10, 0, -10, roof: false);
            AragornCryptInteriors.Room(map, center,
                -6, -4, 6, 4, 0, -4, roof: false);
            // Once the inner jail walls exist they support the remaining
            // central roof, making the entire hall a separate covered interior.
            AragornCryptInteriors.RoofRoom(map, center, -13, -10, 13, 10);

            // Four open-air defensive lines replace ALL four watchhouses.
            // Each Profaned archer squad starts on walkable squares BEHIND
            // its own barricades, then guards that exact strongpoint.
            // The central 5-cell route to the keep doors stays clear.
            AragornCryptInteriors.BarricadeRow(map, center,
                -23, -15, -16);
            AragornCryptInteriors.BarricadeRow(map, center,
                15, 23, -16);
            AragornCryptInteriors.BarricadeRow(map, center,
                -23, -15, 5);
            AragornCryptInteriors.BarricadeRow(map, center,
                15, 23, 5);

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

            RevealExterior(map);
            AragornCryptInteriors.RefreshDiscoverableRooms(map);
            Log.Message("[Aragorn] Rescue fortress v0.39.1: 77x65 site, " +
                "UNBROKEN deep-water moat, four Profaned archer barricade lines, " +
                "roofed central keep with skeleton defenders and nested 13x9 prison at " +
                prisoner + ". Three aligned gates validated; no prebuilt crossing.");
            return prisoner;
        }

        // FAIL-SAFE: a fully cleared site with zero structures is worse than
        // a simpler fortress. When a terrain/roof/ruin interaction interrupts
        // the elaborate map builder, construct a safe, entirely walkable
        // prison at the same centre rather than leaving the mission empty.
        // This path avoids the large-area preparation and complex roofing
        // methods that may have triggered the original exception.
        public static IntVec3 BuildRecoveryPrison(Map map, out IntVec3 guardPost)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            IntVec3 c = map.Center;
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef slate = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            TerrainDef soil = TerrainDefOf.Soil;
            if (wall == null || door == null || slate == null || soil == null)
                throw new InvalidOperationException(
                    "[Aragorn] Emergency prison lacks vanilla slate/walls/door/soil.");

            // Repair the whole emergency 31x27 building area. Roofs are
            // removed BEFORE the underlying natural rock to prevent collapse.
            // Never remove a player colonist or Aragorn from an already saved
            // broken map. Clear the three-wide SOUTHERN passage as well, even
            // if the first builder previously generated a partial deep moat.
            for (int z = -36; z <= 15; z++)
            for (int x = -16; x <= 16; x++)
            {
                if (Math.Abs(z) > 15 && Math.Abs(x) > 2) continue;
                IntVec3 p = At(c, x, z);
                if (!p.InBounds(map)) continue;
                try { map.roofGrid.SetRoof(p, null); }
                catch (Exception ex)
                {
                    Log.WarningOnce("[Aragorn] Emergency prison roof: " +
                        ex.Message, 392523);
                }
                foreach (Thing t in p.GetThingList(map).ToList())
                {
                    if (t is Pawn || t == null || !t.Spawned) continue;
                    try { t.Destroy(DestroyMode.Vanish); }
                    catch (Exception ex)
                    {
                        Log.WarningOnce("[Aragorn] Emergency prison blocker: " +
                            t.def.defName + " / " + ex.Message, 392524);
                    }
                }
                try { map.terrainGrid.SetTerrain(p, soil); }
                catch (Exception ex)
                {
                    Log.WarningOnce("[Aragorn] Emergency prison ground: " +
                        ex.Message, 392525);
                }
            }

            // Always build two concentric rooms, with clearly visible doors
            // along the central rescue route; the roof is deliberately
            // optional in this emergency version.
            void Perimeter(int halfX, int halfZ, int doorZ)
            {
                for (int x = -halfX; x <= halfX; x++)
                for (int z = -halfZ; z <= halfZ; z++)
                {
                    if (Math.Abs(x) != halfX && Math.Abs(z) != halfZ) continue;
                    IntVec3 p = At(c, x, z);
                    bool gate = x == 0 && z == doorZ;
                    ThingDef def = gate ? door : wall;
                    Thing t = ThingMaker.MakeThing(def,
                        def.MadeFromStuff ? slate : null);
                    GenSpawn.Spawn(t, p, map, WipeMode.Vanish);
                    AragornTrialSitePreparation.RequireBuilding(map, p, def);
                }
            }

            Perimeter(13, 10, -10);
            Perimeter(6, 4, -4);

            // Barricade positions remain optional; if the recovery was
            // reached after some props appeared, do not allow them to block
            // the only southern access.
            foreach (int side in new [] { -1, 1 })
            {
                for (int x = 15; x <= 23; x++)
                {
                    IntVec3 p = At(c, x * side, -16);
                    if (p.InBounds(map) && p.Standable(map) &&
                        !p.GetThingList(map).Any(t => t is Building))
                    {
                        ThingDef barricade = DefDatabase<ThingDef>
                            .GetNamedSilentFail("Barricade");
                        if (barricade != null)
                        {
                            Thing cover = ThingMaker.MakeThing(barricade,
                                barricade.MadeFromStuff ? slate : null);
                            GenSpawn.Spawn(cover, p, map, WipeMode.Vanish);
                        }
                    }
                }
            }

            guardPost = At(c, 0, -14);
            if (!guardPost.Standable(map) || !c.Standable(map))
                throw new InvalidOperationException(
                    "[Aragorn] Emergency rescue prison still inaccessible.");
            Log.Warning("[Aragorn] RECOVERED empty rescue map: accessible " +
                "double-room prison built with dry southern passage; " +
                "no further terrain/roof dependency.");
            return c;
        }

        // South shore outside the moat. This square is dry because the
        // water ring ends at z=-28, and the prepared site spans to z=-35.
        public static IntVec3 SouthBank(Map map) => At(map.Center, 0, -30);

        public static void RevealExterior(Map map)
        {
            // Explicitly clear stale mountain fog over the moat and south bank.
            for (int x = -38; x <= 38; x++)
            for (int z = -35; z <= 32; z++)
            {
                IntVec3 p = At(map.Center, x, z);
                if (p.InBounds(map) && map.roofGrid.RoofAt(p) == null)
                    map.fogGrid.Unfog(p);
            }
        }

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
                map.roofGrid.SetRoof(pos, null);
                map.fogGrid.Unfog(pos);
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
            var cells = new List<IntVec3>();
            // No archer can accidentally be spawned INSIDE the central
            // keep through a wall: all cover positions are in the courtyard.
            int[] xs = { -18, 18, -18, 18 };
            int[] zs = { -16, -16, 5, 5 };
            int centerX = xs[postIndex];
            for (int z = zs[postIndex] + 2; z <= zs[postIndex] + 4; z++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = centerX + dx;
                if (Math.Abs(x) < 15) continue;
                cells.Add(At(map.Center, x, z));
            }
            return cells;
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
