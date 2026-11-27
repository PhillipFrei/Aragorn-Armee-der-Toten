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
        // Objective state, NOT physical destruction. All quest sarcophagi
        // remain spawned and immune to all Thing damage/destruction.
        private List<int> trialActivatedSarcophagi = new List<int>();
        private int trialCurrentDuelGrave = -1;
        private int trialKingPhase;
        private int trialKingPhaseTick;
        private bool trialSarcophagusGuideShown;
        private bool trialCaptainsReadyShown;
        private Pawn trialDuelChampion;
        private Pawn trialDuelEnemy;
        private IntVec3 trialFortCenter = IntVec3.Invalid;
        private IntVec3 trialRingCenter = IntVec3.Invalid;
        private int trialGravesOpened;
        // Stable per-grave IDs permit opening any of the three tombs first.
        // Old v0.35 saves only stored trialGravesOpened (sequential order).
        private List<int> trialOpenedGraveIndices = new List<int>();
        private bool trialGraveGuidanceShown;
        private bool trialGravesReadyShown;
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
            Scribe_Collections.Look(ref trialActivatedSarcophagi,
                "arTrialActivatedSarcophagi", LookMode.Value);
            Scribe_Values.Look(ref trialCurrentDuelGrave, "arTrialCurrentDuelGrave", -1);
            Scribe_Values.Look(ref trialKingPhase, "arTrialKingPhase", 0);
            Scribe_Values.Look(ref trialKingPhaseTick, "arTrialKingPhaseTick", 0);
            Scribe_Values.Look(ref trialSarcophagusGuideShown, "arTrialSarcophagusGuideShown", false);
            Scribe_Values.Look(ref trialCaptainsReadyShown, "arTrialCaptainsReadyShown", false);
            Scribe_References.Look(ref trialDuelChampion, "arTrialDuelChampion");
            Scribe_References.Look(ref trialDuelEnemy, "arTrialDuelEnemy");
            Scribe_Values.Look(ref trialFortCenter, "arTrialFortCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref trialRingCenter, "arTrialRingCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref trialGravesOpened, "arTrialGravesOpened", 0);
            Scribe_Collections.Look(ref trialOpenedGraveIndices, "arTrialOpenedGraveIndices", LookMode.Value);
            Scribe_Values.Look(ref trialGraveGuidanceShown, "arTrialGraveGuidanceShown", false);
            Scribe_Values.Look(ref trialGravesReadyShown, "arTrialGravesReadyShown", false);
            Scribe_Values.Look(ref trialDuelVictories, "arTrialDuelVictories", 0);
            Scribe_Values.Look(ref trialWavesSpawned, "arTrialWavesSpawned", 0);
            Scribe_Values.Look(ref trialNextWaveTick, "arTrialNextWaveTick", 0);
            Scribe_Values.Look(ref trialNextMagicTick, "arTrialNextMagicTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (trialGhosts == null) trialGhosts = new List<Pawn>();
                if (trialMarkers == null) trialMarkers = new List<Thing>();
                if (trialActivatedSarcophagi == null)
                    trialActivatedSarcophagi = new List<int>();
                // Old (v0.36) markers were physically destroyed when broken.
                // Reconstruct that progression once when loading an existing
                // mission, rather than resetting it.
                if (trialCampaignActive && trialIndex >= 0 && ClassNumber(trialIndex) != 0)
                    for (int i = 0; i < trialMarkers.Count; i++)
                        if (trialMarkers[i] == null || trialMarkers[i].Destroyed)
                            if (!trialActivatedSarcophagi.Contains(i))
                                trialActivatedSarcophagi.Add(i);
                if (trialOpenedGraveIndices == null) trialOpenedGraveIndices = new List<int>();
                if (trialCampaignActive && trialIndex >= 0 &&
                    ClassNumber(trialIndex) == 0 && trialOpenedGraveIndices.Count == 0 &&
                    trialGravesOpened > 0)
                {
                    // Save migration: existing completed graves were 0,1,2
                    // because previous releases only permitted that order.
                    for (int i = 0; i < Math.Min(3, trialGravesOpened); i++)
                        trialOpenedGraveIndices.Add(i);
                }
            }
        }

        private void ResetCampaignTrial()
        {
            trialCampaignActive = false;
            trialGhosts.Clear();
            trialMarkers.Clear();
            if (trialActivatedSarcophagi == null)
                trialActivatedSarcophagi = new List<int>();
            trialActivatedSarcophagi.Clear();
            trialCurrentDuelGrave = -1;
            trialKingPhase = 0;
            trialKingPhaseTick = 0;
            trialSarcophagusGuideShown = false;
            trialCaptainsReadyShown = false;
            trialDuelChampion = null;
            trialDuelEnemy = null;
            trialFortCenter = IntVec3.Invalid;
            trialRingCenter = IntVec3.Invalid;
            trialGravesOpened = 0;
            if (trialOpenedGraveIndices == null) trialOpenedGraveIndices = new List<int>();
            trialOpenedGraveIndices.Clear();
            trialGraveGuidanceShown = false;
            trialGravesReadyShown = false;
            trialDuelVictories = 0;
            trialWavesSpawned = 0;
            trialNextWaveTick = 0;
            trialNextMagicTick = 0;
        }

        private static bool Cleared(IEnumerable<Pawn> pawns) =>
            pawns == null || pawns.All(p => p == null || !p.Spawned || p.Dead || p.Downed);

        private int RemainingMarkers => Enumerable.Range(0, trialMarkers.Count)
            .Count(i => !MarkerActivated(i));

        private bool MarkerActivated(int i) =>
            ClassNumber(trialIndex) == 0
                ? trialOpenedGraveIndices.Contains(i)
                : trialActivatedSarcophagi.Contains(i);

        private IEnumerable<(int index, Thing grave)> SealedSarcophagi()
        {
            for (int i = 0; i < trialMarkers.Count; i++)
            {
                Thing t = trialMarkers[i];
                if (!MarkerActivated(i) && t != null && t.Spawned && !t.Destroyed)
                    yield return (i, t);
            }
        }

        public bool IsQuestSarcophagus(Thing thing) =>
            trialCampaignActive && trialMap != null && thing != null &&
            thing.Map == trialMap && trialMarkers.Contains(thing);

        public string TrialSarcophagusInspect(Thing marker)
        {
            if (!IsQuestSarcophagus(marker)) return null;
            int index = trialMarkers.IndexOf(marker);
            return AragornText.T(MarkerActivated(index)
                ? "AR_OathGraves_Opened" : "AR_OathGraves_Sealed")
                + " · " + AragornText.T("AR_OathGraves_Indestructible");
        }

        private void MarkSarcophagusOpened(int index)
        {
            if (index < 0 || index >= trialMarkers.Count ||
                trialActivatedSarcophagi.Contains(index)) return;
            trialActivatedSarcophagi.Add(index);
            Thing thing = trialMarkers[index];
            if (thing != null && thing.Spawned)
            {
                FleckMaker.ThrowDustPuffThick(thing.DrawPos, trialMap,
                    2.2f, new Color(.16f, .9f, .75f));
            }
        }

        private IEnumerable<(int index, Thing grave)> ClosedGraves()
        {
            for (int i = 0; i < Math.Min(3, trialMarkers.Count); i++)
            {
                Thing marker = trialMarkers[i];
                if (!trialOpenedGraveIndices.Contains(i) && marker != null &&
                    marker.Spawned && !marker.Destroyed)
                    yield return (i, marker);
            }
        }

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

        // Gravestones remain the sword quest's objects. The third trial uses
        // Profaned's glowing darklight braziers as genuine ritual seals,
        // with safe fallbacks for older Profaned versions.
        private Thing AddTrialMarker(IntVec3 at, bool fixedPosition = false,
            string preferredDef = null)
        {
            ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksSlate");
            IntVec3 cell = fixedPosition && at.InBounds(trialMap) && at.Standable(trialMap)
                ? at : CellFinder.RandomClosewalkCellNear(at, trialMap, 2);
            // ONE visual language in every class: solid stone sarcophagi.
            // Their sealing state is persisted in trialActivatedSarcophagi.
            string[] candidates = new [] { "Sarcophagus" };
            foreach (string name in candidates.Distinct())
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                try
                {
                    Thing marker = ThingMaker.MakeThing(def,
                        def.MadeFromStuff ? stone : null);
                    GenSpawn.Spawn(marker, cell, trialMap, WipeMode.Vanish);
                    if (marker.Spawned)
                    {
                        trialMarkers.Add(marker);
                        return marker;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[Aragorn] Trial marker " + name +
                        " could not be spawned: " + ex.Message);
                }
            }
            Log.Error("[Aragorn] No usable marker for the ritual or trial.");
            return null;
        }

        private void AddDefenders(IntVec3 point, Faction hostile, int baseAmount, string role)
        {
            trialGuards.AddRange(ProfanedEncounterUtility.SpawnThemedGuards(
                trialMap, point, hostile, baseAmount, role));
        }

        private void GuardMission(Faction hostile, bool includeGhosts = true)
        {
            // Existing captains retain their original Lord; each later wave
            // gets its own Lord instead of being re-assigned to a new group.
            var defenders = (includeGhosts ? trialGuards.Concat(trialGhosts) : trialGuards)
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
                    case 0: // Approved 52x32 map: 7 positions x 2 skeletons, 4 elites.
                        // This is the *baseline*. Each Profaned group uses the
                        // existing global quest-danger slider through ScaleCount.
                        trialGuards.AddRange(AragornSwordCryptLayout.SpawnDefenders(map, hostile));
                        for (int i = 0; i < 3; i++)
                            AddTrialMarker(AragornSwordCryptLayout.Grave(map, i),
                                fixedPosition: true);
                        break;
                    case 1: // Three fortified positions with one oath tomb
                            // controlling each otherwise invulnerable archer.
                        for (int i = 0; i < 3; i++)
                        {
                            IntVec3 tower = trialFortCenter + new IntVec3((i - 1) * 13, 0, 9);
                            AddTrialMarker(tower + new IntVec3(0, 0, -5), fixedPosition: true);
                            Pawn archer = SpawnQuestGhost(3 + i, tower, hostile);
                            List<Pawn> bowmen = ProfanedEncounterUtility.SpawnThemedGuards(
                                trialMap, tower + new IntVec3(0, 0, -4), hostile, 9, "archers");
                            trialGuards.AddRange(bowmen);
                            if (archer != null) bowmen.Add(archer);
                            if (bowmen.Count > 0)
                                LordMaker.MakeNewLord(hostile,
                                    new LordJob_DefendPoint(tower, wanderRadius: 3f,
                                        defendRadius: 32f, isCaravanSendable: false, addFleeToil: false),
                                    trialMap, bowmen);
                        }
                        break;
                    case 2: // True ritual crypt: two guarded side chapels
                            // and a northern sanctum with both bombardiers.
                        Pawn westGhost = SpawnQuestGhost(6,
                            AragornBombardierCryptLayout.FirstGhost(map), hostile);
                        Pawn eastGhost = SpawnQuestGhost(7,
                            AragornBombardierCryptLayout.SecondGhost(map), hostile);
                        trialGuards.AddRange(AragornBombardierCryptLayout.SpawnMages(map, hostile));
                        AddTrialMarker(AragornBombardierCryptLayout.WestShrine(map),
                            fixedPosition: true);
                        AddTrialMarker(AragornBombardierCryptLayout.EastShrine(map),
                            fixedPosition: true);
                        AragornBombardierCryptLayout.AssignBombardierGuard(
                            map, hostile, westGhost, eastGhost);
                        break;
                    case 3: // Temple: two indestructible challenge graves
                            // activate distinct one-on-one axe fights.
                        AddDefenders(trialFortCenter + new IntVec3(-8, 0, -9), hostile, 8, "ghouls");
                        AddDefenders(trialFortCenter + new IntVec3(8, 0, -9), hostile, 8, "ghouls");
                        AddTrialMarker(trialRingCenter + new IntVec3(-3, 0, 0),
                            fixedPosition: true);
                        AddTrialMarker(trialRingCenter + new IntVec3(3, 0, 0),
                            fixedPosition: true);
                        break;
                    case 4: // Three enemy waves, then two captain graves
                            // unlock distinct commanders in the fortress.
                        SpawnQuestGhost(10, trialFortCenter + new IntVec3(-7, 0, 12), hostile);
                        SpawnQuestGhost(11, trialFortCenter + new IntVec3(7, 0, 12), hostile);
                        AddTrialMarker(trialFortCenter + new IntVec3(-7, 0, 7),
                            fixedPosition: true);
                        AddTrialMarker(trialFortCenter + new IntVec3(7, 0, 7),
                            fixedPosition: true);
                        AddDefenders(trialFortCenter + new IntVec3(0, 0, -13), hostile, 8, "elite");
                        trialWavesSpawned = 1;
                        break;
                    case 5: // One royal sarcophagus releases a three-wave
                            // throne ritual with escalating lightning.
                        SpawnQuestGhost(12, trialFortCenter + new IntVec3(0, 0, 13), hostile);
                        AddDefenders(trialFortCenter + new IntVec3(-9, 0, 5), hostile, 10, "elite");
                        AddDefenders(trialFortCenter + new IntVec3(9, 0, 5), hostile, 10, "elite");
                        AddTrialMarker(trialFortCenter + new IntVec3(0, 0, 2),
                            fixedPosition: true);
                        break;
                }
                // Every quest must have its intended targets; a partial map
                // must never grant free spirits or trap a save in an unwinnable site.
                int expectedGhosts = cls == 0 || cls == 3 ? 0 :
                    cls == 1 ? 3 : cls == 5 ? 1 : 2;
                int[] markerCounts = { 3, 3, 2, 2, 2, 1 };
                int expectedMarkers = markerCounts[cls];
                if (trialGhosts.Count != expectedGhosts ||
                    trialMarkers.Count != expectedMarkers || trialGuards.Count == 0)
                    throw new InvalidOperationException("Incomplete class trial map: class=" + cls +
                        ", ghosts=" + trialGhosts.Count + "/" + expectedGhosts +
                        ", markers=" + trialMarkers.Count + "/" + expectedMarkers +
                        ", defenders=" + trialGuards.Count);
                if (cls != 0 && cls != 1 && cls != 2) GuardMission(hostile);
                // Sword crypt, archer towers and ritual-crypt chambers have
                // separate defenders, so they must keep their own Lords.
                if (cls == 0 && !trialGraveGuidanceShown)
                {
                    Thing target = trialMarkers.FirstOrDefault(t => t != null && t.Spawned);
                    Find.LetterStack.ReceiveLetter(AragornText.T("AR_Class_GraveGuideTitle"),
                        AragornText.T("AR_Class_GraveGuideBody"),
                        LetterDefOf.NeutralEvent, target);
                    trialGraveGuidanceShown = true;
                }
                if (cls == 2)
                {
                    Thing firstSeal = trialMarkers.FirstOrDefault(t => t != null && t.Spawned);
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_Bombardier_GuideTitle"),
                        AragornText.T("AR_Bombardier_GuideBody"),
                        LetterDefOf.NeutralEvent, firstSeal);
                }
                if (cls != 0 && !trialSarcophagusGuideShown)
                {
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_OathGraves_GuideTitle"),
                        AragornText.F("AR_OathGraves_GuideBody",
                            expectedMarkers, cls + 1),
                        LetterDefOf.NeutralEvent, trialMarkers[0]);
                    trialSarcophagusGuideShown = true;
                }
                Messages.Message(AragornText.F("AR_Class_Encounter", cls + 1),
                    MessageTypeDefOf.ThreatBig);
                Log.Message("[Aragorn] Class trial " + (cls + 1) +
                    "/6: " + trialGhosts.Count + " ghosts, " + trialGuards.Count +
                    " Profaned defenders, " + trialMarkers.Count + " objectives.");
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Failed to populate six-class trial: " + ex);
                // Reset persistent references immediately. A partially generated
                // encounter cannot count as a victory or remain stuck forever.
                QueueTrialRetry();
            }
        }

        private bool LegacyTrial =>
            trialCampaignActive && trialMap != null && trialIndex >= 0 &&
            trialMarkers.Count != new [] { 3, 3, 2, 2, 2, 1 }[ClassNumber(trialIndex)];

        private bool TowerStillGuarded(Thing grave)
        {
            return grave != null && trialGuards.Any(p =>
                p != null && p.Spawned && !p.Dead && !p.Downed &&
                p.Position.DistanceTo(grave.Position) <= 10f);
        }

        public IEnumerable<Gizmo> GetClassTrialGizmos(Pawn pawn)
        {
            if (!trialCampaignActive || trialMap == null || pawn == null ||
                pawn.Faction != Faction.OfPlayer || !pawn.Spawned ||
                pawn.Map != trialMap || !pawn.RaceProps.Humanlike) yield break;

            int cls = ClassNumber(trialIndex);
            Texture2D art = ContentFinder<Texture2D>.Get("Aragorn/Gizmos/GhostTrial", false);
            if (cls == 0 && trialGravesOpened < 3)
            {
                var closest = ClosedGraves()
                    .OrderBy(x => x.grave.Position.DistanceTo(pawn.Position))
                    .FirstOrDefault();
                Thing grave = closest.grave;
                var cmd = new Command_Action {
                    defaultLabel = AragornText.F("AR_Class_OpenGraveProgress", trialGravesOpened),
                    defaultDesc = AragornText.T("AR_Class_OpenGraveDesc"),
                    icon = art,
                    action = () => OpenTrialGrave(pawn)
                };
                if (!Cleared(trialGuards))
                    cmd.Disable(AragornText.T("AR_Class_ClearCrypt"));
                else if (grave == null ||
                    pawn.Position.DistanceTo(grave.Position) > 3f)
                    cmd.Disable(AragornText.T("AR_Class_GetCloser"));
                yield return cmd;
            }
            else if (cls == 3 && trialDuelVictories < 2)
            {
                bool renew = trialDuelEnemy != null && trialDuelEnemy.Spawned &&
                    !trialDuelEnemy.Dead && !trialDuelEnemy.Downed &&
                    (trialDuelChampion == null || trialDuelChampion.Downed ||
                    trialDuelChampion.Dead || !trialDuelChampion.Spawned);
                var nearest = SealedSarcophagi().OrderBy(x =>
                    x.grave.Position.DistanceTo(pawn.Position)).FirstOrDefault();
                var cmd = new Command_Action {
                    defaultLabel = AragornText.T("AR_Class_StartDuel"),
                    defaultDesc = AragornText.T("AR_OathGraves_DuelDescription"),
                    icon = art,
                    action = () => BeginAxeDuel(pawn)
                };
                if (!Cleared(trialGuards))
                    cmd.Disable(AragornText.T("AR_Class_ClearTemple"));
                else if (pawn.Position.DistanceTo(trialRingCenter) > 5f ||
                    (!renew && (nearest.grave == null ||
                    pawn.Position.DistanceTo(nearest.grave.Position) > 4f)))
                    cmd.Disable(AragornText.T("AR_OathGraves_DuelApproach"));
                else if (trialDuelEnemy != null && (trialDuelEnemy.Dead ||
                    trialDuelEnemy.Downed))
                    cmd.Disable(AragornText.T("AR_Class_DuelFinishing"));
                else if (trialDuelEnemy != null && !renew)
                    cmd.Disable(AragornText.T("AR_Class_DuelInProgress"));
                else if (trialMap.mapPawns.AllPawnsSpawned.Any(other =>
                    other != pawn && other.Faction == Faction.OfPlayer &&
                    other.Position.DistanceTo(trialRingCenter) < 7f))
                    cmd.Disable(AragornText.T("AR_Class_DuelAlone"));
                yield return cmd;
            }
            else if ((cls == 1 || cls == 2 || cls == 4 || cls == 5) &&
                RemainingMarkers > 0)
            {
                var nearest = SealedSarcophagi().OrderBy(x =>
                    x.grave.Position.DistanceTo(pawn.Position)).FirstOrDefault();
                Thing grave = nearest.grave;
                var cmd = new Command_Action {
                    defaultLabel = AragornText.F("AR_OathGraves_OpenProgress",
                        trialMarkers.Count - RemainingMarkers, trialMarkers.Count),
                    defaultDesc = cls == 2
                        ? AragornText.T("AR_Bombardier_SealGizmoDesc")
                        : AragornText.T("AR_OathGraves_OpenDescription"),
                    icon = art,
                    action = () => BreakTrialSeal(pawn)
                };
                if (cls == 1 && !LegacyTrial && TowerStillGuarded(grave))
                    cmd.Disable(AragornText.T("AR_OathGraves_ArchersRemain"));
                else if (cls == 4 && !LegacyTrial &&
                    (trialWavesSpawned < 3 || !Cleared(trialGuards)))
                    cmd.Disable(AragornText.T("AR_OathGraves_CaptainsWait"));
                else if (cls == 5 && !LegacyTrial && !Cleared(trialGuards))
                    cmd.Disable(AragornText.T("AR_OathGraves_RoyalGuards"));
                else if (grave == null ||
                    pawn.Position.DistanceTo(grave.Position) > 3f)
                    cmd.Disable(AragornText.T("AR_Class_GetCloser"));
                yield return cmd;
            }
        }

        private void SpawnOpenedGraveGhost(int index, IntVec3 spot)
        {
            if (trialOpenedGraveIndices.Contains(index)) return;
            var hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null) { QueueTrialRetry(); return; }
            Pawn ghost = SpawnQuestGhost(index, spot, hostile);
            if (ghost == null) { QueueTrialRetry(); return; }
            trialOpenedGraveIndices.Add(index);
            trialGravesOpened = trialOpenedGraveIndices.Count;
            LordMaker.MakeNewLord(ghost.Faction,
                new LordJob_DefendPoint(spot, wanderRadius: 5f,
                    defendRadius: 14f, isCaravanSendable: false, addFleeToil: false),
                trialMap, new List<Pawn> { ghost });
        }

        private void OpenTrialGrave(Pawn pawn)
        {
            if (trialMap == null || pawn == null || !pawn.Spawned ||
                pawn.Map != trialMap || !Cleared(trialGuards) ||
                trialGravesOpened >= 3) return;
            var candidate = ClosedGraves()
                .OrderBy(x => x.grave.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();
            if (candidate.grave == null ||
                pawn.Position.DistanceTo(candidate.grave.Position) > 3f) return;
            IntVec3 point = candidate.grave.Position;
            // The sarcophagus NEVER vanishes: the saved ritual status
            // is what changes. Its ghost emerges beside the stone coffin.
            SpawnOpenedGraveGhost(candidate.index, point);
            if (trialOpenedGraveIndices.Contains(candidate.index))
            {
                FleckMaker.ThrowDustPuffThick(candidate.grave.DrawPos, trialMap,
                    2.4f, new Color(.15f, .9f, .75f));
                Messages.Message(AragornText.F("AR_Class_GraveAwakes", trialGravesOpened),
                    pawn, MessageTypeDefOf.ThreatBig);
            }
        }

        private void BreakTrialSeal(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map != trialMap ||
                pawn.Faction != Faction.OfPlayer) return;
            var target = SealedSarcophagi().OrderBy(x =>
                x.grave.Position.DistanceTo(pawn.Position)).FirstOrDefault();
            Thing grave = target.grave;
            if (grave == null || pawn.Position.DistanceTo(grave.Position) > 3f) return;
            int cls = ClassNumber(trialIndex);
            if (cls == 1 && !LegacyTrial && TowerStillGuarded(grave)) return;
            if (cls == 4 && !LegacyTrial &&
                (trialWavesSpawned < 3 || !Cleared(trialGuards))) return;
            if (cls == 5 && !LegacyTrial && !Cleared(trialGuards)) return;

            // Unlike v0.36, ritual markers are never physically removed.
            MarkSarcophagusOpened(target.index);

            if (cls == 2)
            {
                if (RemainingMarkers == 0)
                {
                    trialNextMagicTick = int.MaxValue;
                    Pawn spirit = trialGhosts.FirstOrDefault(p => p != null && p.Spawned);
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_Bombardier_SealsBrokenTitle"),
                        AragornText.T("AR_Bombardier_SealsBrokenBody"),
                        LetterDefOf.PositiveEvent, spirit);
                }
                else
                {
                    trialNextMagicTick = Ticks + 780;
                    Thing remaining = SealedSarcophagi().Select(x => x.grave)
                        .FirstOrDefault();
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_Bombardier_OneSealTitle"),
                        AragornText.T("AR_Bombardier_OneSealBody"),
                        LetterDefOf.NeutralEvent, remaining);
                }
            }
            else if (cls == 5)
            {
                if (!LegacyTrial)
                {
                    trialKingPhase = 1;
                    trialKingPhaseTick = Ticks + 300;
                    trialNextMagicTick = Ticks + 220;
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_OathGraves_KingAwakesTitle"),
                        AragornText.T("AR_OathGraves_KingAwakesBody"),
                        LetterDefOf.ThreatBig, trialBoss);
                }
                else if (RemainingMarkers == 0)
                    Messages.Message(AragornText.T("AR_Class_KingShieldBroken"),
                        pawn, MessageTypeDefOf.PositiveEvent);
            }
            else if (cls == 1)
            {
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_OathGraves_ArcherFreedTitle"),
                    AragornText.F("AR_OathGraves_ArcherFreedBody", target.index + 1),
                    LetterDefOf.ThreatBig, trialGhosts.Count > target.index
                        ? trialGhosts[target.index] : pawn);
            }
            else if (cls == 4)
            {
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_OathGraves_CaptainAwakesTitle"),
                    AragornText.F("AR_OathGraves_CaptainAwakesBody", target.index + 1),
                    LetterDefOf.ThreatBig, trialGhosts.Count > target.index
                        ? trialGhosts[target.index] : pawn);
            }
        }

        private void BeginAxeDuel(Pawn pawn)
        {
            if (trialMap == null || trialDuelVictories >= 2 || !Cleared(trialGuards) ||
                pawn == null || pawn.Downed || !pawn.Spawned ||
                pawn.Map != trialMap ||
                pawn.Position.DistanceTo(trialRingCenter) > 5f) return;
            if (trialMap.mapPawns.AllPawnsSpawned.Any(other =>
                other != pawn && other.Faction == Faction.OfPlayer &&
                other.Position.DistanceTo(trialRingCenter) < 7f)) return;
            if (trialDuelEnemy != null && (trialDuelEnemy.Dead || trialDuelEnemy.Downed))
                return; // result is still being recorded

            // If the previous challenger was downed, another colonist may
            // take their place, but the same spirit continues fighting.
            if (trialDuelEnemy != null && trialDuelEnemy.Spawned)
            {
                if (trialDuelChampion != null && trialDuelChampion.Spawned &&
                    !trialDuelChampion.Dead && !trialDuelChampion.Downed) return;
                trialDuelChampion = pawn;
                Messages.Message(AragornText.T("AR_Class_DuelNeedChampion"),
                    pawn, MessageTypeDefOf.ThreatBig);
                return;
            }

            int chosenGrave = -1;
            IntVec3 spawnAt = trialRingCenter + new IntVec3(0, 0, 3);
            int ghostIndex = 8 + trialDuelVictories;
            if (!LegacyTrial)
            {
                var nearest = SealedSarcophagi().OrderBy(x =>
                    x.grave.Position.DistanceTo(pawn.Position)).FirstOrDefault();
                if (nearest.grave == null ||
                    pawn.Position.DistanceTo(nearest.grave.Position) > 4f) return;
                chosenGrave = nearest.index;
                spawnAt = nearest.grave.Position;
                ghostIndex = 8 + chosenGrave;
            }

            Faction hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null) return;
            Pawn ghost = SpawnQuestGhost(ghostIndex, spawnAt, hostile);
            if (ghost == null) return;
            trialDuelChampion = pawn;
            trialDuelEnemy = ghost;
            trialCurrentDuelGrave = chosenGrave;
            if (chosenGrave >= 0) MarkSarcophagusOpened(chosenGrave);
            LordMaker.MakeNewLord(hostile,
                new LordJob_DefendPoint(trialRingCenter,
                    wanderRadius: 3f, defendRadius: 8f,
                    isCaravanSendable: false, addFleeToil: false),
                trialMap, new List<Pawn> { ghost });
            Messages.Message(AragornText.F("AR_Class_DuelRound", trialDuelVictories + 1),
                pawn, MessageTypeDefOf.ThreatBig);
        }

        // Enforced 1v1: spectators and other attackers cannot hurt duelists
        // and the King is protected until all three seals are broken.
        public bool AllowTrialDamage(Thing target, Thing attacker)
        {
            if (!trialCampaignActive || trialMap == null || target == null ||
                target.Map != trialMap) return true;
            if (IsQuestSarcophagus(target)) return false;

            int cls = ClassNumber(trialIndex);
            Pawn spirit = target as Pawn;
            if (cls == 1 && !LegacyTrial && spirit != null)
            {
                int i = trialGhosts.IndexOf(spirit);
                if (i >= 0 && !MarkerActivated(i)) return false;
            }
            if (cls == 2 && RemainingMarkers > 0 &&
                spirit != null && trialGhosts.Contains(spirit)) return false;
            if (cls == 4 && !LegacyTrial && spirit != null)
            {
                int i = trialGhosts.IndexOf(spirit);
                if (i >= 0 && !MarkerActivated(i)) return false;
            }
            if (cls == 5 && spirit != null &&
                trialGhosts.Count > 0 && spirit == trialGhosts[0] &&
                (RemainingMarkers > 0 ||
                    (!LegacyTrial && trialKingPhase < 5))) return false;

            if (cls != 3 || trialDuelEnemy == null ||
                !trialDuelEnemy.Spawned || trialDuelEnemy.Dead ||
                trialDuelEnemy.Downed) return true;

            // Sacred 1v1: only the current champion and duel spirit
            // may hurt each other. Spectators cannot kill either one.
            bool championReady = trialDuelChampion != null &&
                trialDuelChampion.Spawned && !trialDuelChampion.Dead &&
                !trialDuelChampion.Downed;
            if (target == trialDuelEnemy)
                return championReady && attacker == trialDuelChampion;
            if (target == trialDuelChampion && championReady)
                return attacker == trialDuelEnemy;
            if (attacker == trialDuelEnemy)
                return championReady && target == trialDuelChampion;
            return true;
        }

        private void TickClassTrial()
        {
            if (!trialCampaignActive || trialIndex < 0 || trialMap == null) return;
            if (trialSite == null || !trialSite.HasMap || trialSite.Map != trialMap)
            {
                QueueTrialRetry(); // site disappeared while the trial was in progress
                return;
            }
            int cls = ClassNumber(trialIndex);
            if (cls == 0)
            {
                // Tell players exactly how to interact as soon as the
                // crypt guards are defeated; clicking the letter locates
                // a still-sealed tomb. Any tomb may be opened first.
                if (Cleared(trialGuards) && !trialGravesReadyShown &&
                    trialGravesOpened < 3)
                {
                    Thing target = ClosedGraves().Select(x => x.grave).FirstOrDefault();
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_Class_GravesReadyTitle"),
                        AragornText.T("AR_Class_GravesReadyBody"),
                        LetterDefOf.PositiveEvent, target);
                    trialGravesReadyShown = true;
                }
                // Grave destruction during combat must not soft-lock the
                // quest. Handle EACH tomb independently rather than the
                // formerly assumed next index (0 -> 1 -> 2).
                for (int i = 0; i < Math.Min(3, trialMarkers.Count); i++)
                {
                    if (trialOpenedGraveIndices.Contains(i)) continue;
                    Thing marker = trialMarkers[i];
                    if (marker == null || marker.Destroyed || !marker.Spawned)
                    {
                        IntVec3 point = AragornSwordCryptLayout.Grave(trialMap, i);
                        SpawnOpenedGraveGhost(i, point);
                    }
                }
            }
            if (cls == 3 && trialDuelEnemy != null)
            {
                if (trialDuelEnemy.Dead || trialDuelEnemy.Downed)
                {
                    trialDuelVictories++;
                    trialDuelEnemy = null;
                    trialDuelChampion = null;
                    trialCurrentDuelGrave = -1;
                    Messages.Message(AragornText.F("AR_Class_DuelWon", trialDuelVictories),
                        MessageTypeDefOf.PositiveEvent);
                }
                else if (!trialDuelEnemy.Spawned)
                {
                    // A despawned but living opponent is not a victory.
                    trialGhosts.Remove(trialDuelEnemy);
                    if (trialCurrentDuelGrave >= 0)
                        trialActivatedSarcophagi.Remove(trialCurrentDuelGrave);
                    trialDuelEnemy = null;
                    trialDuelChampion = null;
                    trialCurrentDuelGrave = -1;
                }
                else if (trialDuelChampion == null || trialDuelChampion.Dead ||
                    trialDuelChampion.Downed || !trialDuelChampion.Spawned)
                {
                    if (trialDuelChampion != null)
                        Messages.Message(AragornText.T("AR_Class_DuelNeedChampion"),
                            MessageTypeDefOf.RejectInput);
                    trialDuelChampion = null; // one warning only; new challenger can enter
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
                    GuardMission(hostile, includeGhosts: false);
                    Messages.Message(AragornText.F("AR_Class_WaveArrives", trialWavesSpawned),
                        MessageTypeDefOf.ThreatBig);
                }
            }
            if (cls == 4 && !LegacyTrial && trialWavesSpawned == 3 &&
                Cleared(trialGuards) && RemainingMarkers > 0 &&
                !trialCaptainsReadyShown)
            {
                trialCaptainsReadyShown = true;
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_OathGraves_CaptainsReadyTitle"),
                    AragornText.T("AR_OathGraves_CaptainsReadyBody"),
                    LetterDefOf.PositiveEvent,
                    SealedSarcophagi().Select(x => x.grave).FirstOrDefault());
            }
            // The one royal tomb releases three separate elite waves.
            // Only AFTER all three waves may the King be damaged.
            if (cls == 5 && !LegacyTrial && trialKingPhase >= 1 &&
                trialKingPhase < 5 && Ticks >= trialKingPhaseTick &&
                Cleared(trialGuards))
            {
                trialKingPhase++;
                trialKingPhaseTick = Ticks + 300;
                if (trialKingPhase <= 4)
                {
                    trialGuards.Clear();
                    Faction kingHostile = ProfanedEncounterUtility.GetOrCreateFaction();
                    if (kingHostile == null) { QueueTrialRetry(); return; }
                    IntVec3 gate = trialFortCenter + new IntVec3(
                        trialKingPhase % 2 == 0 ? -12 : 12, 0, -9);
                    AddDefenders(gate, kingHostile,
                        5 + trialKingPhase * 3, "elite");
                    if (trialGuards.Count == 0)
                    {
                        QueueTrialRetry();
                        return;
                    }
                    GuardMission(kingHostile, includeGhosts: false);
                    Messages.Message(AragornText.F(
                        "AR_OathGraves_KingWave", trialKingPhase - 1),
                        MessageTypeDefOf.ThreatBig);
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(
                        AragornText.T("AR_OathGraves_KingVulnerableTitle"),
                        AragornText.T("AR_OathGraves_KingVulnerableBody"),
                        LetterDefOf.PositiveEvent, trialBoss);
                }
            }

            if ((cls == 2 || cls == 5) && Ticks >= trialNextMagicTick &&
                ((cls == 5 && (LegacyTrial || trialKingPhase > 0)) ||
                    (cls == 2 && RemainingMarkers > 0)))
            {
                trialNextMagicTick = Ticks +
                    (cls == 5
                        ? (LegacyTrial ? 330 :
                            trialKingPhase >= 5 ? 180 :
                            trialKingPhase >= 3 ? 230 : 350)
                        : RemainingMarkers >= 2 ? 480 : 780);
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
                    complete = trialDuelVictories >= 2 && Cleared(trialGuards)
                        && (LegacyTrial || RemainingMarkers == 0);
                    break;
                case 4:
                    complete = trialWavesSpawned >= 3 && trialGhosts.Count == 2 &&
                        Cleared(trialGhosts) && Cleared(trialGuards) &&
                        (LegacyTrial || RemainingMarkers == 0);
                    break;
                case 5:
                    complete = trialGhosts.Count == 1 && Cleared(trialGhosts) &&
                        Cleared(trialGuards) && RemainingMarkers == 0 &&
                        (LegacyTrial || trialKingPhase >= 5);
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

    // A genuine quest sarcophagus is neither an ordinary lootable grave
    // nor a ritual prop: no explosions, fire, melee or direct vanish may
    // delete it while its six-class mission is active.
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    internal static class AragornOathSarcophagusNoDestroy
    {
        private static bool Prefix(Thing __instance) =>
            Current.Game?.GetComponent<AragornGame>()?.IsQuestSarcophagus(__instance) != true;
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DeconstructibleBy))]
    internal static class AragornOathSarcophagusNoDeconstruction
    {
        private static void Postfix(Building __instance, ref bool __result)
        {
            if (Current.Game?.GetComponent<AragornGame>()?.IsQuestSarcophagus(__instance) == true)
                __result = false;
        }
    }

    // Vanilla-compatible 2D fortifications; no extra multi-floor mod.
    internal static class AragornTrialStructures
    {
        private static void FloorAndClear(Map map, IntVec3 p, bool stoneRoof)
        {
            if (!p.InBounds(map)) return;
            foreach (Thing thing in p.GetThingList(map).ToList())
                if (thing.def.category == ThingCategory.Plant)
                    thing.Destroy(DestroyMode.Vanish);
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("ProfanedTileSlate")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneSlate");
            if (floor != null && p.Standable(map))
                try { map.terrainGrid.SetTerrain(p, floor); } catch { }
            // Thick rock roof cannot be safely created over an open plain.
            // Use supported constructed roofing for the mountain-themed crypt.
            if (stoneRoof && p.Walkable(map))
                try { map.roofGrid.SetRoof(p, RoofDefOf.RoofConstructed); } catch { }
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
            if (roof)
            {
                // Pillars every ~8 cells support the large covered crypt,
                // while keeping the central x=0 corridor clear.
                for (int x = -16; x <= 16; x += 8)
                for (int z = -16; z <= 16; z += 8)
                {
                    if (x == 0)
                    {
                        Place(map, center + new IntVec3(-2, 0, z), "Wall");
                        Place(map, center + new IntVec3(2, 0, z), "Wall");
                    }
                    else
                        Place(map, center + new IntVec3(x, 0, z), "Wall");
                }
            }
        }

        public static IntVec3 Build(Map map, int cls)
        {
            // The sword quest uses the explicitly approved 52x32 blueprint;
            // all other class maps retain their existing layouts.
            if (cls == 0) return AragornSwordCryptLayout.Build(map);
            if (cls == 2) return AragornBombardierCryptLayout.Build(map);
            IntVec3 c = CellFinder.RandomClosewalkCellNear(map.Center, map, 18);
            OuterFort(map, c, 24, 19, false);
            if (cls == 1)
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
                // Two separately guarded ghoul chapels precede the inner arena.
                // Keep 3-wide access through the central courtyard.
                foreach (int side in new[] { -1, 1 })
                {
                    for (int x = 5; x <= 20; x++)
                    {
                        if (x != 10)
                            Place(map, c + new IntVec3(side * x, 0, -4), "Wall");
                        else
                            Place(map, c + new IntVec3(side * x, 0, -4), "Door");
                    }
                    for (int z = -14; z <= -5; z += 5)
                        Place(map, c + new IntVec3(side * 16, 0, z), "Wall");
                }
                // The arena is a separate chamber, with two distinct
                // oath-coffins. Nobody can win the two duels from outside.
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
            else if (cls == 4)
            {
                // Captain fortress: three successive defense lines before
                // the twin tombs. Two wide flank passages remain open at
                // each level, to permit actual tactical approaches.
                foreach (int z in new[] { -5, 3, 10 })
                    for (int x = -21; x <= 21; x++)
                    {
                        if (Math.Abs(x) <= 3 || Math.Abs(x) >= 19) continue;
                        if (Math.Abs(x) == 12)
                            Place(map, c + new IntVec3(x, 0, z), "Door");
                        else
                            Place(map, c + new IntVec3(x, 0, z), "Wall");
                    }
                foreach (int x in new[] { -18, -9, 9, 18 })
                    for (int z = -13; z <= 7; z += 7)
                        Place(map, c + new IntVec3(x, 0, z), "Barricade");
            }
            else if (cls == 5)
            {
                // The royal sarcophagus is placed by PopulateClassTrial,
                // not as a second decorative coffin.
                // Two aisle lines and pillars lead to the throne, but the
                // central route remains open for players and enemy AI.
                for (int z = -8; z <= 13; z += 7)
                    foreach (int x in new[] { -16, -12, 12, 16 })
                        Place(map, c + new IntVec3(x, 0, z), "Wall");
                for (int x = -17; x <= 17; x++)
                    if (Math.Abs(x) > 4)
                        Place(map, c + new IntVec3(x, 0, 8), "Barricade");
            }
            return c;
        }
    }
}
