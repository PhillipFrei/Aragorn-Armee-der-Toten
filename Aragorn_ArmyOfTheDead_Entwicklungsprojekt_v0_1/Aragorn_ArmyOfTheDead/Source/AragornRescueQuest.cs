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

        public string DiagnosticStatus
        {
            get
            {
                if (Find.QuestManager == null) return "QuestManager ist nicht initialisiert.";
                if (DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_RescueAragornQuest") == null)
                    return "FEHLER: Questdefinition AR_RescueAragornQuest nicht geladen (Player.log prüfen).";
                if (completed) return "Aragorn-Rettung in diesem Spielstand bereits abgeschlossen.";
                Quest q = rescueQuest ?? Find.QuestManager.QuestsListForReading.FirstOrDefault(x =>
                    x != null && x.root != null && x.root.defName == "AR_RescueAragornQuest" && !x.Historical);
                if (q == null) return "Noch keine Rettungsquest erzeugt.";
                if (!Find.QuestManager.Contains(q))
                    return "Quest-Referenz vorhanden, aber nicht im QuestManager.";
                return "Quest vorhanden: " + q.State + (q.dismissed ? " (ausgeblendet)" : "")
                    + (q.hiddenInUI ? " (UI-versteckt)" : "");
            }
        }

        // Immediate manual fallback via Mod Settings. Does not depend on game time
        // ticking or a generated Profaned world faction.
        public string ForceOfferQuest()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null)
                return "Bitte zuerst einen Spielstand vollständig laden.";
            if (completed) return "Rettung bereits abgeschlossen; kein zweiter Auftrag nötig.";
            try
            {
                var root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_RescueAragornQuest");
                if (root == null)
                {
                    Log.Error("[Aragorn] Manuelles Questangebot: QuestScriptDef AR_RescueAragornQuest fehlt.");
                    return "Questdefinition fehlt. Bitte Player.log hochladen.";
                }
                if (rescueQuest == null || !Find.QuestManager.Contains(rescueQuest))
                    rescueQuest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                        q != null && q.root == root && !q.Historical);

                if (rescueQuest == null) OfferQuest();
                if (rescueQuest == null) return "Quest konnte nicht erzeugt werden. Siehe Player.log.";
                rescueQuest.hidden = false;
                rescueQuest.hiddenInUI = false;
                rescueQuest.dismissed = false;

                if (rescueQuest.State == QuestState.NotYetAccepted)
                {
                    QuestUtility.SendLetterQuestAvailable(rescueQuest);
                    rescueOfferLetterSent = true;
                    return "Questangebot ist jetzt unter Aufträge → Verfügbar. Benachrichtigung erstellt.";
                }
                if (rescueQuest.State == QuestState.Ongoing)
                {
                    if (!siteCreated) TryCreateRescueSite();
                    return "Quest bereits angenommen. " + (siteCreated ?
                        "Rettungsort ist auf der Weltkarte." :
                        "Rettungsort noch nicht erstellt – siehe Player.log.");
                }
                return "Queststatus: " + rescueQuest.State;
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Manuelles Questangebot fehlgeschlagen: " + ex);
                return "Fehler beim Erstellen: " + ex.GetType().Name + ". Player.log hochladen.";
            }
        }

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
            // The offer itself requires no home map; the site is placed after acceptance.
            if (completed || Find.QuestManager == null) return;

            if (rescueQuest == null)
            {
                rescueQuest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                    q != null && q.root != null &&
                    q.root.defName == "AR_RescueAragornQuest" && !q.Historical);
                if (rescueQuest == null) OfferQuest();
            }

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
            Quest prior = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                q != null && q.root == root && !q.Historical);
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
            Faction hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null)
            {
                Log.Warning("[Aragorn] Originale Profaned-Fraktion nicht verfügbar.");
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

                Faction guardFaction = ProfanedEncounterUtility.GetOrCreateFaction();
                if (guardFaction == null)
                {
                    Log.Error("[Aragorn] Rescue camp cannot spawn actual Profaned enemy faction.");
                    return;
                }
                if (rescueSite.Faction != guardFaction) rescueSite.SetFaction(guardFaction);
                int count = Mathf.Clamp(AragornMod.Config == null ? 20 : AragornMod.Config.profanedCount, 5, 50);
                // A small slate crypt is now generated rather than only an empty map.
                prisonCell = AragornCryptBuilder.Build(map, prisonCell);
                guards.AddRange(ProfanedEncounterUtility.SpawnGuards(map, prisonCell, guardFaction, count));
                if (guards.Count == 0)
                {
                    Log.Error("[Aragorn] Rettungslager hat keine Wachen erhalten.");
                    return;
                }
                // RimWorld's LordToil_DefendPoint breaks for wanderRadius > Region.GridSize (12).
                LordMaker.MakeNewLord(guardFaction,
                    new LordJob_DefendPoint(prisonCell, wanderRadius: 8f,
                        defendRadius: 28f, isCaravanSendable: false, addFleeToil: false),
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

    public class SitePartWorker_AragornTrial : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<AragornGame>()?.PopulateTrialMap(map);
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
