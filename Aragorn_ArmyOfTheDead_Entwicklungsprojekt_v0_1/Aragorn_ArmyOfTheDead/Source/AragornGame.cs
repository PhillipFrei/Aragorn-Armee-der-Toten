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
    // Saved once per RimWorld save. Trial encounters take place in separate world-map crypts.
    public sealed class AragornGame : GameComponent
    {
        public AragornGame(Game game) { }
        public int Unlocked = 0;
        private int lastSummonTick = -1000000;
        private int summonedUntil = -1;
        private int lastKingLightning = -1000000;
        private int lastSpinTick = -1000000;
        private List<Pawn> ghosts = new List<Pawn>();
        private List<int> initializedAragornIds = new List<int>();
        private Pawn trialBoss;
        private Site trialSite;
        private Quest trialQuest;
        private List<Pawn> trialGuards = new List<Pawn>();
        private Pawn trialAragorn; // Save the original, sword-bearing quest participant.
        private Map trialMap;
        private int trialIndex = -1;
        private bool cleanUpInProgress;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Unlocked, "unlocked", 0);
            Scribe_Values.Look(ref lastSummonTick, "lastSummonTick", -1000000);
            Scribe_Values.Look(ref summonedUntil, "summonedUntil", -1);
            Scribe_Values.Look(ref lastKingLightning, "lastKingLightning", -1000000);
            Scribe_Values.Look(ref lastSpinTick, "lastSpinTick", -1000000);
            Scribe_Values.Look(ref trialIndex, "trialIndex", -1);
            Scribe_Collections.Look(ref ghosts, "ghosts", LookMode.Reference);
            Scribe_Collections.Look(ref initializedAragornIds, "initializedAragornIds", LookMode.Value);
            Scribe_References.Look(ref trialBoss, "trialBoss");
            Scribe_References.Look(ref trialSite, "trialSite");
            Scribe_References.Look(ref trialQuest, "trialQuest");
            Scribe_Collections.Look(ref trialGuards, "trialGuards", LookMode.Reference);
            Scribe_References.Look(ref trialAragorn, "trialAragorn");
            Scribe_References.Look(ref trialMap, "trialMap");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ghosts == null) ghosts = new List<Pawn>();
                if (trialGuards == null) trialGuards = new List<Pawn>();
                if (initializedAragornIds == null) initializedAragornIds = new List<int>();
            }
        }

        private static int Ticks => Find.TickManager.TicksGame;
        private static bool IsPlayerGhost(Pawn pawn) =>
            pawn != null && !pawn.Destroyed && pawn.Faction == Faction.OfPlayer
            && GhostDefinitions.IsGhost(pawn);

        // Recover previously summoned specters even when an old save or an
        // earlier Summon() call lost them from the saved reference list.
        private List<Pawn> CollectPlayerGhosts()
        {
            var found = new HashSet<Pawn>();
            foreach (Pawn ghost in ghosts)
                if (IsPlayerGhost(ghost)) found.Add(ghost);
            if (Find.Maps != null)
                foreach (Map map in Find.Maps)
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                        if (IsPlayerGhost(pawn)) found.Add(pawn);
            return found.ToList();
        }

        private bool AnySummoned => CollectPlayerGhosts().Any(p => p.Spawned && !p.Dead);
        public bool HasSummonedGhosts => CollectPlayerGhosts().Any(p => p.Spawned || p.Corpse != null);
        public bool IsSummonedKing(Pawn pawn) => IsSummoned(pawn) && (pawn.kindDef.defName == "AR_GhostSpiritKing" || pawn.kindDef.defName == "AR_GhostKing") && pawn.Spawned && !pawn.Dead && !pawn.Downed;
        public bool IsSummoned(Pawn pawn) => IsPlayerGhost(pawn) && (ghosts.Contains(pawn) || pawn.Spawned);
        public int SummonCooldownRemaining => Math.Max(0, lastSummonTick + 32500 - Ticks);
        public int LightningCooldownRemaining => Math.Max(0, lastKingLightning + 7500 - Ticks);
        public int SpinCooldownRemaining => Math.Max(0, lastSpinTick + 1500 - Ticks); // Prototype: 0.6 in-game hours.

        public override void GameComponentTick()
        {
            if (Ticks % 60 != 0) return;
            if (AragornMod.Config == null) return;

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    // Captive quest Aragorn is prepared only AFTER joining the player.
                    // This avoids handing Anduril to the prisoner's hostile captors.
                    if (IsPlayerGhost(pawn) && !ghosts.Contains(pawn))
                    {
                        ghosts.Add(pawn);
                        Log.Message("[Aragorn] Recovered orphaned summoned spirit: " + pawn.LabelShortCap);
                    }
                    if (GhostDefinitions.IsAragorn(pawn) && pawn.Faction == Faction.OfPlayer)
                    {
                        if (!initializedAragornIds.Contains(pawn.thingIDNumber))
                        {
                            initializedAragornIds.Add(pawn.thingIDNumber);
                            PrepareAragorn(pawn);
                        }
                        else if (Ticks % 600 == 0)
                        {
                            // Repairs older saved Aragorns as well: traits were not deterministic in v0.1.
                            EnsureAragornTraits(pawn);
                        }
                    }
                }
            }

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Pawn ghost = ghosts[i];
                if (ghost == null || ghost.Destroyed || !ghost.Spawned || ghost.Dead || ghost.Downed)
                {
                    RemoveGhost(ghost);
                    ghosts.RemoveAt(i);
                    continue;
                }
                // ToolUser spirits carry their fixed weapon, including survivors
                // from v0.10 whose equipment tracker was still absent.
                GhostDefinitions.EnsureGhostWeapon(ghost);
                // Always drafted: combat commands only; no work schedule/hauling.
                if (ghost.drafter != null && !ghost.drafter.Drafted) ghost.drafter.Drafted = true;
                if (ghost.needs != null) ghost.needs.AddOrRemoveNeedsAsAppropriate();
                // Temporary ghost-blue aura particle. Full art/render layer is the next milestone.
                if (Ticks % 180 == 0)
                    FleckMaker.ThrowDustPuffThick(ghost.DrawPos, ghost.Map, .55f, new Color(.16f, .91f, .83f, .65f));
            }
            if (summonedUntil > 0 && Ticks >= summonedUntil)
            {
                RecallAll();
            }
            RefreshKinglyCommand();
            CheckTrialOutcome();
            AutoCastBossKing();
        }

        public bool CanSummon(Pawn pawn, out string reason)
        {
            reason = null;
            if (!GhostDefinitions.HasAnduril(pawn) || pawn == null || !pawn.Spawned)
            {
                reason = "Aragorn muss Andúril tragen.";
                return false;
            }
            if (Unlocked == 0) { reason = "Erst eine Geisterprüfung bestehen."; return false; }
            if (AnySummoned) { reason = "Die Armee ist bereits beschworen."; return false; }
            if (SummonCooldownRemaining > 0)
            {
                reason = "Beschwörung wieder in " + (SummonCooldownRemaining / 2500f).ToString("0.0") + " Ingame-Stunden möglich.";
                return false;
            }
            return true;
        }

        public void Summon(Pawn aragorn)
        {
            if (!CanSummon(aragorn, out string reason)) { Reject(reason); return; }
            // Never erase references to existing ghosts; otherwise they become
            // unrecallable and subsequent waves keep accumulating on the map.
            if (CollectPlayerGhosts().Any(p => p.Spawned && !p.Dead))
            {
                Reject("Erst die bereits beschworenen Geister zurückrufen.");
                return;
            }
            ghosts.RemoveAll(p => p == null || p.Destroyed || p.Dead || !p.Spawned);
            for (int i = 0; i < Unlocked && i < 13; i++)
            {
                Pawn ghost = CreateGhost(i, Faction.OfPlayer);
                if (ghost == null) continue;
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(aragorn.Position, aragorn.Map, 7);
                GenSpawn.Spawn(ghost, loc, aragorn.Map);
                if (ghost.drafter != null) ghost.drafter.Drafted = true;
                ghosts.Add(ghost);
            }
            lastSummonTick = Ticks;
            summonedUntil = Ticks + AragornMod.Config.summonHours * 2500;
            RefreshKinglyCommand();
            Messages.Message("Armee der Toten: " + ghosts.Count + " Geister beschworen.", MessageTypeDefOf.PositiveEvent);
        }

        public void RecallAll()
        {
            if (cleanUpInProgress) return;
            cleanUpInProgress = true;
            try
            {
                var targets = CollectPlayerGhosts();
                foreach (Pawn ghost in targets)
                {
                    try { RemoveGhost(ghost); }
                    catch (Exception ex)
                    {
                        Log.Error("[Aragorn] Rückruf fehlgeschlagen für " + ghost?.LabelShortCap + ": " + ex);
                    }
                }
                ghosts.Clear();
                summonedUntil = -1;
                Log.Message("[Aragorn] Rückruf: " + targets.Count + " Spieler-Geister erfasst.");
            }
            finally { cleanUpInProgress = false; }
            Messages.Message("Die Armee der Toten ist zurückgerufen.", MessageTypeDefOf.NeutralEvent);
        }

        private static void RemoveGhost(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.equipment != null) pawn.equipment.DestroyAllEquipment();
            if (pawn.Corpse != null && !pawn.Corpse.Destroyed) pawn.Corpse.Destroy(DestroyMode.Vanish);
            else if (!pawn.Destroyed && pawn.Spawned) pawn.Destroy(DestroyMode.Vanish);
        }

        public static Pawn CreateGhost(int i, Faction faction)
        {
            if (i < 0 || i >= GhostDefinitions.PawnKinds.Length) return null;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(GhostDefinitions.PawnKinds[i]);
            if (kind == null) { Log.Error("[Aragorn] Missing PawnKind for index " + i); return null; }
            // Generate a combat-only ToolUser without human backgrounds, apparel,
            // equipment, relationships, ideology, addictions or age traits.
            var request = new PawnGenerationRequest(kind, faction,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                allowPregnant: false, allowFood: false, allowAddictions: false,
                forceNoIdeo: true, forceNoBackstory: true, forbidAnyTitle: true,
                forceBaselinerChance: 1f, dontGiveWeapon: true,
                maximumAgeTraits: 0, minimumAgeTraits: 0, forceNoGear: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.Name = new NameSingle(GhostDefinitions.Names[i]);
            if (pawn.story != null)
            {
                // forceNoBackstory was already supplied to PawnGenerator.
                // Do NOT assign Childhood = null: RimWorld 1.6's Childhood setter
                // dereferences value.spawnCategories and throws NullReferenceException.
                pawn.story.title = null;
                if (pawn.story.traits != null)
                    foreach (Trait trait in pawn.story.traits.allTraits.ToList())
                        if (trait.sourceGene == null) pawn.story.traits.RemoveTrait(trait);
            }
            if (pawn.apparel != null)
                foreach (Apparel app in pawn.apparel.WornApparel.ToList())
                    app.Destroy(DestroyMode.Vanish);
            // No randomly generated bionic parts, wounds or implants.
            if (pawn.health != null) pawn.health.Reset();
            if (pawn.genes != null)
                foreach (Gene gene in pawn.genes.GenesListForReading.ToList()) pawn.genes.RemoveGene(gene);
            if (pawn.story != null)
            {
                pawn.story.skinColorOverride = new Color(.25f, .85f, .77f);
                pawn.story.HairColor = new Color(.38f, .92f, .84f);
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            if (pawn.needs != null) pawn.needs.AddOrRemoveNeedsAsAppropriate();
            // ToolUser is non-humanlike but has a real weapon tracker.
            // The original weapon is permanently locked via Harmony patches.
            GhostDefinitions.EnsureGhostWeapon(pawn);
            return pawn;
        }

        private void RefreshKinglyCommand()
        {
            HediffDef status = DefDatabase<HediffDef>.GetNamedSilentFail("AR_KinglyCommand");
            if (status == null) return;
            Pawn king = ghosts.FirstOrDefault(IsSummonedKing);
            foreach (Pawn g in ghosts)
            {
                if (g == null || g.health == null || (g.kindDef.defName == "AR_GhostSpiritKing" || g.kindDef.defName == "AR_GhostKing")) continue;
                bool shouldHave = king != null && g.Spawned && g.Map == king.Map && !g.Dead && !g.Downed;
                bool has = g.health.hediffSet.HasHediff(status);
                if (shouldHave && !has) g.health.AddHediff(status);
                else if (!shouldHave && has) g.health.RemoveHediff(g.health.hediffSet.GetFirstHediffOfDef(status));
            }
        }

        public bool CanStartTrial(Pawn aragorn, out string reason)
        {
            reason = null;
            if (!GhostDefinitions.HasAnduril(aragorn)) { reason = "Aragorn benötigt Andúril."; return false; }
            if (Unlocked >= 13) { reason = "Alle 13 Geister sind freigeschaltet."; return false; }
            if (trialIndex >= 0)
            { reason = "Eine Prüfung läuft bereits. Reise zum Geistergrab auf der Weltkarte."; return false; }
            return true;
        }

        // The gizmo now opens a real world-map trial; no hostiles spawn on home maps.
        public void StartTrial(Pawn aragorn)
        {
            if (!CanStartTrial(aragorn, out string reason)) { Reject(reason); return; }
            Faction hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null)
            {
                Reject("Die Profaned-Kampfdefinitionen konnten nicht geladen werden.");
                return;
            }
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail("AR_GhostTrialCamp");
            QuestScriptDef root = DefDatabase<QuestScriptDef>.GetNamedSilentFail("AR_GhostTrialQuest");
            if (part == null || root == null)
            {
                Reject("Geisterprüfung: Weltkarten- oder Questdefinition fehlt. Siehe Player.log.");
                Log.Error("[Aragorn] Missing AR_GhostTrialCamp or AR_GhostTrialQuest.");
                return;
            }
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, minDist: 5, maxDist: 20)
                && !TileFinder.TryFindNewSiteTile(out tile, minDist: 2, maxDist: 35))
            {
                Reject("Kein freies Weltkartenfeld für die nächste Geisterkrypta gefunden.");
                return;
            }
            try
            {
                Site site = SiteMaker.MakeSite(part, tile, hostile,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: StorytellerUtility.DefaultSiteThreatPointsNow());
                site.customLabel = "Geisterkrypta " + (Unlocked + 1) + " – " + GhostDefinitions.Names[Unlocked];
                Find.WorldObjects.Add(site);

                Quest quest = Quest.MakeRaw();
                quest.root = root;
                quest.name = "Geisterprüfung " + (Unlocked + 1) + "/13 – " + GhostDefinitions.Names[Unlocked];
                quest.description = "Aragorn spürt das Grab eines gefesselten Geisterkriegers. "
                    + "Reise mit Aragorn und Andúril zur markierten Krypta, "
                    + "besiege den verfluchten Geist und seine Profaned-Wachen. "
                    + "Die Seele wird danach für Aragorns Armee freigeschaltet. "
                    + "The Profaned muss geladen sein, braucht aber keine Weltkarten-Siedlung.";
                quest.acceptanceExpireTick = -1;
                quest.SetNotYetAccepted();
                quest.AddPart(new QuestPart_AragornRescueTarget { site = site });
                Find.QuestManager.Add(quest);
                quest.Accept(aragorn);

                trialIndex = Unlocked;
                trialSite = site;
                trialQuest = quest;
                trialAragorn = aragorn;
                trialMap = null;
                trialBoss = null;
                trialGuards.Clear();
                Find.LetterStack.ReceiveLetter(
                    "Neue Geisterkrypta entdeckt",
                    "Prüfung " + (Unlocked + 1) + "/13: " + GhostDefinitions.Names[Unlocked]
                    + ". Reise mit Aragorn zur Krypta auf der Weltkarte.",
                    LetterDefOf.NeutralEvent, site);
                Log.Message("[Aragorn] Trial " + trialIndex + " created at world tile " + tile);
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Geisterkrypta konnte nicht erstellt werden: " + ex);
                Reject("Geisterprüfung konnte nicht erstellt werden. Siehe Player.log.");
            }
        }

        public void PopulateTrialMap(Map map)
        {
            if (map == null || trialSite == null || trialSite != map.Parent
                || trialIndex < 0 || trialBoss != null) return;
            Faction hostile = ProfanedEncounterUtility.GetOrCreateFaction();
            if (hostile == null) { Log.Error("[Aragorn] No Profaned faction for trial map."); return; }
            try
            {
                IntVec3 crypt = AragornCryptBuilder.Build(map, map.Center);
                Pawn boss = CreateGhost(trialIndex, hostile);
                if (boss == null) { Log.Error("[Aragorn] Trial boss could not be generated."); return; }
                IntVec3 location = CellFinder.RandomClosewalkCellNear(crypt, map, 4);
                GenSpawn.Spawn(boss, location, map);
                int count = AragornMod.Config == null ? 20 : AragornMod.Config.profanedCount;
                var guards = ProfanedEncounterUtility.SpawnGuards(map, crypt, hostile, count);
                if (guards.Count == 0)
                {
                    Log.Error("[Aragorn] Trial site guard generation failed.");
                    return;
                }
                trialBoss = boss;
                trialMap = map;
                trialGuards = guards;
                var defenders = new List<Pawn>(guards) { boss };
                LordMaker.MakeNewLord(hostile,
                    new LordJob_DefendPoint(crypt, wanderRadius: 8f,
                        defendRadius: 30f, isCaravanSendable: false, addFleeToil: false),
                    map, defenders);
                Messages.Message("Geisterprüfung " + (trialIndex + 1)
                    + "/13: Besiege den Geisterboss und alle Profaned-Wachen.",
                    boss, MessageTypeDefOf.ThreatBig);
                Log.Message("[Aragorn] Trial site populated: " + (trialIndex + 1)
                    + "/13; " + guards.Count + " Profaned guards.");
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Geisterprüfung-Karte konnte nicht aufgebaut werden: " + ex);
            }
        }

        private void CheckTrialOutcome()
        {
            if (trialBoss == null || trialIndex < 0) return;
            if (!trialBoss.Dead && !trialBoss.Downed) return;
            if (trialGuards.Any(g => g != null && g.Spawned && !g.Dead && !g.Downed))
                return;
            // Knocked-down Aragorn still counts if he started armed and stays alive on the map.
            bool validAragorn = trialMap != null && trialAragorn != null &&
                trialAragorn.Spawned && !trialAragorn.Dead && trialAragorn.Map == trialMap;
            if (validAragorn && trialIndex == Unlocked)
            {
                Unlocked++;
                if (trialQuest != null && !trialQuest.Historical)
                    trialQuest.End(QuestEndOutcome.Success, sendLetter: true);
                Messages.Message("Geist freigeschaltet: " + GhostDefinitions.Names[trialIndex] + " (" + Unlocked + "/13).", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                if (trialQuest != null && !trialQuest.Historical)
                    trialQuest.End(QuestEndOutcome.Fail, sendLetter: true);
                Messages.Message("Prüfung nicht bestanden: Aragorn muss lebend auf der Kryptenkarte sein.", MessageTypeDefOf.RejectInput);
            }
            trialBoss = null;
            trialMap = null;
            trialAragorn = null;
            trialSite = null;
            trialQuest = null;
            trialGuards.Clear();
            trialIndex = -1;
        }

        public void StartSpin(Pawn aragorn)
        {
            if (!GhostDefinitions.HasAnduril(aragorn) || aragorn.Map == null) return;
            if (SpinCooldownRemaining > 0) { Reject("Rundumschlag noch auf Abklingzeit."); return; }
            lastSpinTick = Ticks;
            var origin = aragorn.Position;
            foreach (Pawn victim in aragorn.Map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (victim == aragorn || !victim.Spawned || victim.Dead || !victim.HostileTo(aragorn)) continue;
                if (victim.Position.DistanceTo(origin) > 3f) continue;
                victim.TakeDamage(new DamageInfo(DamageDefOf.Cut, 60f, 1.5f, -1f, aragorn));
                // Safe positional knockback; an occupied cell is not moved into.
                if (victim.Spawned && !victim.Dead)
                {
                    IntVec3 delta = victim.Position - origin;
                    IntVec3 push = victim.Position + new IntVec3(Math.Sign(delta.x), 0, Math.Sign(delta.z));
                    if (push.InBounds(victim.Map) && push.Walkable(victim.Map) && !push.GetThingList(victim.Map).Any(t => t is Pawn))
                        victim.Position = push;
                }
            }
            FleckMaker.ThrowDustPuffThick(aragorn.DrawPos, aragorn.Map, 3f, new Color(.30f, .86f, .80f));
        }

        public void TargetKingLightning(Pawn king)
        {
            if (!IsSummonedKing(king)) return;
            if (LightningCooldownRemaining > 0) { Reject("Geistersturm noch auf Abklingzeit."); return; }
            var targetParams = new TargetingParameters { canTargetLocations = true, canTargetPawns = true, canTargetBuildings = false };
            Find.Targeter.BeginTargeting(targetParams, target =>
            {
                if (target.IsValid && target.Cell.InBounds(king.Map)) CastKingLightning(king, target.Cell);
            }, target =>
            {
                GenDraw.DrawRadiusRing(king.Position, 15f);
                if (target.IsValid && target.Cell.InBounds(king.Map)) GenDraw.DrawRadiusRing(target.Cell, 6f);
            }, target => target.IsValid && target.Cell.InBounds(king.Map) &&
                         target.Cell.DistanceTo(king.Position) <= 15f, king);
        }

        private void AutoCastBossKing()
        {
            if (trialBoss == null || trialIndex != 12 || !trialBoss.Spawned || trialBoss.Dead || trialBoss.Downed) return;
            if (LightningCooldownRemaining > 0) return;
            Pawn target = trialBoss.Map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.Spawned && !p.Dead && p.HostileTo(trialBoss) && p.Position.DistanceTo(trialBoss.Position) <= 15f);
            if (target != null) CastKingLightning(trialBoss, target.Position);
        }

        private void CastKingLightning(Pawn king, IntVec3 center)
        {
            if (king == null || !king.Spawned || king.Map == null || king.Dead) return;
            if (LightningCooldownRemaining > 0 || center.DistanceTo(king.Position) > 15f) return;
            lastKingLightning = Ticks;
            var possible = king.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.Spawned && !p.Dead && p != king && p.HostileTo(king) && p.Position.DistanceTo(center) <= 6f).ToList();
            // Five visual strikes; only enemies can be selected as damage targets.
            for (int i = 0; i < 5; i++)
            {
                possible.RemoveAll(p => p == null || !p.Spawned || p.Dead);
                Pawn victim = possible.Count > 0 ? possible.RandomElement() : null;
                IntVec3 hitCell = center;
                if (victim != null) hitCell = victim.Position;
                else
                {
                    // Empty cells still receive a visible harmless bolt.
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        IntVec3 test = center + new IntVec3(Rand.RangeInclusive(-6, 6), 0, Rand.RangeInclusive(-6, 6));
                        if (test.InBounds(king.Map) && test.DistanceTo(center) <= 6f) { hitCell = test; break; }
                    }
                }
                FleckMaker.ThrowLightningGlow(hitCell.ToVector3Shifted(), king.Map, 1.5f);
                FleckMaker.ThrowDustPuffThick(hitCell.ToVector3Shifted(), king.Map, 1.3f, new Color(.12f, .95f, .86f));
                if (victim != null) victim.TakeDamage(new DamageInfo(DamageDefOf.Burn, 20f, 3f, -1f, king));
            }
        }

        public static void Reject(string text)
        {
            Messages.Message(text, MessageTypeDefOf.RejectInput);
        }

        private static readonly string[] AragornGenes = {
            "WoundHealing_Fast", "Immunity_Strong", "Superclotting", "DarkVision",
            "StrongStomach", "RobustDigestion", "MinTemp_SmallDecrease", "MeleeDamage_Strong", "Pain_Reduced",
            "AptitudeTerrible_Plants", "AptitudeTerrible_Construction", "AptitudeTerrible_Crafting", "AptitudePoor_Artistic"
        };
        private static readonly (string defName, int degree)[] AragornTraits = {
            ("SpeedOffset", 2), ("FastLearner", 0), ("NaturalMood", 1), ("Beauty", 2)
        };

        private static void PrepareAragorn(Pawn pawn)
        {
            pawn.Name = new NameSingle("Aragorn");
            if (pawn.genes != null)
                foreach (string name in AragornGenes)
                {
                    GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                    if (gene != null && !pawn.genes.GenesListForReading.Any(g => g.def == gene)) pawn.genes.AddGene(gene, xenogene: true);
                }
            EnsureAragornTraits(pawn);
            if (pawn.apparel != null)
            {
                WearAragorn(pawn, "BotchJob_CrusaderArmor");
                WearAragorn(pawn, "BotchJob_DivineOrderTunic");
            }
            // Development bootstrap: Aragorn receives Andúril immediately.
            // Replace with the rescue / sword quest reward during the quest milestone.
            GhostDefinitions.SetFixedWeapon(pawn, "AR_Anduril");
            Messages.Message("Aragorn ist bereit. Andúril enthält Beschwörung und lokale Testprüfungen.", MessageTypeDefOf.PositiveEvent);
        }

        private static void EnsureAragornTraits(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null) return;
            TraitSet set = pawn.story.traits;
            foreach (Trait existing in set.allTraits.ToList())
            {
                bool desired = AragornTraits.Any(t => t.defName == existing.def.defName && t.degree == existing.Degree);
                if (!desired && existing.sourceGene == null) set.RemoveTrait(existing, unsuppressConflicts: true);
            }
            foreach (var entry in AragornTraits)
            {
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(entry.defName);
                if (def != null && !set.HasTrait(def, entry.degree))
                    set.GainTrait(new Trait(def, entry.degree), suppressConflicts: true);
            }
            if (pawn.skills != null)
            {
                SkillRecord melee = pawn.skills.GetSkill(SkillDefOf.Melee);
                SkillRecord shooting = pawn.skills.GetSkill(SkillDefOf.Shooting);
                melee.Level = Math.Max(18, melee.Level);
                shooting.Level = Math.Max(18, shooting.Level);
                melee.passion = Passion.Major;
                shooting.passion = Passion.Major;
            }
        }

        private static void WearAragorn(Pawn pawn, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            Apparel ap = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null) as Apparel;
            if (ap != null) pawn.apparel.Wear(ap);
        }
    }
}
