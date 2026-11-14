using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AragornArmy
{
    // Single, persistent story mission after Aragorn's rescue.
    // Divine Order smith does the forging; Aragorn only needs to remain near the forge.
    public sealed class AragornForgeGame : GameComponent
    {
        public const int ForgeTicksRequired = 15000; // 6 in-game hours of guard duty (7 waves).
        private Quest quest;
        private Site site;
        private Pawn smith;
        private Thing forge;
        private Pawn aragorn;
        private bool completed;
        private bool offeredLetter;
        private bool built;
        private bool provisionalAndurilRemoved;
        private int progress;
        private int waves;
        private int siegeStartTick = -1;
        private int nextWaveTick = -1;
        private int retryTick;
        private int lastPropertyPenaltyTick = -999999;
        private int abandonedSinceTick = -1;
        private const int WaveIntervalTicks = 2143; // ~51.4 in-game minutes, 1/7 shorter than 2500
        private const int TotalWaves = 7;

        public AragornForgeGame(Game game) { }

        public void DevCompleteForTesting(Pawn hero)
        {
            if (!Prefs.DevMode || hero == null) return;
            // Give the precise same weapon as a real completed forge mission.
            GhostDefinitions.SetFixedWeapon(hero, "AR_Anduril");
            if (!GhostDefinitions.HasAnduril(hero))
                throw new InvalidOperationException("Andúril konnte nicht ausgerüstet werden.");
            completed = true;
            progress = ForgeTicksRequired;
            waves = TotalWaves;
            if (quest != null && !quest.Historical)
            {
                if (quest.State == QuestState.NotYetAccepted)
                    quest.Accept(hero);
                quest.End(QuestEndOutcome.Success, sendLetter: false);
            }
            Log.Message("[Aragorn] Dev completed Divine Order forging quest.");
        }
        public bool Completed => completed;
        public int ProgressPercent => Mathf.Clamp(Mathf.RoundToInt(progress * 100f / ForgeTicksRequired), 0, 100);
        public Thing ForgeBuilding => forge;
        public bool IsForgeMap(Map map) => site != null && site.HasMap && site.Map == map && forge != null;
        public bool ShowForgeProgress(Map map) =>
            map != null && IsForgeMap(map) && built && !completed &&
            quest != null && quest.State == QuestState.Ongoing;
        public string ForgeProgressLabel =>
            AragornText.F("AR_Forge_Progress", ProgressPercent, waves,
                AragornText.T(IsForgingNow() ? "AR_Forge_Running" : "AR_Forge_Paused"));
        public float ForgeProgressFraction => Mathf.Clamp01(progress / (float)ForgeTicksRequired);
        private IntVec3 SmithStandCell =>
            forge == null ? IntVec3.Invalid : forge.Position + new IntVec3(0, 0, -2);
        private IntVec3 AragornStandCell =>
            forge == null ? IntVec3.Invalid : forge.Position + new IntVec3(0, 0, -5);
        private bool IsForgingNow()
        {
            if (forge == null || !forge.Spawned || smith == null || smith.Dead ||
                smith.Downed || !smith.Spawned || aragorn == null ||
                aragorn.Dead || aragorn.Downed || !aragorn.Spawned ||
                aragorn.Map != forge.Map || smith.Map != forge.Map) return false;
            JobDef guardJob = DefDatabase<JobDef>.GetNamedSilentFail("AR_WaitForAnduril");
            JobDef smithJob = DefDatabase<JobDef>.GetNamedSilentFail("AR_SmithAnduril");
            return aragorn.jobs?.curJob?.def == guardJob &&
                smith.jobs?.curJob?.def == smithJob &&
                aragorn.Position.DistanceTo(AragornStandCell) <= 1.5f &&
                smith.Position.DistanceTo(SmithStandCell) <= 1.5f;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref quest, "arForgeQuest");
            Scribe_References.Look(ref site, "arForgeSite");
            Scribe_References.Look(ref smith, "arForgeSmith");
            Scribe_References.Look(ref forge, "arForgeBench");
            Scribe_References.Look(ref aragorn, "arForgeAragorn");
            Scribe_Values.Look(ref completed, "arForgeCompleted");
            Scribe_Values.Look(ref offeredLetter, "arForgeLetter");
            Scribe_Values.Look(ref built, "arForgeBuilt");
            Scribe_Values.Look(ref provisionalAndurilRemoved, "arForgeProvisionalSwordRemoved");
            Scribe_Values.Look(ref progress, "arForgeProgress");
            Scribe_Values.Look(ref waves, "arForgeWaves");
            Scribe_Values.Look(ref siegeStartTick, "arForgeSiegeStartTick", -1);
            Scribe_Values.Look(ref nextWaveTick, "arForgeNextWaveTick", -1);
            Scribe_Values.Look(ref retryTick, "arForgeRetry");
            Scribe_Values.Look(ref lastPropertyPenaltyTick, "arForgePropertyPenaltyTick", -999999);
            Scribe_Values.Look(ref abandonedSinceTick, "arForgeAbandonedSince", -1);
        }

        // Development-only convenience: rerun v0.13 without needing a save
        // from before the previously completed v0.12 smithing quest.
        public string RestartForTesting()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null)
                return "Bitte zunächst einen Spielstand laden.";
            if (!completed)
                return "Schmiedequest ist noch nicht abgeschlossen. Wiederholung ist nur nach Erfolg möglich.";
            completed = false;
            quest = null;
            site = null;
            smith = null;
            forge = null;
            aragorn = null;
            offeredLetter = false;
            built = false;
            provisionalAndurilRemoved = false;
            progress = 0;
            waves = 0;
            siegeStartTick = -1;
            nextWaveTick = -1;
            lastPropertyPenaltyTick = -999999;
            retryTick = Find.TickManager.TicksGame;
            Log.Message("[Aragorn] Forge quest reset for development testing.");
            return "Neue Andúril-Schmiedequest wird sofort wieder angeboten. "
                + "Vorhandenes Andúril bleibt bis zur Annahme erhalten.";
        }

        private static Pawn FindSavedAragorn()
        {
            if (Find.Maps == null) return null;
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn.Faction == Faction.OfPlayer && GhostDefinitions.IsAragorn(pawn) && !pawn.Dead)
                        return pawn;
            return null;
        }

        public override void GameComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null || completed) return;
            if (Find.TickManager.TicksGame < retryTick) return;
            if (quest != null && quest.Historical && !completed)
            {
                FailForge("Die Quest wurde abgebrochen.");
                return;
            }
            if (quest == null)
            {
                QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_ReforgeAndurilQuest");
                if (root == null) return;
                quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q != null
                    && q.root == root && !q.Historical);
                if (quest == null)
                {
                    Pawn hero = FindSavedAragorn();
                    if (hero != null) OfferForgeQuest(hero, root);
                }
            }
            if (quest != null && quest.State == QuestState.NotYetAccepted && !offeredLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
                offeredLetter = true;
            }
        }

        private void OfferForgeQuest(Pawn hero, QuestScriptDef root)
        {
            // Do not remove existing v0.11 Anduril until the player actually accepts
            // the new quest. This avoids changing saved pawns merely on mod load.
            Quest q = Quest.MakeRaw();
            q.root = root;
            q.name = AragornText.T("AR_Forge_Title");
            q.description = AragornText.T("AR_Forge_Description");
            q.acceptanceExpireTick = -1;
            q.SetNotYetAccepted();
            q.hidden = false;
            q.hiddenInUI = false;
            q.dismissed = false;
            q.AddPart(new QuestPart_AragornRescueTarget());
            Find.QuestManager.Add(q);
            quest = q;
            offeredLetter = false;
            Log.Message("[Aragorn] Anduril reforging quest offered after rescue.");
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0 || quest == null || completed) return;
            if (quest.State != QuestState.Ongoing) return;
            if (built)
            {
                bool absent = site == null || !site.HasMap ||
                    !AragornThreat.HasPlayerPawns(site.Map);
                int nowTick = Find.TickManager.TicksGame;
                if (!absent) abandonedSinceTick = -1;
                else if (abandonedSinceTick < 0) abandonedSinceTick = nowTick;
                else if (nowTick - abandonedSinceTick >= 1200)
                {
                    FailForge("Die Schmiedekarte wurde verlassen. Der Auftrag wird erneut angeboten.");
                    return;
                }
            }
            // Older test versions gave Anduril immediately. Once this quest is
            // accepted, replace that provisional reward with the reforged sword.
            if (!provisionalAndurilRemoved)
            {
                Pawn hero = FindSavedAragorn();
                if (hero != null && hero.equipment?.Primary?.def?.defName == "AR_Anduril")
                    hero.equipment.DestroyAllEquipment();
                provisionalAndurilRemoved = true;
            }

            if (site == null && Find.TickManager.TicksGame >= retryTick)
            {
                CreateForgeSite();
                return;
            }
            if (site == null || !site.HasMap || !built) return;
            if (smith == null || smith.Dead || smith.Destroyed || forge == null || forge.Destroyed)
            {
                FailForge(AragornText.T("AR_Smith_Died"));
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (aragorn == null || aragorn.Dead || aragorn.Destroyed)
            {
                aragorn = FindSavedAragorn();
                if (aragorn == null) return; // waiting for the player to arrive with Aragorn
            }
            if (!aragorn.Spawned || aragorn.Map != site.Map) return;
            if (smith.Downed || aragorn.Downed) return;
            // Keep the Divine Order smith working at the actual vanilla bench.
            // Unlike Aragorn, this NPC may have zero crafting skill.
            JobDef smithJob = DefDatabase<JobDef>.GetNamedSilentFail("AR_SmithAnduril");
            if (smithJob != null && smith.jobs != null &&
                smith.jobs.curJob?.def != smithJob)
            {
                try
                {
                    smith.jobs.StartJob(JobMaker.MakeJob(smithJob, forge, SmithStandCell),
                        JobCondition.InterruptForced);
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("[Aragorn] Smith work animation job failed: " +
                        ex.Message, 190734);
                }
            }

            // Both smith and Aragorn must physically hold their posts AND
            // execute their dedicated jobs. A forced retreat freezes everything.
            // WAVE TIMING IS BASED ON FORGING PROGRESS, not world time.
            // Thus waves cannot stack while the ritual is interrupted.
            if (!IsForgingNow()) return;
            if (siegeStartTick < 0)
            {
                siegeStartTick = now;
                Messages.Message(AragornText.T("AR_Forge_SiegeStart"),
                    forge, MessageTypeDefOf.ThreatBig);
            }
            while (waves < TotalWaves && progress >= waves * WaveIntervalTicks)
                SpawnWave();
            progress += 60;
            if (progress % 360 == 0)
                FleckMaker.ThrowDustPuffThick(forge.DrawPos, site.Map, 1.2f,
                    new Color(1f, .68f, .20f));
            if (progress >= ForgeTicksRequired)
            {
                CompleteForge();
            }
        }

        private void CreateForgeSite()
        {
            retryTick = Find.TickManager.TicksGame + 2500;
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail("AR_AndurilForgeSite");
            Faction divine = GetOrCreateDivineQuestFaction();
            if (part == null || divine == null)
            {
                Log.ErrorOnce("[Aragorn] Missing forge SitePartDef or Divine Order faction.", 87623100);
                return;
            }
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, minDist: 4, maxDist: 20)
                && !TileFinder.TryFindNewSiteTile(out tile, minDist: 2, maxDist: 35))
                return;
            try
            {
                Site found = SiteMaker.MakeSite(part, tile, divine,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: AragornThreat.Points(1.75f));
                found.customLabel = AragornText.T("AR_Forge_Site");
                Find.WorldObjects.Add(found);
                site = found;
                QuestPart_AragornRescueTarget target = quest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (target != null) target.site = found;
                Find.LetterStack.ReceiveLetter(
                    AragornText.T("AR_Forge_ArrivalTitle"),
                    AragornText.T("AR_Forge_ArrivalBody"),
                    LetterDefOf.NeutralEvent, found);
                Log.Message("[Aragorn] Anduril forge world site placed: " + tile);
            }
            catch (Exception ex) { Log.Error("[Aragorn] Could not create forge site: " + ex); }
        }

        // A dedicated hidden Divine Order escort faction keeps smith NPCs neutral
        // without changing diplomacy with the actual Divine Order faction in the world.
        private static Faction GetOrCreateDivineQuestFaction()
        {
            if (Find.FactionManager == null) return null;
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("BotchJob_DivineOrderFaction");
            if (def == null) return null;
            Faction existing = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(
                f => f != null && f.def == def && f.Hidden && f.Name == "Divine Order - Aragorn Forge");
            if (existing != null) return existing;
            try
            {
                Faction fac = FactionGenerator.NewGeneratedFaction(
                    new FactionGeneratorParms(def, hidden: true));
                fac.Name = "Divine Order - Aragorn Forge";
                Find.FactionManager.Add(fac);
                FactionRelation rel = new FactionRelation(Faction.OfPlayer, FactionRelationKind.Neutral)
                { baseGoodwill = 0 };
                fac.SetRelation(rel);
                fac.RelationWith(Faction.OfPlayer).baseGoodwill = 0;
                Faction.OfPlayer.RelationWith(fac).baseGoodwill = 0;
                Faction profaned = ProfanedEncounterUtility.GetOrCreateFaction();
                if (profaned != null)
                {
                    fac.SetRelation(new FactionRelation(profaned, FactionRelationKind.Hostile)
                        { baseGoodwill = -100 });
                    fac.RelationWith(profaned).baseGoodwill = -100;
                    profaned.RelationWith(fac).baseGoodwill = -100;
                }
                return fac;
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Could not initialize Divine Order quest faction: " + ex);
                return null;
            }
        }

        public void PopulateForgeMap(Map map)
        {
            if (site == null || map == null || map.Parent != site || built) return;
            try
            {
                forge = AndurilForgeBuilder.Build(map, map.Center, site.Faction);
                if (forge == null)
                {
                    Log.Error("[Aragorn] Forge building not created. No encounter started.");
                    return;
                }
                // Champion receives the original Divine Order plate armor and open
                // plate helmet by default, much tougher than a commoner.
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("BotchJob_DivineOrderChampion");
                if (kind == null) { Log.Error("[Aragorn] Divine Order Champion PawnKind missing."); return; }
                Faction divine = site.Faction;
                smith = PawnGenerator.GeneratePawn(kind, divine);
                smith.Name = new NameSingle(AragornText.T("AR_Forge_SmithName"));
                IntVec3 smithCell = SmithStandCell;
                if (!smithCell.InBounds(map) || !smithCell.Standable(map))
                {
                    Log.Error("[Aragorn] Fixed smith workstation cell obstructed at " + smithCell);
                    return;
                }
                GenSpawn.Spawn(smith, smithCell, map);
                smith.Rotation = Rot4.North;
                // The Divine Order blacksmith is a neutral quest NPC, never a colonist.
                // He can still be attacked or killed by invading Profaned.
                JobDef smithJob = DefDatabase<JobDef>.GetNamedSilentFail("AR_SmithAnduril");
                if (smithJob != null && smith.jobs != null)
                    smith.jobs.StartJob(JobMaker.MakeJob(smithJob, forge, SmithStandCell),
                        JobCondition.InterruptForced);
                built = true;
                Messages.Message(AragornText.T("AR_Forge_Ready"),
                    forge, MessageTypeDefOf.PositiveEvent);
                Log.Message("[Aragorn] Golden forge site generated, Divine Order smith at " + smithCell);
            }
            catch (Exception ex) { Log.Error("[Aragorn] Forge map generation failure: " + ex); }
        }

        private void SpawnWave()
        {
            int index = waves++;
            Faction enemy = ProfanedEncounterUtility.GetOrCreateFaction();
            if (enemy == null || site?.Map == null) return;
            Map map = site.Map;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map),
                map, CellFinder.EdgeRoadChance_Hostile, out IntVec3 edge))
                edge = CellFinder.RandomEdgeCell(map);

            int baseCount = AragornMod.Config == null ? 26 : AragornMod.Config.forgeWaveBaseCount;
            int count = Mathf.Clamp(baseCount + index * 2, 10, 80);
            // Progressively replace weak ordinary infantry with armored elites,
            // Colossi and Wraiths from the ORIGINAL Profaned PawnKindDefs.
            List<Pawn> attackers = ProfanedEncounterUtility.SpawnSiegeWave(
                map, edge, enemy, count, index);
            if (attackers.Count > 0)
            {
                LordMaker.MakeNewLord(enemy,
                    new LordJob_AragornSiegeNoRetreat(),
                    map, attackers);
                // Messages never forcibly pause combat like ThreatBig letters.
                Messages.Message(
                    AragornText.F("AR_Forge_Wave", index + 1, attackers.Count, ProgressPercent),
                    forge, MessageTypeDefOf.ThreatBig);
                Log.Message("[Aragorn] Forge assault wave " + (index + 1) +
                    " spawned: " + attackers.Count + " Profaned.");
            }
        }

        public bool IsDivineGoldProperty(Thing thing)
        {
            if (thing == null || site == null || !site.HasMap || !thing.Spawned ||
                thing.Map != site.Map || !(thing is Building)) return false;
            return thing.Faction == site.Faction &&
                (thing.Stuff == ThingDefOf.Gold || thing == forge);
        }

        public void NotifyGoldPropertyDamaged(Thing property, Thing instigator)
        {
            if (!IsDivineGoldProperty(property) || instigator == null ||
                instigator.Faction != Faction.OfPlayer) return;
            int now = Find.TickManager.TicksGame;
            if (now - lastPropertyPenaltyTick < 2500) return;
            lastPropertyPenaltyTick = now;
            Faction divine = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(
                f => f != null && f.def == site.Faction.def && !f.Hidden && !f.defeated);
            divine = divine ?? site.Faction;
            divine.TryAffectGoodwillWith(Faction.OfPlayer, -20, canSendMessage: true,
                canSendHostilityLetter: true, reason: HistoryEventDefOf.AttackedBuilding,
                lookTarget: property);
            Messages.Message("Das Zerstören der goldenen Divine-Order-Schmiede gilt als Sakrileg! "
                + "Rufverlust bei der Divine Order (-20).", property,
                MessageTypeDefOf.NegativeEvent);
            Log.Warning("[Aragorn] Divine Order golden property attacked by player: " + property);
        }

        public bool CanWaitAtForge(Pawn pawn, out string reason)
        {
            reason = null;
            if (!GhostDefinitions.IsAragorn(pawn) || pawn.Faction != Faction.OfPlayer)
            { reason = "Nur Aragorn kann an der Schmiede wachen."; return false; }
            if (completed || quest?.State != QuestState.Ongoing)
            { reason = "Die Schmiedequest ist nicht aktiv."; return false; }
            if (forge == null || !forge.Spawned || !IsForgeMap(pawn.Map))
            { reason = "Aragorn muss sich auf der Schmiedekarte befinden."; return false; }
            if (!AragornStandCell.InBounds(pawn.Map) || !AragornStandCell.Standable(pawn.Map))
            { reason = AragornText.T("AR_Forge_GuardBlocked"); return false; }
            if (pawn.Downed) { reason = "Aragorn ist kampfunfähig."; return false; }
            return true;
        }

        // The blacksmith speaks when Aragorn first asks him to begin.
        // On subsequent interruptions, selecting the gizmo resumes instantly.
        public void SpeakToSmith(Pawn pawn)
        {
            if (!CanWaitAtForge(pawn, out string reason))
            {
                AragornGame.Reject(reason);
                return;
            }
            if (siegeStartTick >= 0)
            {
                StartGuarding(pawn);
                return;
            }
            Find.WindowStack.Add(new Dialog_MessageBox(
                AragornText.T("AR_Forge_SmithDialogue"),
                AragornText.T("AR_Forge_SmithBegin"),
                () => StartGuarding(pawn),
                AragornText.T("AR_Forge_SmithNotYet"),
                () => { },
                AragornText.T("AR_Forge_SmithDialogueTitle")));
        }

        public void StartGuarding(Pawn pawn)
        {
            if (!CanWaitAtForge(pawn, out string reason))
            { AragornGame.Reject(reason); return; }
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AR_WaitForAnduril");
            if (jobDef == null) { AragornGame.Reject("AR_WaitForAnduril JobDef fehlt."); return; }
            aragorn = pawn;
            if (pawn.drafter != null && pawn.drafter.Drafted) pawn.drafter.Drafted = false;
            pawn.jobs.StartJob(JobMaker.MakeJob(jobDef, forge, AragornStandCell),
                JobCondition.InterruptForced);
            Messages.Message(AragornText.F("AR_Forge_Guarding", ProgressPercent),
                pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void CompleteForge()
        {
            if (completed) return;
            GhostDefinitions.SetFixedWeapon(aragorn, "AR_Anduril");
            if (!GhostDefinitions.HasAnduril(aragorn))
            {
                Log.Error("[Aragorn] Reforging finished but Anduril could not be equipped.");
                return;
            }
            completed = true;
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Success, sendLetter: true);
            Find.LetterStack.ReceiveLetter(AragornText.T("AR_Forge_FinishedTitle"),
                AragornText.T("AR_Forge_FinishedBody"),
                LetterDefOf.PositiveEvent, aragorn);
            Log.Message("[Aragorn] Anduril reforged successfully; ghost trials unlocked.");
        }

        private void FailForge(string reason)
        {
            Log.Warning("[Aragorn] Anduril quest failed: " + reason);
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Fail, sendLetter: true);
            Messages.Message(AragornText.F("AR_Forge_Failed", reason),
                MessageTypeDefOf.NegativeEvent);
            quest = null;
            site = null;
            smith = null;
            forge = null;
            aragorn = null;
            built = false;
            offeredLetter = false;
            provisionalAndurilRemoved = false;
            progress = 0;
            waves = 0;
            siegeStartTick = -1;
            nextWaveTick = -1;
            retryTick = Find.TickManager.TicksGame + 2500;
            abandonedSinceTick = -1;
        }
    }

    // Unlike generic raids this siege has no retreat, timeout or theft transition.
    // Downed/dead attackers naturally drop out of the Lord without ordering others home.
    public class LordJob_AragornSiegeNoRetreat : LordJob
    {
        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.AddToil(new LordToil_AssaultColony());
            return graph;
        }
    }

    public class SitePartWorker_AndurilForge : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Current.Game?.GetComponent<AragornForgeGame>()?.PopulateForgeMap(map);
        }
    }

    public class JobDriver_AragornWaitForAnduril : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Wait(ForgeWaitLength, TargetIndex.A);
        }

        private const int ForgeWaitLength = 40000;
    }

    public class JobDriver_DivineOrderForgeAnduril : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Wait(40000, TargetIndex.A);
        }
    }

    public static class AndurilForgeBuilder
    {
        // All architecture is vanilla, with gold as real stuff. No graphics files.
        public static Thing Build(Map map, IntVec3 near, Faction owner)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef smithy = DefDatabase<ThingDef>.GetNamedSilentFail("FueledSmithy")
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("ElectricSmithy");
            ThingDef sarcophagus = DefDatabase<ThingDef>.GetNamedSilentFail("Sarcophagus");
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamedSilentFail("GoldTile")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("FineTile");
            if (wall == null || smithy == null) return null;

            IntVec3 center = CellFinder.RandomClosewalkCellNear(near, map, 20);
            const int rx = 12, rz = 10;
            // Grand gold hall with two side rooms and gaps in the ramparts.
            for (int dx = -rx; dx <= rx; dx++)
            for (int dz = -rz; dz <= rz; dz++)
            {
                IntVec3 c = center + new IntVec3(dx, 0, dz);
                if (!c.InBounds(map)) continue;
                // Remove all existing trees and bushes before applying floors.
                ClearVegetation(map, c);
                if (floor != null && c.Standable(map))
                {
                    try { map.terrainGrid.SetTerrain(c, floor); } catch { }
                }

                bool outside = Math.Abs(dx) == rx || Math.Abs(dz) == rz;
                bool innerWall = (Math.Abs(dx) == 7 && Math.Abs(dz) <= 6 &&
                    dz != -2 && dz != 2);
                bool columns = Math.Abs(dx) == 5 && (dz == 5 || dz == -5);
                if (!outside && !innerWall && !columns) continue;
                if (outside && (Math.Abs(dx) < 2 && Math.Abs(dz) == rz)) // main gates
                {
                    if (dx == 0 && door != null)
                        SpawnBuilding(door, ThingDefOf.Gold, c, map, owner);
                    continue;
                }
                // Broken gold walls on the western side: tempting salvage.
                if (dx == -rx && dz >= 2 && dz <= 4) continue;
                SpawnBuilding(wall, ThingDefOf.Gold, c, map, owner);
            }

            if (sarcophagus != null)
            {
                foreach (int dx in new[] { -9, 9 })
                foreach (int dz in new[] { -5, 0, 5 })
                    SpawnBuilding(sarcophagus, ThingDefOf.Gold,
                        center + new IntVec3(dx, 0, dz), map, owner);
            }
            // Forge in the NORTH of the hall, smith and Aragorn face it from south.
            IntVec3 forgePos = center + new IntVec3(0, 0, 5);
            Thing bench = SpawnBuilding(smithy, ThingDefOf.Gold, forgePos, map, owner);
            Log.Message("[Aragorn] Gold forge hall built (" + (2 * rx + 1)
                + " x " + (2 * rz + 1) + ") at " + center
                + "; workstation: " + smithy.defName + ".");
            return bench;
        }

        private static void ClearVegetation(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return;
            foreach (Thing thing in cell.GetThingList(map).ToList())
                if (thing.def.category == ThingCategory.Plant)
                    thing.Destroy(DestroyMode.Vanish);
        }

        private static Thing SpawnBuilding(ThingDef def, ThingDef stuff, IntVec3 cell, Map map, Faction owner)
        {
            if (def == null || !cell.InBounds(map)) return null;
            if (cell.GetThingList(map).Any(t => t is Building)) return null;
            try
            {
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff : null);
                ClearVegetation(map, cell);
                Thing placed = GenSpawn.Spawn(thing, cell, map, Rot4.North, WipeMode.Vanish);
                if (placed != null && owner != null && placed.def.CanHaveFaction)
                    placed.SetFaction(owner);
                return placed;
            }
            catch (Exception ex)
            {
                Log.WarningOnce("[Aragorn] Could not build gold architecture: " +
                    def.defName + " / " + ex.Message, 756710);
                return null;
            }
        }
    }
}
