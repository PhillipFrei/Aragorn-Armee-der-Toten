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
        public const int ForgeTicksRequired = 10000; // 4 in-game hours while Aragorn guards the smith.
        private Quest quest;
        private Site site;
        private Pawn smith;
        private Thing forge;
        private Pawn aragorn;
        private bool completed;
        private bool offeredLetter;
        private bool built;
        private int progress;
        private int waves;
        private int retryTick;
        private readonly float[] waveFractions = { 0f, .24f, .50f, .76f, .91f };

        public AragornForgeGame(Game game) { }
        public bool Completed => completed;
        public int ProgressPercent => Mathf.Clamp(Mathf.RoundToInt(progress * 100f / ForgeTicksRequired), 0, 100);
        public bool IsForgeMap(Map map) => site != null && site.HasMap && site.Map == map && forge != null;

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
            Scribe_Values.Look(ref progress, "arForgeProgress");
            Scribe_Values.Look(ref waves, "arForgeWaves");
            Scribe_Values.Look(ref retryTick, "arForgeRetry");
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
            // Old developer builds handed Anduril to Aragorn immediately.
            // Take that provisional sword away so the reforging can be played in existing saves.
            if (hero.equipment?.Primary?.def?.defName == "AR_Anduril")
                hero.equipment.DestroyAllEquipment();

            Quest q = Quest.MakeRaw();
            q.root = root;
            q.name = "Andúril – Die Klinge neu schmieden";
            q.description = "Ein Meisterschmied der Divine Order bewahrt die Bruchstücke von Narsil. "
                + "Reise mit Aragorn zur goldenen Schmiedehalle. Während der Meister die Klinge neu schmiedet, "
                + "muss Aragorn an seiner Schmiede Wache halten. Deine übrigen Kämpfer verteidigen den Ort "
                + "gegen immer größere Wellen der Profaned Legion. Sobald Andúril fertig ist, "
                + "nimm das Schwert und verlasse das Gelände. Die goldenen Bauteile sind echte Materialien "
                + "und könnten zerlegt werden – wenn du während des Angriffs Zeit dafür findest. "
                + "\n\nKein Zeitlimit für die Annahme.";
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

            if (site == null && Find.TickManager.TicksGame >= retryTick)
            {
                CreateForgeSite();
                return;
            }
            if (site == null || !site.HasMap || !built) return;
            if (smith == null || smith.Dead || smith.Destroyed || forge == null || forge.Destroyed)
            {
                FailForge("Der Meisterschmied oder die Schmiede wurde zerstört.");
                return;
            }
            if (aragorn == null || aragorn.Dead || aragorn.Destroyed)
            {
                aragorn = FindSavedAragorn();
                if (aragorn == null) return; // waiting for the player to arrive with Aragorn
            }
            if (!aragorn.Spawned || aragorn.Map != site.Map) return;
            if (aragorn.Position.DistanceTo(forge.Position) > 4.5f)
                return; // stops while Aragorn is away, fleeing or fighting elsewhere
            if (smith.Downed || aragorn.Downed) return;

            // The smith is an actual Divine Order NPC, but progress is scripted
            // independently of pawn crafting skills or any workstation bill.
            progress += 60;
            if (progress % 360 == 0)
                FleckMaker.ThrowDustPuffThick(forge.DrawPos, site.Map, 1.2f,
                    new Color(1f, .68f, .20f));
            while (waves < waveFractions.Length &&
                progress >= ForgeTicksRequired * waveFractions[waves])
                SpawnWave();

            if (progress >= ForgeTicksRequired)
                CompleteForge();
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
                    threatPoints: StorytellerUtility.DefaultSiteThreatPointsNow());
                found.customLabel = "Goldene Schmiede der Divine Order";
                Find.WorldObjects.Add(found);
                site = found;
                QuestPart_AragornRescueTarget target = quest.GetFirstPartOfType<QuestPart_AragornRescueTarget>();
                if (target != null) target.site = found;
                Find.LetterStack.ReceiveLetter("Die Klinge wartet",
                    "Die goldene Schmiedehalle der Divine Order ist auf der Weltkarte markiert. "
                    + "Reise mit Aragorn und deinen Verteidigern dorthin.",
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
                forge = AndurilForgeBuilder.Build(map, map.Center);
                if (forge == null)
                {
                    Log.Error("[Aragorn] Forge building not created. No encounter started.");
                    return;
                }
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("BotchJob_DivineOrderCommoner");
                if (kind == null) { Log.Error("[Aragorn] Divine Order Commoner PawnKind missing."); return; }
                Faction divine = site.Faction;
                smith = PawnGenerator.GeneratePawn(kind, divine);
                smith.Name = new NameSingle("Meisterschmied der Divine Order");
                IntVec3 smithCell = CellFinder.RandomClosewalkCellNear(forge.Position, map, 3);
                GenSpawn.Spawn(smith, smithCell, map);
                // Prevent an unarmed smith from being drafted into the combat AI.
                // He remains physically present and can be killed during the siege.
                built = true;
                Messages.Message("Die Divine Order wartet auf Aragorn. Wähle Aragorn "
                    + "und klicke auf 'An der Schmiede wachen'.",
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

            int baseCount = AragornMod.Config == null ? 20 : AragornMod.Config.profanedCount;
            int count = Mathf.Clamp(Mathf.RoundToInt(baseCount * (.65f + index * .20f)), 5, 50);
            List<Pawn> attackers = ProfanedEncounterUtility.SpawnGuards(map, edge, enemy, count);
            if (attackers.Count > 0)
            {
                LordMaker.MakeNewLord(enemy,
                    new LordJob_AssaultColony(enemy, canKidnap: false,
                        canTimeoutOrFlee: false, canSteal: false),
                    map, attackers);
                Find.LetterStack.ReceiveLetter("Profaned-Welle " + (index + 1),
                    "Die Profaned Legion greift die goldene Schmiede an! "
                    + attackers.Count + " Feinde nähern sich. Andúril: "
                    + ProgressPercent + " % geschmiedet.",
                    LetterDefOf.ThreatBig, forge);
                Log.Message("[Aragorn] Forge assault wave " + (index + 1) +
                    " spawned: " + attackers.Count + " Profaned.");
            }
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
            if (pawn.Downed) { reason = "Aragorn ist kampfunfähig."; return false; }
            return true;
        }

        public void StartGuarding(Pawn pawn)
        {
            if (!CanWaitAtForge(pawn, out string reason))
            { AragornGame.Reject(reason); return; }
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AR_WaitForAnduril");
            if (jobDef == null) { AragornGame.Reject("AR_WaitForAnduril JobDef fehlt."); return; }
            aragorn = pawn;
            if (pawn.drafter != null && pawn.drafter.Drafted) pawn.drafter.Drafted = false;
            pawn.jobs.StartJob(JobMaker.MakeJob(jobDef, forge), JobCondition.InterruptForced);
            Messages.Message("Aragorn wacht beim Schmied. Die Fertigung dauert insgesamt 4 Ingame-Stunden, "
                + "solange Aragorn in der Nähe bleibt. Fortschritt: "
                + ProgressPercent + " %.", pawn, MessageTypeDefOf.PositiveEvent);
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
            Find.LetterStack.ReceiveLetter("Andúril ist neu geschmiedet!",
                "Der Schmied der Divine Order hat die Bruchstücke von Narsil "
                + "zur Klinge Andúril vereint. Aragorn trägt sie nun. "
                + "Die Geisterprüfungen sind freigeschaltet. "
                + "Verlasse die goldene Schmiede mit deinen Überlebenden – "
                + "die Profaned können noch auf der Karte sein!",
                LetterDefOf.PositiveEvent, aragorn);
            Log.Message("[Aragorn] Anduril reforged successfully; ghost trials unlocked.");
        }

        private void FailForge(string reason)
        {
            Log.Warning("[Aragorn] Anduril quest failed: " + reason);
            if (quest != null && !quest.Historical)
                quest.End(QuestEndOutcome.Fail, sendLetter: true);
            Messages.Message("Die Schmiedequest scheiterte: " + reason
                + " Ein neuer Versuch wird später angeboten.",
                MessageTypeDefOf.NegativeEvent);
            quest = null;
            site = null;
            smith = null;
            forge = null;
            aragorn = null;
            built = false;
            offeredLetter = false;
            progress = 0;
            waves = 0;
            retryTick = Find.TickManager.TicksGame + 2500;
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
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(ForgeWaitLength, TargetIndex.A);
        }

        private const int ForgeWaitLength = 25000;
    }

    public static class AndurilForgeBuilder
    {
        // All architecture is vanilla, with gold as real stuff. No graphics files.
        public static Thing Build(Map map, IntVec3 near)
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
                        SpawnBuilding(door, ThingDefOf.Gold, c, map);
                    continue;
                }
                // Broken gold walls on the western side: tempting salvage.
                if (dx == -rx && dz >= 2 && dz <= 4) continue;
                SpawnBuilding(wall, ThingDefOf.Gold, c, map);
            }

            if (sarcophagus != null)
            {
                foreach (int dx in new[] { -9, 9 })
                foreach (int dz in new[] { -5, 0, 5 })
                    SpawnBuilding(sarcophagus, ThingDefOf.Gold,
                        center + new IntVec3(dx, 0, dz), map);
            }
            IntVec3 forgePos = center + new IntVec3(0, 0, 2);
            Thing bench = SpawnBuilding(smithy, ThingDefOf.Gold, forgePos, map);
            Log.Message("[Aragorn] Gold forge hall built (" + (2 * rx + 1)
                + " x " + (2 * rz + 1) + ") at " + center
                + "; workstation: " + smithy.defName + ".");
            return bench;
        }

        private static Thing SpawnBuilding(ThingDef def, ThingDef stuff, IntVec3 cell, Map map)
        {
            if (def == null || !cell.InBounds(map)) return null;
            if (cell.GetThingList(map).Any(t => t is Building)) return null;
            try
            {
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff : null);
                return GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
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
