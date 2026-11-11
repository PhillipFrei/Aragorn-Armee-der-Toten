using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // Also display the exact percentage when selecting the vanilla smithy.
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    internal static class ShowAndurilForgeInspectProgress
    {
        private static void Postfix(ThingWithComps __instance, ref string __result)
        {
            AragornForgeGame game = Current.Game?.GetComponent<AragornForgeGame>();
            if (game == null || game.ForgeBuilding != __instance ||
                !game.ShowForgeProgress(__instance.Map)) return;
            if (!string.IsNullOrEmpty(__result)) __result += "\n";
            __result += game.ForgeProgressLabel;
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    internal static class SuppressGhostNeeds
    {
        private static void Postfix(Pawn_NeedsTracker __instance, ref bool __result)
        {
            Pawn owner = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (GhostDefinitions.IsGhost(owner)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
    internal static class DenyGhostApparel
    {
        private static bool Prefix(Pawn_ApparelTracker __instance)
        {
            return !GhostDefinitions.IsGhost(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment))]
    internal static class DenyUnauthorizedEquipment
    {
        private static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq == null) return true;
            if (newEq.def.defName == "AR_Anduril" && !GhostDefinitions.IsAragorn(__instance.pawn)) return false;
            return !GhostDefinitions.IsGhost(__instance.pawn) || GhostRuntime.IsAssigningWeapon;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    internal static class GhostsNeverDropGear
    {
        private static bool Prefix(Pawn_EquipmentTracker __instance, ref bool __result, ref ThingWithComps resultingEq)
        {
            if (GhostDefinitions.IsGhost(__instance.pawn))
            {
                resultingEq = null;
                __result = false;
                return false;
            }
            // Prevent an accidental drop of Aragorn's unique Anduril on downing.
            if (GhostDefinitions.IsAragorn(__instance.pawn) &&
                __instance.Primary != null && __instance.Primary.def.defName == "AR_Anduril")
            {
                resultingEq = null;
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.DropAllEquipment))]
    internal static class NoGhostDeathLoot
    {
        private static bool Prefix(Pawn_EquipmentTracker __instance)
        {
            if (!GhostDefinitions.IsGhost(__instance.pawn)) return true;
            __instance.DestroyAllEquipment();
            return false;
        }
    }

    // v0.17 hid EVERY equipped weapon, including bows, axes, and launchers.
    // Only the first sword spirit has a sword baked into its body texture.
    // Keep that single legacy exception until the body art is sword-free;
    // all other ghosts must draw their actual weapon normally (aiming included).
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    internal static class DoNotDoubleDrawSpectralWeapons
    {
        private static bool Prefix(Pawn pawn)
        {
            return pawn == null || pawn.kindDef == null ||
                pawn.kindDef.defName != "AR_GhostSpiritSword01";
        }
    }

    // Gold rooms belong to the Divine Order. Ordinary deconstruction or
    // claiming is prohibited, but deliberate violent salvage remains possible
    // at a clear diplomacy cost (handled on damage below).
    [HarmonyPatch(typeof(Building), nameof(Building.DeconstructibleBy))]
    internal static class PreventDivineGoldDeconstruction
    {
        private static void Postfix(Building __instance, Faction faction,
            ref AcceptanceReport __result)
        {
            if (faction == Faction.OfPlayer &&
                Current.Game?.GetComponent<AragornForgeGame>()?.IsDivineGoldProperty(__instance) == true)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.ClaimableBy))]
    internal static class PreventDivineGoldClaiming
    {
        private static void Postfix(Building __instance, Faction by,
            ref AcceptanceReport __result)
        {
            if (by == Faction.OfPlayer &&
                Current.Game?.GetComponent<AragornForgeGame>()?.IsDivineGoldProperty(__instance) == true)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class PenalizeAttackOnDivineGold
    {
        private static void Prefix(Thing __instance, DamageInfo dinfo)
        {
            Current.Game?.GetComponent<AragornForgeGame>()?.NotifyGoldPropertyDamaged(
                __instance, dinfo.Instigator);
        }
    }

    // In RimWorld normal-speed simulation 60 ticks ~= one real second.
    // A dynamic label stays visible even while the command is disabled.
    internal sealed class AragornCooldownCommand : Command_Action
    {
        public Func<int> RemainingTicks;

        public override string TopRightLabel
        {
            get
            {
                int ticks = RemainingTicks == null ? 0 : Math.Max(0, RemainingTicks());
                return ticks > 0 ? Mathf.CeilToInt(ticks / 60f) + "s" : null;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class AragornAndGhostGizmos
    {
        private static Texture2D Icon(string name) =>
            ContentFinder<Texture2D>.Get("Aragorn/Gizmos/" + name, false);

        private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            IEnumerable<Gizmo> vanilla = __result;
            __result = Build(vanilla, __instance);
        }

        private static IEnumerable<Gizmo> Build(IEnumerable<Gizmo> vanilla, Pawn pawn)
        {
            var game = Current.Game == null ? null : Current.Game.GetComponent<AragornGame>();
            if (GhostDefinitions.IsGhost(pawn))
            {
                // Retain drafting, forced attack commands, equipment verbs and player modded combat commands.
                // No needs, clothing or inventory because the trackers have no such items.
                foreach (var gizmo in vanilla)
                {
                    if (gizmo is Command_Action && !(gizmo is Command_Toggle)) continue;
                    yield return gizmo;
                }
                // Only Aragorn can recall the army; ghosts never receive this gizmo.
                if (game != null && game.IsSummonedKing(pawn))
                {
                    var lightning = new AragornCooldownCommand {
                        defaultLabel = AragornText.T("AR_Gizmo_StormTitle"),
                        icon = Icon("KingsLightning"),
                        defaultDesc = AragornText.T("AR_Gizmo_StormDesc"),
                        action = () => game.TargetKingLightning(pawn),
                        RemainingTicks = () => game.LightningCooldownRemaining
                    };
                    if (game.LightningCooldownRemaining > 0)
                        lightning.Disable("Abklingzeit: " + Mathf.CeilToInt(game.LightningCooldownRemaining / 60f) + " Sekunden (bei normaler Geschwindigkeit).");
                    yield return lightning;
                }
                yield break;
            }

            foreach (var gizmo in vanilla) yield return gizmo;
            if (game == null || !GhostDefinitions.IsAragorn(pawn)) yield break;

            if (Prefs.DevMode && pawn.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action {
                    defaultLabel = "DEV: All Aragorn Quests Complete",
                    defaultDesc = "Development only: complete this mod's rescue, "
                        + "reforging and 13 spirit trials, equip Andúril, "
                        + "and allow immediate summoning. Does not complete unrelated quests.",
                    icon = Icon("DevComplete"),
                    action = () => game.DevCompleteAragornStory(pawn)
                };
                yield return new Command_Action {
                    defaultLabel = "DEV: Abklingzeiten zurücksetzen",
                    defaultDesc = "Setzt Beschwörung, Andúril-Rundumschlag und "
                        + "Geistersturm sofort zurück. Nur bei aktivem Entwicklermodus.",
                    icon = Icon("DevComplete"),
                    action = () => game.DevResetCooldowns(pawn)
                };
            }

            var forgeGame = Current.Game?.GetComponent<AragornForgeGame>();
            if (forgeGame != null && forgeGame.IsForgeMap(pawn.Map) && !forgeGame.Completed)
            {
                Command_Action forgeWatch = new Command_Action {
                    defaultLabel = AragornText.T("AR_Forge_GuardTitle"),
                    icon = Icon("GuardForge"),
                    defaultDesc = AragornText.T("AR_Forge_GuardDesc"),
                    action = () => forgeGame.SpeakToSmith(pawn)
                };
                if (!forgeGame.CanWaitAtForge(pawn, out string forgeReason))
                    forgeWatch.Disable(forgeReason);
                yield return forgeWatch;
            }

            // Keep recall visible even without the sword. A lost Anduril should
            // not strand previously summoned specters permanently.
            Command_Action recall = new Command_Action {
                defaultLabel = AragornText.T("AR_Gizmo_RecallTitle"),
                        icon = Icon("RecallGhosts"),
                defaultDesc = AragornText.T("AR_Gizmo_RecallDesc"),
                action = () => game.RecallAll()
            };
            if (!game.HasSummonedGhosts) recall.Disable(AragornText.T("AR_Gizmo_RecallNone"));
            yield return recall;

            if (!GhostDefinitions.HasAnduril(pawn))
            {
                // Once the forging quest has been completed, the old test repair
                // remains available in case a save loses the unique sword.
                if (forgeGame != null && forgeGame.Completed)
                    yield return new Command_Action {
                        defaultLabel = AragornText.T("AR_Gizmo_RestoreTitle"),
                        icon = Icon("BeginReforging"),
                        defaultDesc = AragornText.T("AR_Gizmo_RestoreDesc"),
                        action = () => GhostDefinitions.SetFixedWeapon(pawn, "AR_Anduril")
                    };
                yield break;
            }

            AragornCooldownCommand summon = new AragornCooldownCommand {
                defaultLabel = AragornText.T("AR_Gizmo_SummonTitle"),
                icon = Icon("SummonGhosts"),
                defaultDesc = AragornText.T("AR_Gizmo_SummonDesc"),
                action = () => game.Summon(pawn),
                RemainingTicks = () => game.SummonCooldownRemaining
            };
            if (!game.CanSummon(pawn, out string summoningReason)) summon.Disable(summoningReason);
            yield return summon;

            AragornCooldownCommand spin = new AragornCooldownCommand {
                defaultLabel = AragornText.T("AR_Gizmo_WhirlTitle"),
                icon = Icon("SwordWhirl"),
                defaultDesc = AragornText.T("AR_Gizmo_WhirlDesc"),
                action = () => game.StartSpin(pawn),
                RemainingTicks = () => game.SpinCooldownRemaining
            };
            if (game.SpinCooldownRemaining > 0)
                spin.Disable("Abklingzeit: " + Mathf.CeilToInt(game.SpinCooldownRemaining / 60f) + " Sekunden (bei normaler Geschwindigkeit).");
            yield return spin;

            Command_Action trial = new Command_Action {
                defaultLabel = AragornText.T("AR_Trial_GizmoTitle"),
                icon = Icon("GhostTrial"),
                defaultDesc = AragornText.T("AR_Trial_GizmoDesc"),
                action = () => game.StartTrial(pawn)
            };
            if (!game.CanStartTrial(pawn, out string trialReason)) trial.Disable(trialReason);
            yield return trial;
        }
    }
}
