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
    // v0.6: The real quests tab entry is permanent. Accepting it creates a
    // traversable world-map site; a custom site worker populates the rescue map.
    // This quest does NOT require a Profaned world faction or settlement.
    public sealed class AragornRescueGame : GameComponent
    {
        private Quest rescueQuest;
        private Site rescueSite;
        private Pawn captive;
        private List<Pawn> guards = new List<Pawn>();
        private bool siteCreated;
        private bool encounterCreated;
        private bool completed;
        private int nextRetryTick;
        private bool rescueOfferLetterSent;

        public AragornRescueGame(Game game) { }

        public override void ExposeData()
        {
            Scribe_References.Look(ref rescueQuest, "arRescueQuest");
            Scribe_References.Look(ref rescueSite, "arRescueSite");
            Scribe_References.Look(ref captive, "arRescueCaptive");
            Scribe_Collections.Look(ref guards, "arRescueGuards", LookMode.Reference);
            Scribe_Values.Look(ref siteCreated, "arRescueSiteCreated");
            Scribe_Values.Look(ref encounterCreated, "arRescueEncounterCreated");
            Scribe_Values.Look(ref completed, "arRescueCompleted");
            Scribe_Values.Look(ref nextRetryTick, "arRescueRetryTick");
            Scribe_Values.Look(ref rescueOfferLetterSent, "arRescueOfferLetterSent", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && guards == null)
                guards = new List<Pawn>();
        }

        // Unlike GameComponentTick, GameComponentUpdate also runs while the
        // colony is paused. Always expose the permanent offer after load.
        public override void GameComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (completed || Find.QuestManager == null || Find.AnyPlayerHomeMap == null) return;

            if (rescueQuest == null)
                OfferQuest();

            if (rescueQuest != null && !rescueOfferLetterSent)
            {
                // Also notifies users upgrading from v0.6 whose offer exists
                // but had never generated a visible letter.
                if (rescueQuest.State == QuestState.NotYetAccepted)
                {
                    QuestUtility.SendLetterQuestAvailable(rescueQuest);
                    Messages.Message("Aragorn-Rettungsquest: Unter Aufträge → Verfügbar verfügbar.",
                        MessageTypeDefOf.PositiveEvent);
                }
                rescueOfferLetterSent = true;
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 180 != 0) return;
            if (Find.QuestManager == null || Find.AnyPlayerHomeMap == null) return;

            if (!completed && rescueQuest == null) OfferQuest();
            if (rescueQuest == null || completed) return;
            if (rescueQuest.State == QuestState.Ongoing && !siteCreated && now >= nextRetryTick)
                TryCreateRescueSite();
            if (rescueQuest.State == QuestState.Ongoing && encounterCreated)
                CheckRescueOutcome();
        }

        private void OfferQuest()
        {
            QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_RescueAragornQuest");
            if (root == null)
            {
                Log.ErrorOnce("[Aragorn] AR_RescueAragornQuest nicht geladen. Bitte Player.log prüfen (XML-Fehler).", 906173571);
                return;
            }
            Quest prior = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.root == root && !q.Historical);
            if (prior != null) { rescueQuest = prior; return; }

            Quest quest = Quest.MakeRaw();
            quest.root = root;
            quest.name = "Aragorn finden – der gefangene König";
            quest.description = "Kundschafter berichten von einem gefangenen Krieger namens Aragorn. "
                + "Nimm diesen Auftrag an, um den Gefängnisort auf der Weltkarte aufzudecken. "
                + "Reise mit einer Karawane dorthin und besiege seine Profaned-Bewacher. "
                + "Sobald die Wachen besiegt sind, wird Aragorn befreit und schließt sich dir an. "
                + "\n\nDies ist die erste spielbare Rettungsmission (Teststand): Die individuell "
                + "gestaltete Kryptenkarte folgt später. Der Auftrag hat kein Ablaufdatum. "
                + "Eine Profaned-Weltfraktion ist nicht erforderlich.";
            quest.acceptanceExpireTick = -1;
            quest.SetNotYetAccepted();
            quest.hidden = false;
            quest.hiddenInUI = false;
            quest.dismissed = false;
            quest.AddPart(new QuestPart_AragornRescueTarget());
            Find.QuestManager.Add(quest);
            rescueQuest = quest;
            rescueOfferLetterSent = false;
            Log.Message("[Aragorn] Dauerhafter Rettungsauftrag verfügbar: " + quest.name);
        }

        private void TryCreateRescueSite()
        {
            nextRetryTick = Find.TickManager.TicksGame + 2500;
            if (rescueSite != null) { siteCreated = true; return; }

            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail("AR_RescueCamp");
            if (part == null) { Log.Error("[Aragorn] AR_RescueCamp SitePartDef fehlt."); return; }
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, minDist: 4, maxDist: 18)
                && !TileFinder.TryFindNewSiteTile(out tile, minDist: 2, maxDist: 30))
            {
                Log.Warning("[Aragorn] Kein freies Weltkartenfeld für Aragorns Gefängnis gefunden; versuche es später erneut.");
                return;
            }
            Faction hostile = Faction.OfAncientsHostile ?? Faction.OfMechanoids ?? Faction.OfInsects;
            if (hostile == null)
            {
                Log.Warning("[Aragorn] Keine feindliche Vanilla-Fraktion für die Rettungsmission verfügbar.");
                return;
            }
            try
            {
                Site created = SiteMaker.MakeSite(part, tile, hostile,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: StorytellerUtility.DefaultSiteThreatPointsNow());
                if (created == null) return;
                created.customLabel = "Aragorns Gefängnis";
                Find.WorldObjects.Add(created);
                rescueSite = created;
                siteCreated = true;
                var link = rescueQuest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (link != null) link.site = created;
                Find.LetterStack.ReceiveLetter("Aragorns Gefängnis gefunden",
                    "Der Rettungsort ist jetzt auf der Weltkarte markiert. Reise dorthin, "
                    + "besiege die Profaned-Bewacher und befreie Aragorn.",
                    LetterDefOf.NeutralEvent, created);
                Log.Message("[Aragorn] Rettungsort auf Weltkarte erstellt: " + tile);
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Rettungsort konnte nicht erstellt werden: " + ex);
            }
        }

        // Called by the rescue SitePartWorker exactly when the site map is generated.
        public void PopulateRescueMap(Map map)
        {
            if (encounterCreated || map == null || rescueSite == null || map.Parent != rescueSite) return;
            FactionDef profanedDef = DefDatabase<FactionDef>.GetNamedSilentFail("BotchJob_ProfanedFaction");
            if (profanedDef == null)
            {
                Log.Error("[Aragorn] The Profaned ist nicht geladen: BotchJob_ProfanedFaction fehlt.");
                return;
            }
            PawnKindDef aragornKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("AR_Aragorn");
            if (aragornKind == null) { Log.Error("[Aragorn] PawnKind AR_Aragorn fehlt."); return; }

            try
            {
                IntVec3 prisonCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 25);
                PawnGenerationRequest request = new PawnGenerationRequest(aragornKind,
                    faction: null, forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false, allowAddictions: false,
                    forceBaselinerChance: 1f, fixedGender: Gender.Male);
                Pawn prisoner = PawnGenerator.GeneratePawn(request);
                prisoner.Name = new NameSingle("Aragorn");
                GenSpawn.Spawn(prisoner, prisonCell, map);
                // Keeps the prisoner from wandering off while the guards are alive.
                prisoner.health.AddHediff(HediffDefOf.Anesthetic);
                captive = prisoner;

                var group = profanedDef.pawnGroupMakers == null ? null
                    : profanedDef.pawnGroupMakers.FirstOrDefault(x => x.kindDef == PawnGroupKindDefOf.Combat);
                var options = group == null ? new List<PawnGenOption>()
                    : group.options.Where(x => x.kind != null && x.selectionWeight > 0).ToList();
                if (options.Count == 0)
                {
                    Log.Error("[Aragorn] Keine Profaned-Kampfgruppenoptionen gefunden; der Rettungskampf kann nicht beginnen.");
                    return;
                }

                Faction guardFaction = rescueSite.Faction;
                int count = Mathf.Clamp(AragornMod.Config == null ? 20 : AragornMod.Config.profanedCount, 5, 50);
                for (int i = 0; i < count; i++)
                {
                    PawnGenOption choice = options.RandomElementByWeight(x => x.selectionWeight);
                    try
                    {
                        Pawn guard = PawnGenerator.GeneratePawn(choice.kind, guardFaction);
                        IntVec3 pos = CellFinder.RandomClosewalkCellNear(prisonCell, map, 18);
                        GenSpawn.Spawn(guard, pos, map);
                        guards.Add(guard);
                    }
                    catch (Exception e) { Log.Warning("[Aragorn] Profaned-Wache konnte nicht erzeugt werden: " + e.Message); }
                }
                if (guards.Count == 0)
                {
                    Log.Error("[Aragorn] Rettungslager hat keine Wachen erhalten.");
                    return;
                }
                LordMaker.MakeNewLord(guardFaction,
                    new LordJob_DefendPoint(prisonCell, wanderRadius: 20f,
                        defendRadius: 30f, isCaravanSendable: false, addFleeToil: false),
                    map, guards);
                encounterCreated = true;
                Messages.Message("Aragorn ist hier gefangen! Besiege seine " + guards.Count
                    + " Profaned-Bewacher.", prisoner, MessageTypeDefOf.ThreatBig);
                Log.Message("[Aragorn] Rettungsmission gestartet: " + guards.Count + " Wachen.");
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Fehler beim Erzeugen der Rettungsbegegnung: " + ex);
            }
        }

        private void CheckRescueOutcome()
        {
            if (captive == null || captive.Dead || captive.Destroyed)
            {
                Log.Warning("[Aragorn] Der Gefangene ist gestorben oder verschwunden.");
                Finish(QuestEndOutcome.Fail);
                return;
            }
            if (rescueSite == null || !rescueSite.HasMap) return;
            Map map = rescueSite.Map;
            if (guards.Any(g => g != null && !g.Dead && !g.Downed && g.Spawned && g.Map == map))
                return;

            if (!captive.Spawned || captive.Map != map) return;
            try
            {
                // Aragorn joins only after the guards are defeated, never on quest acceptance.
                Hediff anesthetic = captive.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);
                if (anesthetic != null) captive.health.RemoveHediff(anesthetic);
                captive.SetFaction(Faction.OfPlayer);
                Finish(QuestEndOutcome.Success);
                Messages.Message("Aragorn ist gerettet und schließt sich deiner Kolonie an!",
                    captive, MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Fehler beim Befreien Aragorns: " + ex);
            }
        }

        private void Finish(QuestEndOutcome outcome)
        {
            if (rescueQuest != null && !rescueQuest.Historical)
                rescueQuest.End(outcome, sendLetter: true);
            completed = outcome == QuestEndOutcome.Success;
            if (outcome != QuestEndOutcome.Success)
            {
                rescueQuest = null;
                rescueSite = null;
                captive = null;
                guards.Clear();
                siteCreated = false;
                encounterCreated = false;
            }
        }
    }

    public class QuestNode_PermanentAragorn : QuestNode
    {
        protected override void RunInt() { }
        protected override bool TestRunInt(Slate slate) { return true; }
    }

    public class QuestPart_AragornRescueTarget : QuestPart
    {
        public Site site;
        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get { if (site != null) yield return site; }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "arSite");
        }
    }

    public class SitePartWorker_AragornRescue : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<AragornRescueGame>()?.PopulateRescueMap(map);
        }
    }
}
