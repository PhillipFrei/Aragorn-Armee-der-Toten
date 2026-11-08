using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AragornArmy
{
    // Saved once per RimWorld save. For this development version, trials take place on the active map.
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
            Scribe_References.Look(ref trialAragorn, "trialAragorn");
            Scribe_References.Look(ref trialMap, "trialMap");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ghosts == null) ghosts = new List<Pawn>();
                if (initializedAragornIds == null) initializedAragornIds = new List<int>();
            }
        }

        private static int Ticks => Find.TickManager.TicksGame;
        private bool AnySummoned => ghosts.Any(p => p != null && p.Spawned && !p.Dead && !p.Downed);
        public bool IsSummonedKing(Pawn pawn) => pawn != null && pawn.kindDef.defName == "AR_GhostKing" && ghosts.Contains(pawn) && pawn.Spawned && !pawn.Dead && !pawn.Downed;
        public bool IsSummoned(Pawn pawn) => pawn != null && ghosts.Contains(pawn);
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
                    if (GhostDefinitions.IsAragorn(pawn))
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
            // Every summon produces fresh Pawns and fresh original-mod weapons.
            ghosts.Clear();
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
                foreach (Pawn ghost in ghosts.ToList()) RemoveGhost(ghost);
                ghosts.Clear();
                summonedUntil = -1;
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
            // Generate a combat-only pawn without random backgrounds, apparel,
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
            GhostDefinitions.SetFixedWeapon(pawn, GhostDefinitions.Weapons[i]);
            return pawn;
        }

        private void RefreshKinglyCommand()
        {
            HediffDef status = DefDatabase<HediffDef>.GetNamedSilentFail("AR_KinglyCommand");
            if (status == null) return;
            Pawn king = ghosts.FirstOrDefault(IsSummonedKing);
            foreach (Pawn g in ghosts)
            {
                if (g == null || g.health == null || g.kindDef.defName == "AR_GhostKing") continue;
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
            if (trialBoss != null && !trialBoss.Dead && !trialBoss.Downed && trialBoss.Spawned)
            { reason = "Eine Prüfung läuft bereits."; return false; }
            return true;
        }

        // Local-map combat prototype. World-map quests, rescue camp and crypt layouts are NOT yet part of this build.
        public void StartTrial(Pawn aragorn)
        {
            if (!CanStartTrial(aragorn, out string reason)) { Reject(reason); return; }
            // The Profaned mod supplies its faction definition and its PawnGroupMaker,
            // but a Profaned world faction is NOT necessary. It might never have spawned
            // or have been eliminated earlier in this save.
            var factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("BotchJob_ProfanedFaction");
            if (factionDef == null)
            {
                Reject("Profaned-Fraktionsdefinition fehlt. Ist The Profaned geladen?");
                Log.Error("[Aragorn] Missing BotchJob_ProfanedFaction def. Trial requires the loaded mod, not its world faction.");
                return;
            }
            // A guaranteed hostile, hidden vanilla faction provides ownership and AI.
            // The combatants are still generated from the REAL Profaned PawnKindDefs and
            // the Profaned PawnGroupMaker weights; we do not alter world factions or diplomacy.
            Faction hostile = Faction.OfAncientsHostile;
            if (hostile == null)
            {
                Reject("Keine feindliche Test-Fraktion verfügbar (AncientsHostile).");
                return;
            }
            // Prepare the boss before saving a trial state. A generation failure
            // must not leave an invisible or irrecoverably half-started trial.
            Pawn boss;
            try
            {
                boss = CreateGhost(Unlocked, hostile);
            }
            catch (Exception ex)
            {
                Log.Error("[Aragorn] Fehler beim Erzeugen des Prüfungsgeistes " + (Unlocked + 1) + ": " + ex);
                Reject("Geisterprüfung konnte nicht gestartet werden. Details im Player.log unter [Aragorn].");
                return;
            }
            if (boss == null)
            {
                Reject("Geisterprüfung konnte nicht gestartet werden: Geisterdefinition fehlt.");
                return;
            }
            trialIndex = Unlocked;
            trialMap = aragorn.Map;
            trialAragorn = aragorn; // Anduril was equipped when starting the fight.
            trialBoss = boss;
            IntVec3 location = CellFinder.RandomClosewalkCellNear(aragorn.Position, aragorn.Map, 24);
            GenSpawn.Spawn(trialBoss, location, aragorn.Map);
            List<Pawn> attackers = new List<Pawn> { trialBoss };
            var group = factionDef.pawnGroupMakers.FirstOrDefault(x => x.kindDef == PawnGroupKindDefOf.Combat);
            var valid = group == null ? new List<PawnGenOption>() : group.options.Where(o => o.kind != null && o.selectionWeight > 0).ToList();
            for (int i = 0; i < AragornMod.Config.profanedCount && valid.Count > 0; i++)
            {
                PawnGenOption option = valid.RandomElementByWeight(o => o.selectionWeight);
                try
                {
                    Pawn extra = PawnGenerator.GeneratePawn(option.kind, hostile);
                    IntVec3 point = CellFinder.RandomClosewalkCellNear(location, aragorn.Map, 9);
                    GenSpawn.Spawn(extra, point, aragorn.Map);
                    attackers.Add(extra);
                }
                catch (Exception ex) { Log.Warning("[Aragorn] Profaned cannot spawn: " + option.kind + " / " + ex.Message); }
            }
            LordMaker.MakeNewLord(hostile, new LordJob_AssaultColony(hostile, canKidnap: false, canTimeoutOrFlee: false, canSteal: false), aragorn.Map, attackers);
            Messages.Message("Prüfung " + (trialIndex + 1) + "/13: " + GhostDefinitions.Names[trialIndex] + ". Aragorn muss mit Andúril auf dieser Karte bleiben.", MessageTypeDefOf.ThreatBig);
        }

        private void CheckTrialOutcome()
        {
            if (trialBoss == null || trialIndex < 0) return;
            if (!trialBoss.Dead && !trialBoss.Downed) return;
            // Knocked-down Aragorn still counts if he started armed and stays alive on the map.
            bool validAragorn = trialMap != null && trialAragorn != null &&
                trialAragorn.Spawned && !trialAragorn.Dead && trialAragorn.Map == trialMap;
            if (validAragorn && trialIndex == Unlocked)
            {
                Unlocked++;
                Messages.Message("Geist freigeschaltet: " + GhostDefinitions.Names[trialIndex] + " (" + Unlocked + "/13).", MessageTypeDefOf.PositiveEvent);
            }
            else
                Messages.Message("Prüfung nicht bestanden: Aragorn muss mit Andúril auf der Karte sein.", MessageTypeDefOf.RejectInput);
            trialBoss = null;
            trialMap = null;
            trialAragorn = null;
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
