using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AragornArmy
{
    // Persistent once-per-save late-story Mechanoid quest.
    public sealed class AragornMechQuestGame : GameComponent
    {
        private Quest quest;
        private Site site;
        private List<Pawn> defenders = new List<Pawn>();
        private bool completed;
        private bool mapPopulated;
        private int lastPulseTick = -1000000;
        private int savedCount;
        private float frozenThreatPoints;
        private int abandonedSinceTick = -1;

        public AragornMechQuestGame(Game game) { }
        public bool Completed => completed;
        public int CooldownRemaining => Math.Max(0, lastPulseTick + 3600 - Find.TickManager.TicksGame);

        public override void ExposeData()
        {
            Scribe_References.Look(ref quest, "arMechQuest");
            Scribe_References.Look(ref site, "arMechSite");
            Scribe_Collections.Look(ref defenders, "arMechDefenders", LookMode.Reference);
            Scribe_Values.Look(ref completed, "arMechCompleted", false);
            Scribe_Values.Look(ref mapPopulated, "arMechMapPopulated", false);
            Scribe_Values.Look(ref lastPulseTick, "arMechLastPulse", -1000000);
            Scribe_Values.Look(ref savedCount, "arMechEnemyCount", 0);
            Scribe_Values.Look(ref frozenThreatPoints, "arMechThreatPoints", 0f);
            Scribe_Values.Look(ref abandonedSinceTick, "arMechAbandonedSince", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && defenders == null)
                defenders = new List<Pawn>();
        }

        public override void GameComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing || completed ||
                Find.QuestManager == null || Find.TickManager == null) return;
            if (Current.Game?.GetComponent<AragornGame>()?.Unlocked < 13) return;
            if (quest != null && quest.Historical)
            {
                ResetFailedQuest();
                return;
            }
            if (quest == null)
            {
                quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                    q != null && !q.Historical && q.root != null &&
                    q.root.defName == "AR_MechSilenceQuest");
                if (quest == null) Offer();
            }
            if (quest != null && quest.State == QuestState.Ongoing && site == null)
                CreateSite();
        }

        public override void GameComponentTick()
        {
            if (completed || !mapPopulated ||
                Find.TickManager.TicksGame % 60 != 0) return;
            int now = Find.TickManager.TicksGame;
            bool absent = site == null || !site.HasMap ||
                !AragornThreat.HasPlayerPawns(site.Map);
            if (absent)
            {
                if (abandonedSinceTick < 0) abandonedSinceTick = now;
                else if (now - abandonedSinceTick >= 1200)
                    ResetFailedQuest();
                return;
            }
            abandonedSinceTick = -1;
            if (defenders.Any(p => p != null && p.Spawned && !p.Dead && !p.Downed))
                return;
            // The mission is a battle, not an exploit to auto-complete without
            // visiting the site or killing the actual defenders.
            if (savedCount <= 0) return;
            completed = true;
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Success, sendLetter: true);
            Find.LetterStack.ReceiveLetter(
                AragornText.T("AR_Mech_WinTitle"),
                AragornText.T("AR_Mech_WinBody"),
                LetterDefOf.PositiveEvent, site);
            Log.Message("[Aragorn] Mechanoid quest completed; Machine Silence unlocked.");
        }

        private void ResetFailedQuest()
        {
            if (completed) return;
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Fail, sendLetter: false);
            quest = null;
            site = null;
            defenders.Clear();
            mapPopulated = false;
            savedCount = 0;
            frozenThreatPoints = 0f;
            abandonedSinceTick = -1;
            Messages.Message("Mechanoidenfestung abgebrochen: Die Quest wird erneut angeboten.",
                MessageTypeDefOf.NeutralEvent);
            Log.Message("[Aragorn] Mech quest site abandoned or cancelled; reoffering.");
        }

        private void Offer()
        {
            QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_MechSilenceQuest");
            if (root == null) { Log.ErrorOnce("[Aragorn] Missing mech quest def.", 988208); return; }
            quest = Quest.MakeRaw();
            quest.root = root;
            quest.name = AragornText.T("AR_Mech_QuestTitle");
            quest.description = AragornText.T("AR_Mech_QuestDesc");
            quest.acceptanceExpireTick = -1;
            quest.SetNotYetAccepted();
            quest.AddPart(new QuestPart_AragornRescueTarget());
            Find.QuestManager.Add(quest);
            QuestUtility.SendLetterQuestAvailable(quest);
        }

        private void CreateSite()
        {
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail("AR_MechSilenceFortress");
            Faction faction = Faction.OfMechanoids;
            if (part == null || faction == null || faction.deactivated) return;
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, minDist: 5, maxDist: 22)
                && !TileFinder.TryFindNewSiteTile(out tile, minDist: 2, maxDist: 35)) return;
            try
            {
                frozenThreatPoints = AragornThreat.Points(2.5f);
                site = SiteMaker.MakeSite(part, tile, faction,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: frozenThreatPoints);
                if (site == null) return;
                site.customLabel = AragornText.T("AR_Mech_Site");
                Find.WorldObjects.Add(site);
                QuestPart_AragornRescueTarget link =
                    quest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (link != null) link.site = site;
                Find.LetterStack.ReceiveLetter(AragornText.T("AR_Mech_ArrivalTitle"),
                    AragornText.T("AR_Mech_ArrivalBody"),
                    LetterDefOf.NeutralEvent, site);
            }
            catch (Exception e) { Log.Error("[Aragorn] Mech fortress creation: " + e); }
        }

        public void PopulateMap(Map map)
        {
            if (map == null || site == null || map.Parent != site || mapPopulated) return;
            Faction faction = Faction.OfMechanoids;
            if (faction == null || faction.deactivated) return;
            try
            {
                // Ruined fortified compound with one central stronghold.
                IntVec3 center = AragornMechFortBuilder.Build(map, map.Center, faction);
                var parms = new PawnGroupMakerParms {
                    faction = faction,
                    groupKind = PawnGroupKindDefOf.Combat,
                    points = frozenThreatPoints > 0 ? frozenThreatPoints : AragornThreat.Points(2.5f)
                };
                int existingHostiles = AragornThreat.CountAliveQuestHostiles(map, faction);
                int available = Math.Max(0, AragornThreat.MaxEnemies - existingHostiles);
                var group = PawnGroupMakerUtility.GeneratePawns(parms, warnOnZeroResults: false)
                    .Take(available).ToList();
                foreach (Pawn mech in group)
                {
                    try
                    {
                        IntVec3 spot = CellFinder.RandomClosewalkCellNear(center, map, 18);
                        GenSpawn.Spawn(mech, spot, map);
                        defenders.Add(mech);
                    }
                    catch (Exception ex) { Log.Warning("[Aragorn] Mech spawn: " + ex.Message); }
                }
                if (defenders.Count == 0)
                {
                    // A modded disabled faction/group maker is not a quest victory.
                    Log.Error("[Aragorn] Mech fortress spawned no combat pawns; quest stays open.");
                    return;
                }
                savedCount = defenders.Count;
                mapPopulated = true;
                LordMaker.MakeNewLord(faction,
                    new LordJob_DefendPoint(center, wanderRadius: 12f,
                        defendRadius: 35f, isCaravanSendable: false, addFleeToil: false),
                    map, defenders);
                Messages.Message(AragornText.F("AR_Mech_Encounter", defenders.Count),
                    defenders[0], MessageTypeDefOf.ThreatBig);
            }
            catch (Exception ex) { Log.Error("[Aragorn] Mech fortress map failed: " + ex); }
        }

        public void DevUnlock(Pawn hero)
        {
            if (!Prefs.DevMode || (completed && quest != null && quest.Historical)) return;
            // Ensure the last story quest appears as truly completed in the
            // quest history, even when Dev completes everything before the
            // post-13 quest has ever been offered.
            if (quest == null)
            {
                quest = Find.QuestManager?.QuestsListForReading.FirstOrDefault(q =>
                    q != null && !q.Historical && q.root != null &&
                    q.root.defName == "AR_MechSilenceQuest");
                if (quest == null && Find.QuestManager != null) Offer();
            }
            if (quest == null)
            {
                Log.Error("[Aragorn] Dev unlock: mech quest could not be created.");
                return;
            }
            completed = true;
            lastPulseTick = -1000000;
            if (!quest.Historical)
            {
                if (quest.State == QuestState.NotYetAccepted) quest.Accept(hero);
                quest.End(QuestEndOutcome.Success, sendLetter: false);
            }
        }

        public void DevResetCooldown()
        {
            if (Prefs.DevMode) lastPulseTick = -1000000;
        }

        public void TargetPulse(Pawn aragorn)
        {
            if (!completed || !GhostDefinitions.IsAragorn(aragorn) ||
                aragorn.Map == null || !aragorn.Spawned || aragorn.Downed) return;
            if (CooldownRemaining > 0)
            {
                AragornGame.Reject("EMP-Sturm: Abklingzeit "
                    + Mathf.CeilToInt(CooldownRemaining / 60f) + " Sekunden.");
                return;
            }
            var parms = new TargetingParameters {
                canTargetLocations = true, canTargetPawns = true,
                canTargetBuildings = false,
                validator = target => target.IsValid && aragorn.Spawned &&
                    target.Map == aragorn.Map &&
                    target.Cell.DistanceTo(aragorn.Position) <= 30f
            };
            Find.Targeter.BeginTargeting(parms, target =>
            {
                if (!target.IsValid || !aragorn.Spawned || aragorn.Map == null ||
                    !target.Cell.InBounds(aragorn.Map) ||
                    target.Cell.DistanceTo(aragorn.Position) > 30f) return;
                lastPulseTick = Find.TickManager.TicksGame;
                int stunned = 0;
                foreach (Pawn p in aragorn.Map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (p == null || !p.Spawned || p.Dead || !p.RaceProps.IsMechanoid
                        || !p.HostileTo(aragorn) ||
                        p.Position.DistanceTo(target.Cell) > 6f) continue;
                    if (p.stances?.stunner == null) continue;
                    // EMP stuns the machine and inflicts 100 real injury damage.
                    // Burn is used for the electrical injury; vanilla EMP
                    // itself primarily stuns and is not reliable HP damage.
                    p.stances.stunner.StunFor(600, aragorn);
                    DamageInfo electricHit = new DamageInfo(DamageDefOf.Burn, 100f, 0f, -1f, aragorn);
                    electricHit.SetIgnoreArmor(true);
                    p.TakeDamage(electricHit);
                    if (p.Spawned && !p.Dead)
                        FleckMaker.ThrowLightningGlow(p.DrawPos, p.Map, 2.5f);
                    stunned++;
                }
                FleckMaker.ThrowLightningGlow(target.Cell.ToVector3Shifted(), aragorn.Map, 2.5f);
                Messages.Message(AragornText.F("AR_Mech_StunReport", stunned),
                    aragorn, MessageTypeDefOf.PositiveEvent);
            }, target =>
            {
                if (!aragorn.Spawned) return;
                GenDraw.DrawRadiusRing(aragorn.Position, 30f);
                if (target.IsValid && target.Cell.DistanceTo(aragorn.Position) <= 30f)
                    GenDraw.DrawRadiusRing(target.Cell, 6f);
            }, target => target.IsValid && aragorn.Spawned &&
                target.Cell.InBounds(aragorn.Map) &&
                target.Cell.DistanceTo(aragorn.Position) <= 30f, aragorn);
        }
    }


    // Build an actual steel-and-concrete fortress, not a recycled Profaned crypt.
    public static class AragornMechFortBuilder
    {
        public static IntVec3 Build(Map map, IntVec3 near, Faction owner)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef steel = ThingDefOf.Steel;
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete");
            IntVec3 center = CellFinder.RandomClosewalkCellNear(near, map, 18);
            if (wall == null || door == null || steel == null) return center;
            // Large reinforced rectangular keep, four entrances, hard cover.
            const int rx = 13, rz = 11;
            for (int x = -rx; x <= rx; x++)
            for (int z = -rz; z <= rz; z++)
            {
                IntVec3 c = center + new IntVec3(x, 0, z);
                if (!c.InBounds(map)) continue;
                try
                {
                    foreach (Thing t in c.GetThingList(map).ToList())
                        if (t.def.category == ThingCategory.Plant)
                            t.Destroy(DestroyMode.Vanish);
                    if (floor != null && c.Walkable(map))
                        map.terrainGrid.SetTerrain(c, floor);
                    bool perimeter = Math.Abs(x) == rx || Math.Abs(z) == rz;
                    bool interiorBarricade = (Math.Abs(x) == 6 && (Math.Abs(z) == 4 || Math.Abs(z) == 5))
                        || (Math.Abs(z) == 6 && (Math.Abs(x) == 4 || Math.Abs(x) == 5));
                    if (!perimeter && !interiorBarricade) continue;
                    bool gate = perimeter && ((x == 0 && Math.Abs(z) == rz)
                        || (z == 0 && Math.Abs(x) == rx));
                    if (c.GetThingList(map).Any(t => t is Building)) continue;
                    Thing structure = ThingMaker.MakeThing(gate ? door : wall, steel);
                    GenSpawn.Spawn(structure, c, map, WipeMode.Vanish);
                    if (owner != null && structure.def.CanHaveFaction)
                        structure.SetFaction(owner);
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("[Aragorn] Mech fortress block: " + ex.Message, 317786);
                }
            }
            Log.Message("[Aragorn] Steel mechanoid stronghold generated at " + center);
            return center;
        }
    }

    public sealed class SitePartWorker_AragornMechSilence : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<AragornMechQuestGame>()?.PopulateMap(map);
        }
    }
}
