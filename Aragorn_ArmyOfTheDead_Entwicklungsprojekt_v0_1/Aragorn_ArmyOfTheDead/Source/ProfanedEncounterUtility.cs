using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    public static class ProfanedEncounterUtility
    {
        private const string ProfanedDefName = "BotchJob_ProfanedFaction";

        // If the Profaned faction was excluded at world creation or defeated,
        // a HIDDEN instance of the actual Profaned FactionDef is created.
        // hidden:true prevents FactionGenerator from making a settlement.
        public static Faction GetOrCreateFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(ProfanedDefName);
            if (def == null || Find.FactionManager == null)
            {
                Log.Error("[Aragorn] The Profaned FactionDef is not loaded.");
                return null;
            }
            Faction existing = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f != null && f.def == def && !f.defeated);
            if (existing != null) return existing;

            // Reuse a previously generated hidden instance even after all settlements
            // of the faction were destroyed. Avoid generating repeated factions.
            existing = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f != null && f.def == def && f.Hidden);
            if (existing != null) return existing;

            try
            {
                Faction newFaction = FactionGenerator.NewGeneratedFaction(
                    new FactionGeneratorParms(def, hidden: true));
                Find.FactionManager.Add(newFaction);
                Log.Message("[Aragorn] Hidden Profaned Legion created for quest combat (no world settlements).");
                return newFaction;
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Could not initialize hidden Profaned faction: " + ex);
                return null;
            }
        }

        public static List<Pawn> SpawnGuards(Map map, IntVec3 center, Faction faction, int requested)
        {
            var result = new List<Pawn>();
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(ProfanedDefName);
            var combat = def?.pawnGroupMakers?.FirstOrDefault(
                x => x.kindDef == PawnGroupKindDefOf.Combat);
            var options = combat?.options?.Where(x => x.kind != null && x.selectionWeight > 0).ToList()
                ?? new List<PawnGenOption>();
            if (faction == null || options.Count == 0)
            {
                Log.Error("[Aragorn] Profaned guard spawn unavailable (faction or Combat pool missing).");
                return result;
            }

            // Guarantee visible undead creatures during every encounter rather than
            // relying solely on an unlucky weighted roll of 20 humanoid options.
            var guaranteed = new[] { "BotchJob_Skeleton", "BotchJob_Wraith", "BotchJob_Ghoul" };
            int total = Mathf.Clamp(requested, 5, 50);
            for (int i = 0; i < total; i++)
            {
                PawnKindDef kind = null;
                if (i < guaranteed.Length)
                    kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(guaranteed[i]);
                if (kind == null) kind = options.RandomElementByWeight(o => o.selectionWeight).kind;
                try
                {
                    Pawn guard = PawnGenerator.GeneratePawn(kind, faction);
                    IntVec3 point = CellFinder.RandomClosewalkCellNear(center, map, 10);
                    GenSpawn.Spawn(guard, point, map);
                    result.Add(guard);
                }
                catch (Exception ex)
                {
                    Log.Warning("[Aragorn] Profaned unit spawn failed (" + kind?.defName
                        + "): " + ex.Message);
                }
            }
            Log.Message("[Aragorn] Spawned " + result.Count
                + " original Profaned pawns, allegiance=" + faction.Name);
            return result;
        }
        // Heavier military group specifically for the Anduril siege.
        // The regular crypt encounters keep their existing difficulty.
        public static List<Pawn> SpawnSiegeWave(Map map, IntVec3 edge,
            Faction faction, int requested, int waveIndex)
        {
            var result = new List<Pawn>();
            if (map == null || faction == null) return result;
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(ProfanedDefName);
            var combat = factionDef?.pawnGroupMakers?.FirstOrDefault(x =>
                x.kindDef == PawnGroupKindDefOf.Combat);
            var common = combat?.options?.Where(x =>
                x.kind != null && x.selectionWeight > 0).ToList()
                ?? new List<PawnGenOption>();
            if (common.Count == 0)
            {
                Log.Error("[Aragorn] Profaned siege cannot find its Combat pawn pool.");
                return result;
            }
            // These are the actual Profaned 1.6 PawnKindDefs, not generic medieval fighters.
            string[] hardNames = {
                "BotchJob_ScourgeKnight", "BotchJob_Gravetender",
                "BotchJob_CryptLurker", "BotchJob_ScourgeLord",
                "BotchJob_ProfanedCursebearer", "BotchJob_UndeadColossus",
                "BotchJob_UndeadWarg", "BotchJob_Wraith"
            };
            var hard = hardNames.Select(DefDatabase<PawnKindDef>.GetNamedSilentFail)
                .Where(k => k != null).ToList();
            int total = Mathf.Clamp(requested, 10, 80);
            int eliteCount = hard.Count == 0 ? 0 :
                Mathf.Clamp(Mathf.RoundToInt(total * (.25f + waveIndex * .018f)), 0, total);
            for (int i = 0; i < total; i++)
            {
                PawnKindDef kind;
                if (i < eliteCount)
                {
                    // Stronger creatures become common in later waves.
                    int eliteIndex = (waveIndex >= 7 && i % 5 == 0)
                        ? Rand.Range(Math.Min(3, hard.Count), hard.Count)
                        : Rand.Range(0, hard.Count);
                    kind = hard[eliteIndex];
                }
                else
                    kind = common.RandomElementByWeight(x => x.selectionWeight).kind;
                try
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                    IntVec3 pos = CellFinder.RandomClosewalkCellNear(edge, map, 12);
                    GenSpawn.Spawn(pawn, pos, map);
                    result.Add(pawn);
                }
                catch (Exception ex)
                {
                    Log.Warning("[Aragorn] Siege enemy failed to spawn (" +
                        kind?.defName + "): " + ex.Message);
                }
            }
            Log.Message("[Aragorn] Forge siege wave " + (waveIndex + 1) +
                ": " + result.Count + " undead, including up to " + eliteCount +
                " elite units.");
            return result;
        }
    }

    // A small walkable crypt as the first playable structure prototype.
    // Does not depend on Custom Quest Framework / removed underground maps.
    public static class AragornCryptBuilder
    {
        // The actual Profaned mod provides KCSG layout BotchJob_DarkCrypt.
        // We use its native slate, lanterns, tombs and braziers as inspiration
        // rather than depending on a KCSG internal layout-generation API.
        private static void ClearVegetation(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return;
            foreach (Thing thing in cell.GetThingList(map).ToList())
                if (thing.def.category == ThingCategory.Plant)
                    thing.Destroy(DestroyMode.Vanish);
        }

        public static IntVec3 BuildRescuePrison(Map map, IntVec3 near, out IntVec3 guardPost)
        {
            guardPost = Build(map, near); // hostile keep on the opposite side
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");
            IntVec3 jail = guardPost + new IntVec3(23, 0, 0);
            if (wall == null || door == null || stone == null ||
                !jail.InBounds(map)) return guardPost + new IntVec3(12, 0, 0);
            for (int dx = -6; dx <= 6; dx++)
            for (int dz = -5; dz <= 5; dz++)
            {
                IntVec3 cell = jail + new IntVec3(dx, 0, dz);
                if (!cell.InBounds(map)) continue;
                ClearVegetation(map, cell);
                if (floor != null && cell.Standable(map))
                {
                    try { map.terrainGrid.SetTerrain(cell, floor); } catch { }
                }
                if (Math.Abs(dx) != 6 && Math.Abs(dz) != 5) continue;
                ThingDef structure = (dx == -6 && dz == 0) ? door : wall;
                if (cell.GetThingList(map).Any(t => t is Building)) continue;
                try
                {
                    Thing thing = ThingMaker.MakeThing(structure, stone);
                    GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
                }
                catch (Exception e) { Log.Warning("[Aragorn] Prison wall failed: " + e.Message); }
            }
            ThingDef bed = DefDatabase<ThingDef>.GetNamedSilentFail("Bed");
            if (bed != null)
            {
                IntVec3 bedCell = jail + new IntVec3(2, 0, -2);
                if (bedCell.InBounds(map) && bedCell.Standable(map))
                {
                    try { GenSpawn.Spawn(ThingMaker.MakeThing(bed,
                        bed.MadeFromStuff ? stone : null),
                        bedCell, map, WipeMode.Vanish); }
                    catch { }
                }
            }
            Log.Message("[Aragorn] Separate prison cell generated at " + jail +
                " (Profaned guards remain by " + guardPost + ").");
            return jail;
        }

        private static void MakeWing(Map map, IntVec3 center, ThingDef wall,
            ThingDef door, ThingDef stone, TerrainDef floor)
        {
            for (int dx = -5; dx <= 5; dx++)
            for (int dz = -4; dz <= 4; dz++)
            {
                IntVec3 c = center + new IntVec3(dx, 0, dz);
                if (!c.InBounds(map)) continue;
                ClearVegetation(map, c);
                if (floor != null && c.Standable(map))
                {
                    try { map.terrainGrid.SetTerrain(c, floor); } catch { }
                }
                if (Math.Abs(dx) != 5 && Math.Abs(dz) != 4) continue;
                ThingDef structure = dx == 0 && Math.Abs(dz) == 4 ? door : wall;
                if (c.GetThingList(map).Any(t => t is Building)) continue;
                try { GenSpawn.Spawn(ThingMaker.MakeThing(structure, stone),
                    c, map, WipeMode.Vanish); }
                catch { }
            }
            AddCryptProp(map, center + new IntVec3(3, 0, 0),
                "BotchJob_ProfanedBrazier", stone);
            AddCryptProp(map, center + new IntVec3(-3, 0, 0),
                "BotchJob_ProfanedLantern", stone);
            AddCryptProp(map, center + new IntVec3(0, 0, 1),
                "Sarcophagus", stone);
        }

        private static void AddCryptProp(Map map, IntVec3 cell,
            string defName, ThingDef stuff)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || !cell.InBounds(map) ||
                cell.GetThingList(map).Any(t => t is Building)) return;
            try
            {
                GenSpawn.Spawn(ThingMaker.MakeThing(def,
                    def.MadeFromStuff ? stuff : null), cell, map, WipeMode.Vanish);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Aragorn] Profaned crypt prop " + defName +
                    " failed: " + e.Message, 731116);
            }
        }

        public static IntVec3 Build(Map map, IntVec3 near)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            ThingDef sarcophagus = DefDatabase<ThingDef>.GetNamedSilentFail("Sarcophagus");
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");
            if (wall == null || door == null || stone == null) return near;

            // Center target is chosen on the open map; place a 15x13 enclosure.
            IntVec3 center = CellFinder.RandomClosewalkCellNear(near, map, 25);
            const int rx = 7, rz = 6;
            for (int dx = -rx; dx <= rx; dx++)
            for (int dz = -rz; dz <= rz; dz++)
            {
                IntVec3 cell = center + new IntVec3(dx, 0, dz);
                if (!cell.InBounds(map)) continue;
                ClearVegetation(map, cell);
                if (floor != null && cell.Walkable(map))
                {
                    try { map.terrainGrid.SetTerrain(cell, floor); } catch { }
                }
                if (Math.Abs(dx) != rx && Math.Abs(dz) != rz) continue;
                // Entrances on north and south sides.
                ThingDef structure = dx == 0 && Math.Abs(dz) == rz ? door : wall;
                try
                {
                    if (cell.GetThingList(map).Any(t => t is Building)) continue;
                    var thing = ThingMaker.MakeThing(structure, stone);
                    GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("[Aragorn] Crypt structure spawn warning: "
                        + ex.Message, 948201);
                }
            }
            if (sarcophagus != null)
            {
                foreach (int dz in new[] { -3, 0, 3 })
                foreach (int dx in new[] { -4, 4 })
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map) || !cell.Standable(map)) continue;
                    try
                    {
                        Thing tomb = ThingMaker.MakeThing(sarcophagus, stone);
                        GenSpawn.Spawn(tomb, cell, map, WipeMode.Vanish);
                    }
                    catch { }
                }
            }
            // Two connected-looking crypt chapels using the Profaned mod's
            // original lanterns and braziers. These are open, lootable ruins.
            MakeWing(map, center + new IntVec3(0, 0, 14),
                wall, door, stone, floor);
            MakeWing(map, center + new IntVec3(0, 0, -14),
                wall, door, stone, floor);
            AddCryptProp(map, center + new IntVec3(0, 0, -2),
                "BotchJob_ProfanedThrone", stone);
            AddCryptProp(map, center + new IntVec3(0, 0, 3),
                "BotchJob_ProfanedBrazierDarklight", stone);
            Log.Message("[Aragorn] Three-room Profaned crypt with DarkCrypt-inspired decorations generated at " + center);
            return center;
        }
    }
}
