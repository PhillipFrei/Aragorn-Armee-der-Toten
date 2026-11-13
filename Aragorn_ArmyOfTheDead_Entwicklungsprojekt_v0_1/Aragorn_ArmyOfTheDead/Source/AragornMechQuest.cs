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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && defenders == null)
                defenders = new List<Pawn>();
        }

        public override void GameComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing || completed ||
                Find.QuestManager == null || Find.TickManager == null) return;
            if (Current.Game?.GetComponent<AragornGame>()?.Unlocked < 13) return;
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
            if (completed || !mapPopulated || site?.Map == null ||
                Find.TickManager.TicksGame % 60 != 0) return;
            if (defenders.Any(p => p != null && p.Spawned && !p.Dead && !p.Downed))
                return;
            // The mission is a battle, not an exploit to auto-complete without
            // visiting the site or killing the actual defenders.
            if (savedCount <= 0) return;
            completed = true;
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Success, sendLetter: true);
            Find.LetterStack.ReceiveLetter(
                "Der Bann des Königs",
                "Die Maschinen schweigen. Aragorn beherrscht nun den Maschinenstillstand: 30 Felder Reichweite, 6 Felder Wirkungsradius, 10 Sekunden Betäubung, 60 Sekunden Abklingzeit.",
                LetterDefOf.PositiveEvent, site);
            Log.Message("[Aragorn] Mechanoid quest completed; Machine Silence unlocked.");
        }

        private void Offer()
        {
            QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_MechSilenceQuest");
            if (root == null) { Log.ErrorOnce("[Aragorn] Missing mech quest def.", 988208); return; }
            quest = Quest.MakeRaw();
            quest.root = root;
            quest.name = "Das Schweigen des Eisens";
            quest.description = "Ein Heer uralter Maschinen bewacht eine vergessene Festung. "
                + "Aragorn und seine 13 Eidverpflichteten muessen die Mechanoiden besiegen. "
                + "Belohnung: Maschinenstillstand, ein Flaechenstun gegen feindliche Mechanoiden.";
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
                site.customLabel = "Mechanoidenfestung: Das Schweigen des Eisens";
                Find.WorldObjects.Add(site);
                QuestPart_AragornRescueTarget link =
                    quest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (link != null) link.site = site;
                Find.LetterStack.ReceiveLetter("Die Maschinenfestung",
                    "Die Festung wurde auf der Weltkarte entdeckt. "
                    + "Besiege ihre Mechanoiden, um den Bann des Königs zu erhalten.",
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
                IntVec3 center = AragornCryptBuilder.Build(map, map.Center);
                var parms = new PawnGroupMakerParms {
                    faction = faction,
                    groupKind = PawnGroupKindDefOf.Combat,
                    points = frozenThreatPoints > 0 ? frozenThreatPoints : AragornThreat.Points(2.5f)
                };
                var group = PawnGroupMakerUtility.GeneratePawns(parms, warnOnZeroResults: false)
                    .Take(AragornThreat.MaxEnemies).ToList();
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
                Messages.Message("Mechanoidenfestung: " + defenders.Count
                    + " Verteidiger. Besiege sie alle!", defenders[0], MessageTypeDefOf.ThreatBig);
            }
            catch (Exception ex) { Log.Error("[Aragorn] Mech fortress map failed: " + ex); }
        }

        public void DevUnlock(Pawn hero)
        {
            if (!Prefs.DevMode) return;
            completed = true;
            lastPulseTick = -1000000;
            if (quest != null && !quest.Historical)
            {
                if (quest.State == QuestState.NotYetAccepted) quest.Accept(null);
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
                AragornGame.Reject("Maschinenstillstand: Abklingzeit "
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
                    p.stances.stunner.StunFor(600, aragorn);
                    FleckMaker.ThrowLightningGlow(p.DrawPos, p.Map, 1.4f);
                    stunned++;
                }
                FleckMaker.ThrowLightningGlow(target.Cell.ToVector3Shifted(), aragorn.Map, 2.5f);
                Messages.Message("Maschinenstillstand: " + stunned + " feindliche Mechanoiden "
                    + "fuer 10 Sekunden betaeubt.", aragorn, MessageTypeDefOf.PositiveEvent);
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

    public sealed class SitePartWorker_AragornMechSilence : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<AragornMechQuestGame>()?.PopulateMap(map);
        }
    }
}
