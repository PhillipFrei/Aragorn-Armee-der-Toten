using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // One deliberately conservative builder for all six crypt interiors.
    // Roofed rooms are sealed behind actual doors and remain discoverable
    // through RimWorld fog when a player enters. The central courtyards
    // remain outdoors, especially where lightning is a quest mechanic.
    internal static class AragornCryptInteriors
    {
        private static ThingDef Wall => DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
        private static ThingDef Door => DefDatabase<ThingDef>.GetNamedSilentFail("Door");
        private static ThingDef Stone => DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
        private static ThingDef Cover => DefDatabase<ThingDef>.GetNamedSilentFail("Barricade");

        public static IntVec3 At(IntVec3 origin, int x, int z) =>
            origin + new IntVec3(x, 0, z);

        public static void Build(Map map, IntVec3 cell, ThingDef def,
            bool required = true)
        {
            if (def == null)
            {
                if (required) throw new InvalidOperationException(
                    "[Aragorn] Required crypt building def missing.");
                return;
            }
            if (!cell.InBounds(map))
                throw new InvalidOperationException(
                    "[Aragorn] Crypt building outside map: " + cell);
            if (cell.GetThingList(map).Any(t => t is Building && t.Spawned))
            {
                if (required)
                    AragornTrialSitePreparation.RequireBuilding(map, cell, def);
                return;
            }
            Thing objectToBuild = ThingMaker.MakeThing(def,
                def.MadeFromStuff ? Stone : null);
            GenSpawn.Spawn(objectToBuild, cell, map, WipeMode.Vanish);
            if (required)
                AragornTrialSitePreparation.RequireBuilding(map, cell, def);
        }

        // A room is fully enclosed by walls and one door. We never replace
        // a missing wall with an open passage; an invalid room fails generation.
        public static void Room(Map map, IntVec3 origin,
            int minX, int minZ, int maxX, int maxZ, int doorX, int doorZ)
        {
            if (minX + 2 >= maxX || minZ + 2 >= maxZ ||
                !(doorX == minX || doorX == maxX ||
                  doorZ == minZ || doorZ == maxZ))
                throw new ArgumentException("Invalid crypt room or door position.");
            if (!((doorX >= minX && doorX <= maxX) &&
                (doorZ >= minZ && doorZ <= maxZ)))
                throw new ArgumentException("Crypt door must be on the room perimeter.");

            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                bool border = x == minX || x == maxX || z == minZ || z == maxZ;
                if (!border) continue;
                Build(map, At(origin, x, z),
                    x == doorX && z == doorZ ? Door : Wall);
            }
            RoofRoom(map, origin, minX, minZ, maxX, maxZ);
        }

        // Roof only tiles with a supporting wall no more than six cells away
        // (RimWorld's own roof-support range). Open-air yards are never roofed.
        public static void RoofRoom(Map map, IntVec3 origin,
            int minX, int minZ, int maxX, int maxZ)
        {
            for (int x = minX + 1; x < maxX; x++)
            for (int z = minZ + 1; z < maxZ; z++)
            {
                IntVec3 p = At(origin, x, z);
                if (!p.InBounds(map) || !p.Walkable(map)) continue;
                bool supported = false;
                for (int dx = -6; dx <= 6 && !supported; dx++)
                for (int dz = -6; dz <= 6 && !supported; dz++)
                {
                    if (dx * dx + dz * dz > 36) continue;
                    IntVec3 support = p + new IntVec3(dx, 0, dz);
                    if (support.InBounds(map) && support.GetThingList(map)
                        .Any(t => t is Building && t.Spawned && t.def == Wall))
                        supported = true;
                }
                if (supported)
                    map.roofGrid.SetRoof(p, RoofDefOf.RoofConstructed);
            }
        }

        // Barricades are real passable vanilla cover buildings. Hostile ranged
        // pawns spawn on the INSIDE, one or two cells behind them, and their
        // defending Lord stays close to the defensive position.
        public static void BarricadeRow(Map map, IntVec3 origin,
            int x0, int x1, int z, int gapX = int.MinValue)
        {
            if (Cover == null) return;
            for (int x = x0; x <= x1; x++)
                if (x != gapX) Build(map, At(origin, x, z), Cover);
        }

        public static void BarricadeColumn(Map map, IntVec3 origin,
            int x, int z0, int z1, int gapZ = int.MinValue)
        {
            if (Cover == null) return;
            for (int z = z0; z <= z1; z++)
                if (z != gapZ) Build(map, At(origin, x, z), Cover);
        }

        public static List<IntVec3> HoldBehindRow(IntVec3 origin, int centerX,
            int coverZ, int depth = 2)
        {
            var positions = new List<IntVec3>();
            // Northern side of the barricades. A single squad has enough
            // distinct firing cells even at raised global danger settings.
            for (int dz = depth; dz <= depth + 2; dz++)
                for (int dx = -5; dx <= 5; dx++)
                    positions.Add(At(origin, centerX + dx, coverZ + dz));
            return positions;
        }

        public static List<IntVec3> HoldBehindColumn(IntVec3 origin,
            int coverX, int centerZ, int direction)
        {
            var positions = new List<IntVec3>();
            for (int dx = 2; dx <= 4; dx++)
                for (int dz = -5; dz <= 5; dz++)
                    positions.Add(At(origin, coverX + direction * dx, centerZ + dz));
            return positions;
        }

        // Known Profaned furniture is preferred; speculative stele defs
        // are queried safely and omitted if the installed version lacks them.
        public static void Decoration(Map map, IntVec3 origin, int x, int z,
            params string[] defNames)
        {
            IntVec3 cell = At(origin, x, z);
            if (!cell.InBounds(map) || !cell.Standable(map) ||
                cell.GetThingList(map).Any(t => t is Building || t is Pawn))
                return;
            foreach (string defName in defNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null || def.size.x != 1 || def.size.z != 1) continue;
                try
                {
                    Build(map, cell, def, required: false);
                    return;
                }
                catch (Exception e)
                {
                    Log.WarningOnce("[Aragorn] Optional Profaned decor " +
                        defName + ": " + e.Message, 380172);
                }
            }
        }

        public static void DressChapel(Map map, IntVec3 origin, int x, int z)
        {
            Decoration(map, origin, x - 3, z,
                "BotchJob_ProfanedBrazierDarklight",
                "BotchJob_ProfanedBrazier", "BotchJob_ProfanedLantern");
            Decoration(map, origin, x + 3, z,
                "BotchJob_ProfanedBrazierDarklight",
                "BotchJob_ProfanedBrazier", "BotchJob_ProfanedLantern");
            Decoration(map, origin, x, z + 3,
                "BotchJob_ProfanedStele", "BotchJob_ProfanedStela",
                "BotchJob_ProfanedThrone", "BotchJob_ProfanedLantern");
        }

        // RimWorld maps can be generated BEFORE the quest's rooms are added.
        // Force a single fog refresh after all walls/roofs exist so the sealed
        // chambers are not prematurely visible. Reflection avoids an unsafe
        // hard dependency on FogGrid's non-public API in RimWorld 1.6.
        public static void RefreshDiscoverableRooms(Map map)
        {
            if (map?.fogGrid == null) return;
            try
            {
                object updater = map.regionAndRoomUpdater;
                MethodInfo rebuild = updater?.GetType().GetMethod(
                    "RebuildAllRegionsAndRooms",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                rebuild?.Invoke(updater, null);
                object fog = map.fogGrid;
                MethodInfo refog = fog.GetType().GetMethod(
                    "Refog",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (refog != null)
                {
                    refog.Invoke(fog, null);
                    Log.Message("[Aragorn] Enclosed crypt chambers refogged after construction.");
                }
                else
                    Log.Warning("[Aragorn] FogGrid.Refog was unavailable; " +
                        "rooms are still built and roofed but fog must be checked in-game.");
            }
            catch (Exception e)
            {
                Log.Warning("[Aragorn] Room fog refresh could not complete: " + e.Message);
            }
        }

        public static void ArcherFortress(Map map, IntVec3 origin)
        {
            // Three independently enclosed tomb chambers behind three firing
            // lines. Rear doors lead into initially hidden, roofed crypts.
            for (int t = -1; t <= 1; t++)
            {
                int x = t * 20;
                Room(map, origin, x - 6, 12, x + 6, 25, x, 12);
                BarricadeRow(map, origin, x - 7, x + 7, 6, gapX: x);
                BarricadeRow(map, origin, x - 6, x - 2, -1);
                BarricadeRow(map, origin, x + 2, x + 6, -1);
                DressChapel(map, origin, x, 21);
            }
            // Fogged side barracks and rear stores. No quest objectives are
            // hidden here; they provide flank options and visual depth.
            Room(map, origin, -32, -11, -23, 1, -23, -5);
            Room(map, origin, 23, -11, 32, 1, 23, -5);
            Decoration(map, origin, -29, -2,
                "BotchJob_ProfanedLantern", "BotchJob_ProfanedBrazier");
            Decoration(map, origin, 29, -2,
                "BotchJob_ProfanedLantern", "BotchJob_ProfanedBrazier");
            BarricadeRow(map, origin, -11, -4, -13, gapX: -7);
            BarricadeRow(map, origin, 4, 11, -13, gapX: 7);
        }

        public static void AxeTemple(Map map, IntVec3 origin)
        {
            Room(map, origin, -30, -23, -9, -6, -17, -23);
            Room(map, origin, 9, -23, 30, -6, 17, -23);
            DressChapel(map, origin, -19, -11);
            DressChapel(map, origin, 19, -11);
            Room(map, origin, -8, -8, 8, 3, 0, -8);
            // The ring is a separate, enclosed 13x13 room with a single
            // southern entry; both coffins remain reachable to one challenger.
            Room(map, origin, -6, 5, 6, 17, 0, 5);
            Decoration(map, origin, -4, 14,
                "BotchJob_ProfanedLantern", "BotchJob_ProfanedBrazier");
            Decoration(map, origin, 4, 14,
                "BotchJob_ProfanedLantern", "BotchJob_ProfanedBrazier");
        }

        public static void CaptainFortress(Map map, IntVec3 origin)
        {
            for (int z = -17; z <= 5; z += 11)
            {
                for (int x = -28; x <= 28; x++)
                {
                    if (Math.Abs(x) <= 3 || Math.Abs(x) >= 26) continue;
                    if (x == -14 || x == 14)
                        Build(map, At(origin, x, z), Door);
                    else
                        Build(map, At(origin, x, z), Wall);
                }
                BarricadeRow(map, origin, -22, -5, z + 2, gapX: -14);
                BarricadeRow(map, origin, 5, 22, z + 2, gapX: 14);
            }
            Room(map, origin, -22, 8, -2, 25, -12, 8);
            Room(map, origin, 2, 8, 22, 25, 12, 8);
            DressChapel(map, origin, -15, 20);
            DressChapel(map, origin, 15, 20);
        }

        public static void KingSanctuary(Map map, IntVec3 origin)
        {
            Room(map, origin, -12, 7, 12, 25, 0, 7);
            // Central pillars support a larger vaulted hall without blocking
            // the king (z=13) or the unique sarcophagus (z=11).
            foreach (int x in new[] { -7, 7 })
                foreach (int z in new[] { 13, 19 })
                    Build(map, At(origin, x, z), Wall);
            BarricadeRow(map, origin, -24, -6, -3, gapX: -14);
            BarricadeRow(map, origin, 6, 24, -3, gapX: 14);
            DressChapel(map, origin, 0, 22);
            Room(map, origin, -30, -13, -17, -1, -23, -13);
            Room(map, origin, 17, -13, 30, -1, 23, -13);
        }
    }
}
