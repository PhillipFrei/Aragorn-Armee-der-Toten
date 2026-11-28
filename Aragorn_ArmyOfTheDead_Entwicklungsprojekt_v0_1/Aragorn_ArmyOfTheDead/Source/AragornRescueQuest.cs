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
    public sealed partial class AragornRescueGame : GameComponent
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
        private int abandonedSinceTick = -1;
        // Only newly built moat-fortress rescue maps have this objective.
        // Old already-generated rescue sites retain their previous layout.
        private bool moatRequiresPreparation;
        private bool moatCrossingPrepared;

        public AragornRescueGame(Game game) { }

        public void DevCompleteForTesting(Pawn hero)
        {
            if (!Prefs.DevMode || hero == null) return;
            completed = true;
            // Do not change any other rescue pawn's faction: a Dev unlock on an
            // already-recruited Aragorn must never duplicate the hero.
            if (rescueQuest != null && !rescueQuest.Historical)
            {
                if (rescueQuest.State == QuestState.NotYetAccepted)
                    rescueQuest.Accept(hero);
                rescueQuest.End(QuestEndOutcome.Success, sendLetter: false);
            }
            rescueOfferLetterSent = true;
            Log.Message("[Aragorn] Dev completed rescue quest.");
        }

        public string DiagnosticStatus
        {
            get
            {
                if (Find.QuestManager == null) return AragornText.T("AR_RescueStatus_NoManager");
                if (DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_RescueAragornQuest") == null)
                    return AragornText.T("AR_RescueStatus_MissingDef");
                if (completed) return AragornText.T("AR_RescueStatus_Done");
                Quest q = rescueQuest ?? Find.QuestManager.QuestsListForReading.FirstOrDefault(x =>
                    x != null && x.root != null && x.root.defName == "AR_RescueAragornQuest" && !x.Historical);
                if (q == null) return AragornText.T("AR_RescueStatus_NoQuest");
                if (!Find.QuestManager.Contains(q))
                    return AragornText.T("AR_RescueStatus_Orphan");
                return AragornText.F("AR_RescueStatus_QuestPresent", q.State)
                    + (q.dismissed ? AragornText.T("AR_RescueStatus_Dismissed") : "")
                    + (q.hiddenInUI ? AragornText.T("AR_RescueStatus_Hidden") : "");
            }
        }

        // Immediate manual fallback via Mod Settings. Does not depend on game time
        // ticking or a generated Profaned world faction.
        public string ForceOfferQuest()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null)
                return AragornText.T("AR_Dev_LoadSave");
            if (completed) return AragornText.T("AR_RescueStatus_DoneNoSecond");
            try
            {
                var root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_RescueAragornQuest");
                if (root == null)
                {
                    Log.Error("[Aragorn] Manuelles Questangebot: QuestScriptDef AR_RescueAragornQuest fehlt.");
                    return AragornText.T("AR_RescueStatus_NoDefLog");
                }
                if (rescueQuest == null || !Find.QuestManager.Contains(rescueQuest))
                    rescueQuest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                        q != null && q.root == root && !q.Historical);

                if (rescueQuest == null) OfferQuest();
                if (rescueQuest == null) return AragornText.T("AR_RescueStatus_CreateFailed");
                rescueQuest.hidden = false;
                rescueQuest.hiddenInUI = false;
                rescueQuest.dismissed = false;

                if (rescueQuest.State == QuestState.NotYetAccepted)
                {
                    QuestUtility.SendLetterQuestAvailable(rescueQuest);
                    rescueOfferLetterSent = true;
                    return AragornText.T("AR_RescueStatus_Offered");
                }
                if (rescueQuest.State == QuestState.Ongoing)
                {
                    if (!siteCreated) TryCreateRescueSite();
                    return AragornText.T("AR_RescueStatus_Accepted") + " " + (siteCreated ?
                        AragornText.T("AR_RescueStatus_SiteCreated") :
                        AragornText.T("AR_RescueStatus_SiteMissing"));
                }
                return AragornText.F("AR_RescueStatus_State", rescueQuest.State);
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Manuelles Questangebot fehlgeschlagen: " + ex);
                return AragornText.F("AR_RescueStatus_Exception", ex.GetType().Name);
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
            Scribe_Values.Look(ref abandonedSinceTick, "arRescueAbandonedSince", -1);
            Scribe_Values.Look(ref moatRequiresPreparation,
                "arRescueMoatRequiresPreparation", false);
            Scribe_Values.Look(ref moatCrossingPrepared,
                "arRescueMoatCrossingPrepared", false);
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
                    Messages.Message(AragornText.T("AR_Msg_RescueAvailable"),
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
            if (rescueQuest.Historical)
            {
                ResetAbandonedRescue();
                return;
            }
            if (encounterCreated && rescueQuest.State == QuestState.Ongoing)
            {
                bool absent = rescueSite == null || !rescueSite.HasMap ||
                    !AragornThreat.HasPlayerPawns(rescueSite.Map);
                if (!absent) abandonedSinceTick = -1;
                else if (abandonedSinceTick < 0) abandonedSinceTick = now;
                else if (now - abandonedSinceTick >= 1200)
                {
                    ResetAbandonedRescue();
                    return;
                }
            }
            if (rescueQuest.State == QuestState.Ongoing && !siteCreated && now >= nextRetryTick)
                TryCreateRescueSite();

            // The old PostMapGenerate-only route silently stranded quests
            // when the map layout threw AFTER clearing the surface: the site
            // remained loaded with an empty map forever. Repair it in-place
            // on the next game tick interval; no re-acceptance is required.
            if (rescueQuest.State == QuestState.Ongoing &&
                siteCreated && !encounterCreated &&
                rescueSite != null && rescueSite.HasMap &&
                now >= nextRetryTick)
            {
                nextRetryTick = now + 600;
                Log.Warning("[Aragorn] Empty/incomplete rescue site detected; " +
                    "retrying fortress and quest NPC population on loaded map.");
                PopulateRescueMap(rescueSite.Map);
            }
            if (rescueQuest.State == QuestState.Ongoing && encounterCreated)
            {
                EnsureBridgeSite();
                CheckRescueOutcome();
            }
        }

        private void ResetAbandonedRescue()
        {
            if (completed) return;
            Log.Message("[Aragorn] Rescue map left/quest aborted; fresh rescue offer queued.");
            if (rescueQuest != null && !rescueQuest.Historical)
                rescueQuest.End(QuestEndOutcome.Fail, sendLetter: false);
            rescueQuest = null;
            rescueSite = null;
            captive = null;
            guards.Clear();
            siteCreated = false;
            encounterCreated = false;
            rescueOfferLetterSent = false;
            abandonedSinceTick = -1;
            moatRequiresPreparation = false;
            moatCrossingPrepared = false;
            nextRetryTick = Find.TickManager.TicksGame + 600;
            Messages.Message(AragornText.T("AR_Msg_RescueAborted"),
                MessageTypeDefOf.NeutralEvent);
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
            quest.name = AragornText.T("AR_Rescue_Title");
            quest.description = AragornText.T("AR_Rescue_Description");
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
                    threatPoints: AragornThreat.Points(1.0f));
                if (created == null) return;
                created.customLabel = AragornText.T("AR_Rescue_Site");
                Find.WorldObjects.Add(created);
                rescueSite = created;
                siteCreated = true;
                var link = rescueQuest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (link != null) link.site = created;
                Find.LetterStack.ReceiveLetter(AragornText.T("AR_Rescue_ArrivalTitle"),
                    AragornText.T("AR_Rescue_ArrivalBody"),
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
                IntVec3 guardPost;
                IntVec3 prisonCell;
                try
                {
                    Log.Message("[Aragorn] Rescue map stage 1: clearing terrain " +
                        "and building 77x65 moat fortress.");
                    prisonCell = AragornCryptBuilder.BuildRescuePrison(
                        map, map.Center, out guardPost);
                    moatRequiresPreparation = true;
                    moatCrossingPrepared = false;
                    Log.Message("[Aragorn] Rescue map stage 2: moat, " +
                        "barricades and nested prison complete.");
                }
                catch (Exception layoutException)
                {
                    Log.Error("[Aragorn] Main rescue fortress construction " +
                        "failed. Switching to emergency rescue prison. " +
                        "Original error: " + layoutException);
                    prisonCell = AragornRescueMoatFortress.BuildRecoveryPrison(
                        map, out guardPost);
                    moatRequiresPreparation = false;
                    moatCrossingPrepared = true; // emergency land path is open
                }
                PawnGenerationRequest request = new PawnGenerationRequest(aragornKind,
                    faction: null, forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false, allowAddictions: false,
                    forceBaselinerChance: 1f, fixedGender: Gender.Male);
                // If this is a repair of a partly populated map, keep the
                // ORIGINAL Aragorn pawn; never create a second hero and
                // never reset an already captured player character.
                Pawn prisoner = captive != null && captive.Spawned &&
                    captive.Map == map && !captive.Dead
                        ? captive
                        : map.mapPawns.AllPawnsSpawned.FirstOrDefault(p =>
                            p != null && !p.Dead && GhostDefinitions.IsAragorn(p) &&
                            p.Faction != Faction.OfPlayer);
                if (prisoner == null)
                {
                    prisoner = PawnGenerator.GeneratePawn(request);
                    AragornGame.PrepareAragorn(prisoner, showMessage: false);
                    GenSpawn.Spawn(prisoner, prisonCell, map);
                    prisoner.health.AddHediff(HediffDefOf.Anesthetic);
                }
                captive = prisoner;
                Log.Message("[Aragorn] Rescue map stage 3: captive is present.");

                Faction guardFaction = ProfanedEncounterUtility.GetOrCreateFaction();
                if (guardFaction == null)
                {
                    Log.Error("[Aragorn] Rescue camp cannot spawn actual Profaned enemy faction.");
                    return;
                }
                if (rescueSite.Faction != guardFaction) rescueSite.SetFaction(guardFaction);
                int count = Mathf.Clamp(AragornMod.Config == null ? 20 : AragornMod.Config.profanedCount, 5, 50);
                // Four Profaned bow detachments hold distinct barricade
                // rows. Spawn explicitly BEHIND cover and assign one short-
                // range defending Lord to each of the four strongpoints.
                // A failed prior attempt may have spawned some guards but
                // never marked the encounter complete. Do not duplicate them
                // every time the recovery logic runs.
                foreach (Pawn orphan in guards.Where(p =>
                    p != null && p.Spawned && p.Map == map).ToList())
                {
                    orphan.Destroy(DestroyMode.Vanish);
                }
                guards.Clear();
                List<IntVec3> archerPosts = AragornRescueMoatFortress.ArcherPosts(map);
                int perArcherPost = Math.Max(2, (count + 3) / 4);
                for (int i = 0; i < archerPosts.Count; i++)
                {
                    IntVec3 at = archerPosts[i];
                    List<Pawn> archers = ProfanedEncounterUtility.SpawnThemedGuards(
                        map, at, guardFaction, perArcherPost, "archers",
                        AragornRescueMoatFortress.ArcherCoverPositions(map, i));
                    guards.AddRange(archers);
                    if (archers.Count > 0)
                        LordMaker.MakeNewLord(guardFaction,
                            new LordJob_DefendPoint(at, wanderRadius: 2f,
                                defendRadius: 18f, isCaravanSendable: false,
                                addFleeToil: false),
                            map, archers);
                }

                // The inner keep is not empty: skeletons wait on BOTH
                // sides of Aragorn's second prison wall, ready to attack
                // any player who breaches the outer keep gate.
                int perSkeletonFlank = Math.Max(3, count / 5);
                foreach (bool east in new[] { false, true })
                {
                    IntVec3 post = map.Center +
                        new IntVec3(east ? 9 : -9, 0, 0);
                    List<Pawn> skeletons = ProfanedEncounterUtility.SpawnThemedGuards(
                        map, post, guardFaction, perSkeletonFlank, "skeletons",
                        AragornRescueMoatFortress.SkeletonPositions(map, east));
                    guards.AddRange(skeletons);
                    if (skeletons.Count > 0)
                        LordMaker.MakeNewLord(guardFaction,
                            new LordJob_DefendPoint(post, wanderRadius: 2.5f,
                                defendRadius: 14f, isCaravanSendable: false,
                                addFleeToil: false),
                            map, skeletons);
                }
                if (guards.Count == 0)
                    throw new InvalidOperationException(
                        "No Profaned defenders spawned on the rescue map.");
                Log.Message("[Aragorn] Rescue map stage 4: " +
                    guards.Count + " Profaned bowmen/skeletons spawned.");
                encounterCreated = true;
                Messages.Message(AragornText.F("AR_Rescue_Encounter", guards.Count),
                    prisoner, MessageTypeDefOf.ThreatBig);
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_Rescue_MoatTitle"),
                    AragornText.T("AR_Rescue_MoatBody"),
                    LetterDefOf.NeutralEvent,
                    new TargetInfo(AragornRescueMoatFortress.SouthBank(map), map));
                Log.Message("[Aragorn] Rettungsmission gestartet: " + guards.Count + " Wachen.");
            }
            catch (Exception ex)
            {
                // ALWAYS retry a failed map, even when the map itself already
                // exists and the site remains marked siteCreated=true.
                // This was the cause of permanently empty quest locations.
                nextRetryTick = Find.TickManager.TicksGame + 600;
                encounterCreated = false;
                Log.Error("[Aragorn] Rescue generation failed; automated " +
                    "in-place repair scheduled: " + ex);
            }
        }

        // No preconstructed bridge: the user must bring a colonist to
        // the SOUTHERN bank and explicitly choose to prepare the crossing.
        // This is a safe quest action because vanilla bridges cannot
        // normally be built on truly deep water (WaterDeep).
        public IEnumerable<Gizmo> GetMoatGizmos(Pawn pawn)
        {
            if (!moatRequiresPreparation || moatCrossingPrepared ||
                !encounterCreated || completed || rescueSite == null ||
                !rescueSite.HasMap || pawn == null || !pawn.Spawned ||
                pawn.Dead || pawn.Downed || pawn.Map != rescueSite.Map ||
                pawn.Faction != Faction.OfPlayer ||
                !pawn.RaceProps.Humanlike)
                yield break;

            Map map = rescueSite.Map;
            IntVec3 bank = AragornRescueMoatFortress.SouthBank(map);
            var cmd = new Command_Action {
                defaultLabel = AragornText.T("AR_Rescue_PrepareCrossing"),
                defaultDesc = AragornText.T("AR_Rescue_PrepareCrossingDesc"),
                icon = ContentFinder<Texture2D>.Get("Aragorn/Gizmos/GhostTrial", false),
                action = () => PrepareMoatCrossing(pawn)
            };
            if (pawn.Position.DistanceTo(bank) > 5f)
                cmd.Disable(AragornText.T("AR_Rescue_GoToSouthBank"));
            yield return cmd;
        }

        internal void FinishMoatCrossing(Pawn pawn)
        {
            if (!moatRequiresPreparation || moatCrossingPrepared ||
                !encounterCreated || rescueSite == null ||
                !rescueSite.HasMap || pawn == null || !pawn.Spawned ||
                pawn.Downed || pawn.Faction != Faction.OfPlayer ||
                pawn.Map != rescueSite.Map)
                return;
            Map map = rescueSite.Map;
            IntVec3 bank = AragornRescueMoatFortress.SouthBank(map);
            if (pawn.Position.DistanceTo(bank) > 5f) return;
            try
            {
                AragornRescueMoatFortress.PrepareCrossing(map);
                if (!AragornRescueMoatFortress.CrossingIsPrepared(map))
                    throw new InvalidOperationException(
                        "The rescue crossing did not become walkable.");
                moatCrossingPrepared = true;
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_Rescue_CrossingReadyTitle"),
                    AragornText.T("AR_Rescue_CrossingReadyBody"),
                    LetterDefOf.PositiveEvent,
                    new TargetInfo(bank, map));
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Rescue crossing could not be prepared: " + ex);
                Messages.Message(AragornText.T("AR_Rescue_CrossingFailed"),
                    pawn, MessageTypeDefOf.RejectInput);
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
                Messages.Message(AragornText.T("AR_Rescue_Success"),
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
                abandonedSinceTick = -1;
                moatRequiresPreparation = false;
                moatCrossingPrepared = false;
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
