using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AragornArmy
{
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

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class AragornAndGhostGizmos
    {
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
                // Every selected summoned ghost can recall the entire army.
                // This is useful if Aragorn is off-map or temporarily loses Anduril.
                if (game != null && game.IsSummoned(pawn))
                    yield return new Command_Action {
                        defaultLabel = "Geister zurückrufen",
                        defaultDesc = "Entfernt alle beschworenen Spieler-Geister auf allen geladenen Karten, auch verlorene Geister aus älteren Spielständen.",
                        action = () => game.RecallAll()
                    };
                if (game != null && game.IsSummonedKing(pawn))
                {
                    var lightning = new Command_Action {
                        defaultLabel = "Geistersturm",
                        defaultDesc = "5 türkise Blitze mit je 20 Schaden und 300 % Durchdringung in Radius 6. Zielpunkt in 15 Feldern. 3 Ingame-Stunden Abklingzeit.",
                        action = () => game.TargetKingLightning(pawn)
                    };
                    if (game.LightningCooldownRemaining > 0)
                        lightning.Disable("Noch " + (game.LightningCooldownRemaining / 2500f).ToString("0.0") + " Ingame-Stunden Abklingzeit.");
                    yield return lightning;
                }
                yield break;
            }

            foreach (var gizmo in vanilla) yield return gizmo;
            if (game == null || !GhostDefinitions.IsAragorn(pawn)) yield break;

            // Keep recall visible even without the sword. A lost Anduril should
            // not strand previously summoned specters permanently.
            Command_Action recall = new Command_Action {
                defaultLabel = "Geister zurückrufen",
                defaultDesc = "Ruft alle beschworenen Geister auf allen geladenen Karten sofort zurück (inklusive verwaister Geister aus älteren Versionen).",
                action = () => game.RecallAll()
            };
            if (!game.HasSummonedGhosts) recall.Disable("Zurzeit keine Geister beschworen.");
            yield return recall;

            if (!GhostDefinitions.HasAnduril(pawn))
            {
                // Repair tool for old v0.1 saves in which the sword was dropped.
                yield return new Command_Action {
                    defaultLabel = "Andúril wiederherstellen (Test)",
                    defaultDesc = "Entwicklungswerkzeug für alte Testspielstände: Andúril anlegen, falls die Waffe verlorenging.",
                    action = () => GhostDefinitions.SetFixedWeapon(pawn, "AR_Anduril")
                };
                yield break;
            }

            Command_Action summon = new Command_Action {
                defaultLabel = "Geister beschwören",
                defaultDesc = "Beschwört alle freigeschalteten Geister. 6–13 Stunden Dauer, 13 Stunden Cooldown.",
                action = () => game.Summon(pawn)
            };
            if (!game.CanSummon(pawn, out string summoningReason)) summon.Disable(summoningReason);
            yield return summon;

            Command_Action spin = new Command_Action {
                defaultLabel = "Andúril – Rundumschlag",
                defaultDesc = "60 Schaden, 150 % Durchdringung, Radius 3 und ein leichter Rückstoß. Prototyp-Cooldown: 1500 Ticks.",
                action = () => game.StartSpin(pawn)
            };
            if (game.SpinCooldownRemaining > 0) spin.Disable("Rundumschlag noch auf Abklingzeit.");
            yield return spin;

            Command_Action trial = new Command_Action {
                defaultLabel = "Nächste Geisterkrypta entdecken",
                defaultDesc = "Startet die nächste der 13 Weltkartenprüfungen. Reise mit Aragorn zur Krypta, besiege den Geisterboss und die Profaned-Legion, um den Geist freizuschalten.",
                action = () => game.StartTrial(pawn)
            };
            if (!game.CanStartTrial(pawn, out string trialReason)) trial.Disable(trialReason);
            yield return trial;
        }
    }
}
