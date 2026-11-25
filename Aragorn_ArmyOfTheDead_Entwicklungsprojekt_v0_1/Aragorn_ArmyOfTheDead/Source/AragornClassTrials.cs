using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AragornArmy
{
    // Six mission types, thirteen ghosts. Separate file keeps saved v0.33
    // progression, all weapon rendering and existing quests untouched.
    public sealed partial class AragornGame
    {
        private static readonly int[] TrialBeginnings = { 0, 3, 6, 8, 10, 12 };
        private static readonly int[] TrialEnds = { 3, 6, 8, 10, 12, 13 };
        private bool trialCampaignActive;
        private List<Pawn> trialGhosts = new List<Pawn>();
        private List<Thing> trialMarkers = new List<Thing>();
        private Pawn trialDuelChampion;
        private Pawn trialDuelEnemy;
        private IntVec3 trialFortCenter = IntVec3.Invalid;
        private IntVec3 trialRingCenter = IntVec3.Invalid;
        private int trialGravesOpened;
        private int trialDuelVictories;
        private int trialWavesSpawned;
        private int trialNextWaveTick;
        private int trialNextMagicTick;

        private static int ClassNumber(int unlocked)
        {
            for (int i = 0; i < TrialEnds.Length; i++)
                if (unlocked < TrialEnds[i]) return i;
            return 5;
        }
        private static int ClassStart(int unlocked) => TrialBeginnings[ClassNumber(unlocked)];
        private static int ClassEnd(int unlocked) => TrialEnds[ClassNumber(unlocked)];

        private void ExposeCampaignTrial()
        {
            Scribe_Values.Look(ref trialCampaignActive, "arTrialCampaignActive", false);
            Scribe_Collections.Look(ref trialGhosts, "arTrialGhosts", LookMode.Reference);
            Scribe_Collections.Look(ref trialMarkers, "arTrialMarkers", LookMode.Reference);
            Scribe_References.Look(ref trialDuelChampion, "arTrialDuelChampion");
            Scribe_References.Look(ref trialDuelEnemy, "arTrialDuelEnemy");
            Scribe_Values.Look(ref trialFortCenter, "arTrialFortCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref trialRingCenter, "arTrialRingCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref trialGravesOpened, "arTrialGravesOpened", 0);
            Scribe_Values.Look(ref trialDuelVictories, "arTrialDuelVictories", 0);
            Scribe_Values.Look(ref trialWavesSpawned, "arTrialWavesSpawned", 0);
            Scribe_Values.Look(ref trialNextWaveTick, "arTrialNextWaveTick", 0);
            Scribe_Values.Look(ref trialNextMagicTick, "arTrialNextMagicTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (trialGhosts == null) trialGhosts = new List<Pawn>();
                if (trialMarkers == null) trialMarkers = new List<Thing>();
            }
        }

        private void ResetCampaignTrial()
        {
            trialCampaignActive = false;
            trialGhosts.Clear();
            trialMarkers.Clear();
            trialDuelChampion = null;
            trialDuelEnemy = null;
            trialFortCenter = IntVec3.Invalid;
            trialRingCenter = IntVec3.Invalid;
            trialGravesOpened = 0;
            trialDuelVictories = 0;
            trialWavesSpawned = 0;
            trialNextWaveTick = 0;
            trialNextMagicTick = 0;
        }

        private static bool Cleared(IEnumerable<Pawn> pawns) =>
            pawns == null || pawns.All(p => p == null || !p.Spawned || p.Dead || p.Downed);

        private int RemainingMarkers => trialMarkers.Count(t => t != null && t.Spawned && !t.Destroyed);

        private Pawn SpawnQuestGhost(int index, IntVec3 spot, Faction hostile)
        {
            Pawn ghost = CreateGhost(index, hostile);
            if (ghost == null) return null;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(spot, trialMap, 3);
            GenSpawn.Spawn(ghost, cell, trialMap);
            trialGhosts.Add(ghost);
            if (trialBoss == null) trialBoss = ghost;
            return ghost;
        }

        private Thing AddTrialMarker(IntVec3 at)
        {
            ThingDef sarcophagus = DefDatabase<ThingDef>.GetNamedSilentFail("Sarcophagus");
            if (sarcophagus == null) return null;
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            try
            {
                Thing marker = ThingMaker.MakeThing(sarcophagus,
                    sarcophagus.MadeFromStuff ? stone : null);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(at, trialMap, 2);
                GenSpawn.Spawn(marker, cell, trialMap, WipeMode.Vanish);
                trialMarkers.Add(marker);
                return marker;
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Could not create mission objective: " + ex);
                return null;
            }
        }

        private void AddDefenders(IntVec3 point, Faction hostile, int baseAmount, string role)
        {
            trialGuards.AddRange(ProfanedEncounterUtility.SpawnThemedGuards(
                trialMap, point, hostile, baseAmount, role));
        }

        private void GuardMission(Faction hostile)
        {
            var defenders = trialGuards.Concat(trialGhosts)
                .Where(p => p != null && p.Spawned && !p.Dead).Distinct().ToList();
            if (defenders.Count == 0) return;
            LordMaker.MakeNewLord(hostile,
                new LordJob_DefendPoint(trialFortCenter, wanderRadius: 10f,
                    defendRadius: 40f, isCaravanSendable: false, addFleeToil: false),
                trialMap, defenders);
        }

        private void PopulateClassTrial(Map map)
        {
            if (map == null || trialSite == null || trialSite != map.Parent ||
                trialIndex < 0 || trialMap != null) return;
            Faction hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null) { Log.Error("[Aragorn] Missing Profaned quest faction."); return; }
            try
            {
                trialMap = map;
                trialCampaignActive = true;
                trialGhosts.Clear();
                trialGuards.Clear();
                trialMarkers.Clear();
                trialBoss = null;
                int cls = ClassNumber(trialIndex);
                trialFortCenter = AragornTrialStructures.Build(map, cls);
                trialRingCenter = trialFortCenter + new IntVec3(0, 0, 11);
                trialNextMagicTick = Ticks + 480;
                switch (cls)
                {
                    case 0: // Three graves after a corridor of separate Profaned patrols.
                        for (int z = -14; z <= 6; z += 7)
                            AddDefenders(trialFortCenter + new IntVec3(0, 0, z),
                                hostile, 3, "undead");
                        for (int i = 0; i < 3; i++)
                            AddTrialMarker(trialFortCenter + new IntVec3((i - 1) * 7, 0, 15));
                        break;
                    case 1: // Three high-cover bow platforms.
                        for (int i = 0; i < 3; i++)
                        {
                            IntVec3 tower = trialFortCenter + new IntVec3((i - 1) * 13, 0, 9);
                            SpawnQuestGhost(3 + i, tower, hostile);
                            AddDefenders(tower + new IntVec3(0, 0, -5), hostile, 9, "archers");
                        }
                        AddTrialMarker(trialFortCenter);
                        break;
                    case 2: // Two bombardiers and a defended curse ritual.
                        SpawnQuestGhost(6, trialFortCenter + new IntVec3(-8, 0, 8), hostile);
                        SpawnQuestGhost(7, trialFortCenter + new IntVec3(8, 0, 8), hostile);
                        AddDefenders(trialFortCenter + new IntVec3(-9, 0, 0), hostile, 10, "mages");
                        AddDefenders(trialFortCenter + new IntVec3(9, 0, 0), hostile, 10, "mages");
                        AddTrialMarker(trialFortCenter + new IntVec3(-5, 0, 2));
                        AddTrialMarker(trialFortCenter + new IntVec3(5, 0, 2));
                        break;
                    case 3: // Temple first; two sequential one-on-one axe duels later.
                        AddDefenders(trialFortCenter + new IntVec3(-8, 0, -9), hostile, 8, "ghouls");
                        AddDefenders(trialFortCenter + new IntVec3(8, 0, -9), hostile, 8, "ghouls");
                        break;
                    case 4: // Two captains hold the inner fort behind three waves.
                        SpawnQuestGhost(10, trialFortCenter + new IntVec3(-7, 0, 12), hostile);
                        SpawnQuestGhost(11, trialFortCenter + new IntVec3(7, 0, 12), hostile);
                        AddDefenders(trialFortCenter + new IntVec3(0, 0, -13), hostile, 8, "elite");
                        trialWavesSpawned = 1;
                        break;
                    case 5: // King, three oath-seals, elite guard and lightning.
                        SpawnQuestGhost(12, trialFortCenter + new IntVec3(0, 0, 13), hostile);
                        AddDefenders(trialFortCenter + new IntVec3(-9, 0, 5), hostile, 10, "elite");
                        AddDefenders(trialFortCenter + new IntVec3(9, 0, 5), hostile, 10, "elite");
                        for (int i = 0; i < 3; i++)
                            AddTrialMarker(trialFortCenter + new IntVec3((i - 1) * 9, 0, -1));
                        break;
                }
                GuardMission(hostile);
                Messages.Message(AragornText.F("AR_Class_Encounter", cls + 1),
                    MessageTypeDefOf.ThreatBig);
                Log.Message("[Aragorn] Class trial " + (cls + 1) +
                    "/6: " + trialGhosts.Count + " ghosts, " + trialGuards.Count +
                    " Profaned defenders, " + trialMarkers.Count + " objectives.");
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Failed to populate six-class trial: " + ex);
                // Leave the mission available for failure/retry rather than
                // awarding progress for an incomplete generated level.
            }
        }

        public IEnumerable<Gizmo> GetClassTrialGizmos(Pawn pawn)
        {
            if (!trialCampaignActive || trialMap == null || pawn == null ||
                pawn.Faction != Faction.OfPlayer || !pawn.Spawned ||
                pawn.Map != trialMap || !pawn.RaceProps.Humanlike) yield break;
            int cls = ClassNumber(trialIndex);
            if (cls == 0 && trialGravesOpened < 3)
            {
                Thing grave = trialMarkers.Skip(trialGravesOpened).FirstOrDefault();
                var cmd = new Command_Action {
                    defaultLabel = AragornText.T("AR_Class_OpenGrave"),
                    defaultDesc = AragornText.T("AR_Class_OpenGraveDesc"),
                    action = () => OpenTrialGrave(pawn)
                };
                if (grave == null || !grave.Spawned ||
                    pawn.Position.DistanceTo(grave.Position) > 3f)
                    cmd.Disable(AragornText.T("AR_Class_GetCloser"));
                yield return cmd;
            }
            else if ((cls == 1 || cls == 2 || cls == 5) && RemainingMarkers > 0)
            {
                Thing nearest = trialMarkers.Where(t => t != null && t.Spawned)
                    .OrderBy(t => t.Position.DistanceTo(pawn.Position)).FirstOrDefault();
                var cmd = new Command_Action {
                    defaultLabel = AragornText.T("AR_Class_BreakSeal"),
                    defaultDesc = AragornText.T("AR_Class_BreakSealDesc"),
                    action = () => BreakTrialSeal(pawn)
                };
                if (nearest == null || pawn.Position.DistanceTo(nearest.Position) > 3f)
                    cmd.Disable(AragornText.T("AR_Class_GetCloser"));
                yield return cmd;
            }
            else if (cls == 3 && trialDuelVictories < 2)
            {
                var cmd = new Command_Action {
                    defaultLabel = AragornText.T("AR_Class_StartDuel"),
                    defaultDesc = AragornText.T("AR_Class_StartDuelDesc"),
                    action = () => BeginAxeDuel(pawn)
                };
                if (!Cleared(trialGuards))
                    cmd.Disable(AragornText.T("AR_Class_ClearTemple"));
                else if (pawn.Position.DistanceTo(trialRingCenter) > 5f)
                    cmd.Disable(AragornText.T("AR_Class_EnterArena"));
                else if (trialDuelEnemy != null && trialDuelEnemy.Spawned &&
                    !trialDuelEnemy.Dead && !trialDuelEnemy.Downed &&
                    trialDuelChampion != null && trialDuelChampion.Spawned &&
                    !trialDuelChampion.Dead && !trialDuelChampion.Downed)
                    cmd.Disable(AragornText.T("AR_Class_DuelInProgress"));
                else if (trialMap.mapPawns.AllPawnsSpawned.Any(other =>
                    other != pawn && other.Faction == Faction.OfPlayer &&
                    other.Position.DistanceTo(trialRingCenter) < 7f))
                    cmd.Disable(AragornText.T("AR_Class_DuelAlone"));
                yield return cmd;
            }
        }

        private void OpenTrialGrave(Pawn pawn)
        {
            if (trialMap == null || trialGravesOpened >= 3 ||
                trialMarkers.Count <= trialGravesOpened) return;
            Thing grave = trialMarkers[trialGravesOpened];
            if (grave == null || !grave.Spawned ||
                pawn.Position.DistanceTo(grave.Position) > 3f) return;
            IntVec3 point = grave.Position;
            grave.Destroy(DestroyMode.Vanish);
            int index = trialGravesOpened++;
            Pawn ghost = SpawnQuestGhost(index, point,
                ProfanedEncounterUtility.GetOrCreateFaction());
            if (ghost != null)
                LordMaker.MakeNewLord(ghost.Faction,
                    new LordJob_DefendPoint(point, wanderRadius: 5f,
                        defendRadius: 14f, isCaravanSendable: false, addFleeToil: false),
                    trialMap, new List<Pawn> { ghost });
            Messages.Message(AragornText.F("AR_Class_GraveAwakes", trialGravesOpened),
                pawn, MessageTypeDefOf.ThreatBig);
        }

        private void BreakTrialSeal(Pawn pawn)
        {
            Thing nearest = trialMarkers.Where(t => t != null && t.Spawned)
                .OrderBy(t => t.Position.DistanceTo(pawn.Position)).FirstOrDefault();
            if (nearest == null || pawn.Position.DistanceTo(nearest.Position) > 3f) return;
            Vector3 dustAt = nearest.DrawPos;
            nearest.Destroy(DestroyMode.Vanish);
            FleckMaker.ThrowDustPuffThick(dustAt, trialMap, 2f,
                new Color(.15f, .86f, .7f));
            if (ClassNumber(trialIndex) == 5 && RemainingMarkers == 0)
                Messages.Message(AragornText.T("AR_Class_KingShieldBroken"),
                    pawn, MessageTypeDefOf.PositiveEvent);
            else
                Messages.Message(AragornText.F("AR_Class_SealsLeft", RemainingMarkers),
                    pawn, MessageTypeDefOf.NeutralEvent);
        }

        private void BeginAxeDuel(Pawn pawn)
        {
            if (trialMap == null || trialDuelVictories >= 2 || !Cleared(trialGuards) ||
                pawn == null || pawn.Downed || pawn.Position.DistanceTo(trialRingCenter) > 5f)
                return;
            if (trialMap.mapPawns.AllPawnsSpawned.Any(other =>
                other != pawn && other.Faction == Faction.OfPlayer &&
                other.Position.DistanceTo(trialRingCenter) < 7f))
                return;
            if (trialDuelEnemy != null && trialDuelEnemy.Spawned &&
                !trialDuelEnemy.Dead && !trialDuelEnemy.Downed &&
                trialDuelChampion != null && trialDuelChampion.Spawned &&
                !trialDuelChampion.Dead && !trialDuelChampion.Downed) return;
            trialDuelChampion = pawn;
            if (trialDuelEnemy == null || trialDuelEnemy.Dead || trialDuelEnemy.Downed ||
                !trialDuelEnemy.Spawned)
            {
                Pawn ghost = SpawnQuestGhost(8 + trialDuelVictories,
                    trialRingCenter + new IntVec3(0, 0, 3),
                    ProfanedEncounterUtility.GetOrCreateFaction());
                trialDuelEnemy = ghost;
                if (ghost != null)
                    LordMaker.MakeNewLord(ghost.Faction,
                        new LordJob_DefendPoint(trialRingCenter,
                            wanderRadius: 3f, defendRadius: 8f,
                            isCaravanSendable: false, addFleeToil: false),
                        trialMap, new List<Pawn> { ghost });
            }
            Messages.Message(AragornText.F("AR_Class_DuelRound", trialDuelVictories + 1),
                pawn, MessageTypeDefOf.ThreatBig);
        }

        // Enforced 1v1: spectators and other attackers cannot hurt duelists
        // and the King is protected until all three seals are broken.
        public bool AllowTrialDamage(Thing target, Thing attacker)
        {
            if (!trialCampaignActive || trialMap == null || target == null ||
                target.Map != trialMap) return true;
            if (ClassNumber(trialIndex) == 5 && trialGhosts.Count > 0 &&
                target == trialGhosts[0] && RemainingMarkers > 0) return false;
            if (ClassNumber(trialIndex) != 3 || trialDuelEnemy == null ||
                trialDuelChampion == null || !trialDuelEnemy.Spawned ||
                trialDuelEnemy.Dead || trialDuelEnemy.Downed) return true;
            if (target == trialDuelEnemy) return attacker == trialDuelChampion;
            if (target == trialDuelChampion) return attacker == trialDuelEnemy;
            if (attacker == trialDuelEnemy) return target == trialDuelChampion;
            return true;
        }

        private void TickClassTrial()
        {
            if (!trialCampaignActive || trialIndex < 0 || trialMap == null) return;
            int cls = ClassNumber(trialIndex);
            if (cls == 3 && trialDuelEnemy != null)
            {
                if (trialDuelEnemy.Dead || trialDuelEnemy.Downed || !trialDuelEnemy.Spawned)
                {
                    trialDuelVictories++;
                    trialDuelEnemy = null;
                    trialDuelChampion = null;
                    Messages.Message(AragornText.F("AR_Class_DuelWon", trialDuelVictories),
                        MessageTypeDefOf.PositiveEvent);
                }
                else if (trialDuelChampion == null || trialDuelChampion.Dead ||
                    trialDuelChampion.Downed || !trialDuelChampion.Spawned)
                {
                    trialDuelChampion = null; // another pawn may challenge the survivor
                    Messages.Message(AragornText.T("AR_Class_DuelNeedChampion"),
                        MessageTypeDefOf.RejectInput);
                }
            }
            if (cls == 4 && Cleared(trialGuards) && trialWavesSpawned < 3)
            {
                if (trialNextWaveTick <= 0) trialNextWaveTick = Ticks + 240;
                if (Ticks >= trialNextWaveTick)
                {
                    trialGuards.Clear();
                    trialWavesSpawned++;
                    trialNextWaveTick = 0;
                    var hostile = ProfanedEncounterUtility.GetOrCreateFaction();
                    IntVec3 gate = trialFortCenter + new IntVec3(
                        trialWavesSpawned % 2 == 0 ? -14 : 14, 0, -13);
                    AddDefenders(gate, hostile, 8 + trialWavesSpawned * 2, "elite");
                    GuardMission(hostile);
                    Messages.Message(AragornText.F("AR_Class_WaveArrives", trialWavesSpawned),
                        MessageTypeDefOf.ThreatBig);
                }
            }
            if ((cls == 2 || cls == 5) && Ticks >= trialNextMagicTick &&
                (cls == 5 || RemainingMarkers > 0))
            {
                trialNextMagicTick = Ticks + (cls == 5 ? 330 : 540);
                var candidates = trialMap.mapPawns.AllPawnsSpawned.Where(p =>
                    p != null && p.Spawned && !p.Dead && p.Faction == Faction.OfPlayer &&
                    p.RaceProps.Humanlike).ToList();
                if (candidates.Count > 0)
                {
                    Pawn victim = candidates.RandomElement();
                    trialMap.weatherManager.eventHandler.AddEvent(
                        new WeatherEvent_LightningStrike(trialMap, victim.Position));
                    // Vanilla weather bolt provides its own visual and damage.
                }
            }
            bool complete = false;
            switch (cls)
            {
                case 0:
                    complete = trialGravesOpened == 3 && trialGhosts.Count == 3 &&
                        Cleared(trialGhosts) && Cleared(trialGuards);
                    break;
                case 1:
                    complete = trialGhosts.Count == 3 && Cleared(trialGhosts) &&
                        Cleared(trialGuards) && RemainingMarkers == 0;
                    break;
                case 2:
                    complete = trialGhosts.Count == 2 && Cleared(trialGhosts) &&
                        Cleared(trialGuards) && RemainingMarkers == 0;
                    break;
                case 3:
                    complete = trialDuelVictories >= 2 && Cleared(trialGuards);
                    break;
                case 4:
                    complete = trialWavesSpawned >= 3 && trialGhosts.Count == 2 &&
                        Cleared(trialGhosts) && Cleared(trialGuards);
                    break;
                case 5:
                    complete = trialGhosts.Count == 1 && Cleared(trialGhosts) &&
                        Cleared(trialGuards) && RemainingMarkers == 0;
                    break;
            }
            if (!complete) return;
            if (trialAragorn == null || !trialAragorn.Spawned ||
                trialAragorn.Dead || trialAragorn.Map != trialMap)
            {
                if (trialQuest != null && !trialQuest.Historical)
                    trialQuest.End(QuestEndOutcome.Fail, sendLetter: true);
                QueueTrialRetry();
                return;
            }
            Unlocked = Math.Max(Unlocked, ClassEnd(trialIndex));
            if (trialQuest != null && !trialQuest.Historical)
                trialQuest.End(QuestEndOutcome.Success, sendLetter: true);
            Messages.Message(AragornText.F("AR_Class_Completed",
                cls + 1, Unlocked), trialAragorn, MessageTypeDefOf.PositiveEvent);
            trialBoss = null;
            trialSite = null;
            trialQuest = null;
            trialMap = null;
            trialAragorn = null;
            trialIndex = -1;
            trialGuards.Clear();
            trialAbandonedSinceTick = -1;
            ResetCampaignTrial();
        }
    }

    // Spectators cannot intervene in the two sacred axe duels.
    // The King's oath-seals block damage until the ritual is completed.
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class AragornTrialProtectedDamage
    {
        private static bool Prefix(Thing __instance, DamageInfo dinfo,
            ref DamageWorker.DamageResult __result)
        {
            var game = Current.Game?.GetComponent<AragornGame>();
            if (game == null || game.AllowTrialDamage(__instance, dinfo.Instigator))
                return true;
            __result = new DamageWorker.DamageResult();
            return false;
        }
    }

    // Vanilla-compatible 2D fortifications; no extra multi-floor mod.
    internal static class AragornTrialStructures
    {
        private static void FloorAndClear(Map map, IntVec3 p, bool stoneRoof)
        {
            if (!p.InBounds(map)) return;
            foreach (Thing thing in p.GetThingList(map).ToList())
                if (thing.def.category == ThingCategory.Plant ||
                    thing.def.category == ThingCategory.Item && !thing.def.EverHaulable)
                    thing.Destroy(DestroyMode.Vanish);
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");
            if (floor != null && p.Standable(map))
                try { map.terrainGrid.SetTerrain(p, floor); } catch { }
            if (stoneRoof && p.Walkable(map))
                try { map.roofGrid.SetRoof(p, RoofDefOf.RoofRockThick); } catch { }
        }

        private static void Place(Map map, IntVec3 p, string defName)
        {
            if (!p.InBounds(map) || p.GetThingList(map).Any(t => t is Building)) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            if (def == null) return;
            try
            {
                Thing obj = ThingMaker.MakeThing(def, def.MadeFromStuff ? stone : null);
                GenSpawn.Spawn(obj, p, map, WipeMode.Vanish);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Aragorn] Trial structure " + defName + ": " + e.Message, 104308);
            }
        }

        private static void OuterFort(Map map, IntVec3 center, int halfWidth,
            int halfHeight, bool roof)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            for (int z = -halfHeight; z <= halfHeight; z++)
            {
                IntVec3 cell = center + new IntVec3(x, 0, z);
                FloorAndClear(map, cell, roof && Math.Abs(x) < halfWidth && Math.Abs(z) < halfHeight);
                bool border = Math.Abs(x) == halfWidth || Math.Abs(z) == halfHeight;
                if (!border) continue;
                // South entrance; large flanking corridors remain traversable.
                bool entrance = z == -halfHeight && Math.Abs(x) <= 2;
                if (!entrance) Place(map, cell, "Wall");
                else if (x == 0) Place(map, cell, "Door");
            }
        }

        public static IntVec3 Build(Map map, int cls)
        {
            IntVec3 c = CellFinder.RandomClosewalkCellNear(map.Center, map, 18);
            bool mountain = cls == 0;
            OuterFort(map, c, cls == 0 ? 22 : 24, 19, mountain);
            if (cls == 0)
            {
                // Mountain chapel: stone roof, deep floor, three bends and
                // separated patrol rooms before the three sealed tombs.
                for (int z = -7; z <= 7; z += 7)
                for (int x = -20; x <= 20; x++)
                {
                    if (Math.Abs(x) <= 2 || Math.Abs(x) >= 16) continue;
                    Place(map, c + new IntVec3(x, 0, z), "Wall");
                }
            }
            else if (cls == 1)
            {
                // Three low-walled ranged platforms with open firing lines.
                for (int t = -1; t <= 1; t++)
                {
                    IntVec3 tower = c + new IntVec3(t * 13, 0, 9);
                    for (int x = -4; x <= 4; x++)
                    for (int z = -3; z <= 3; z++)
                        if (Math.Abs(x) == 4 || Math.Abs(z) == 3)
                            if ((x + z) % 3 != 0)
                                Place(map, tower + new IntVec3(x, 0, z), "Barricade");
                }
            }
            else if (cls == 3)
            {
                // A single arena in the inner temple. Only one player pawn
                // may activate each duel; outside shots are also blocked in code.
                IntVec3 ring = c + new IntVec3(0, 0, 11);
                for (int x = -6; x <= 6; x++)
                for (int z = -6; z <= 6; z++)
                {
                    if (Math.Abs(x) != 6 && Math.Abs(z) != 6) continue;
                    if (x == 0 && z == -6)
                        Place(map, ring + new IntVec3(x, 0, z), "Door");
                    else
                        Place(map, ring + new IntVec3(x, 0, z), "Wall");
                }
            }
            else if (cls == 5)
            {
                // Throne and triple cursed-seal positions. Bolts are genuine
                // RimWorld weather events with warning flash.
                Place(map, c + new IntVec3(0, 0, 16), "Sarcophagus");
            }
            return c;
        }
    }
}
