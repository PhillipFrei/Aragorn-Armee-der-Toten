using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AragornArmy
{
    internal static class GhostDefinitions
    {
        public static readonly string[] PawnKinds = {
            "AR_GhostSword", "AR_GhostSword", "AR_GhostSword",
            "AR_GhostArcher", "AR_GhostArcher", "AR_GhostArcher",
            "AR_GhostGrenadier", "AR_GhostGrenadier",
            "AR_GhostAxe", "AR_GhostAxe",
            "AR_GhostCaptain", "AR_GhostCaptain", "AR_GhostKing"
        };

        public static readonly string[] Weapons = {
            "BotchJob_ProfanedScimitar", "BotchJob_ProfanedShotel", "BotchJob_ScourgeknightGreatsword",
            "BotchJob_SpectralGreatbow", "BotchJob_SpectralGreatbow", "BotchJob_SpectralGreatbow",
            "BotchJob_DivineOrderCleansingConcoction", "BotchJob_DivineOrderCleansingConcoction",
            "BotchJob_ScourgeknightGreataxe", "BotchJob_ScourgeknightGreataxe",
            "BotchJob_GraspingDeadGreatsword", "BotchJob_GraspingDeadGreatsword", "MeleeWeapon_PsyfocusStaff"
        };

        public static readonly string[] Names = {
            "Schwertgeist I", "Schwertgeist II", "Schwertgeist III",
            "Bogenschütze I", "Bogenschütze II", "Bogenschütze III",
            "Granatwerfer I", "Granatwerfer II",
            "Axtgeist I", "Axtgeist II",
            "Verfluchter Hauptmann I", "Verfluchter Hauptmann II", "König der Toten"
        };

        public static bool IsGhost(Pawn pawn) => pawn != null && pawn.kindDef != null &&
            pawn.kindDef.defName.StartsWith("AR_Ghost", StringComparison.Ordinal);

        public static bool IsAragorn(Pawn pawn) => pawn != null && pawn.kindDef != null &&
            pawn.kindDef.defName == "AR_Aragorn";

        public static bool HasAnduril(Pawn pawn) => IsAragorn(pawn) && pawn.equipment != null &&
            pawn.equipment.Primary != null && pawn.equipment.Primary.def.defName == "AR_Anduril";

        public static float ArmorMultiplier(Pawn pawn)
        {
            if (!IsGhost(pawn)) return 0f;
            var s = AragornMod.Config;
            switch (pawn.kindDef.defName)
            {
                case "AR_GhostSword": return s.swordArmor;
                case "AR_GhostArcher": return s.archerArmor;
                case "AR_GhostGrenadier": return s.grenadeArmor;
                case "AR_GhostAxe": return s.axeArmor;
                case "AR_GhostCaptain": return s.captainArmor;
                case "AR_GhostKing": return s.kingArmor;
                default: return 0f;
            }
        }

        // Worn apparel is intentionally never used for combat ghosts.
        public static void SetFixedWeapon(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.equipment == null) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null && defName == "MeleeWeapon_PsyfocusStaff")
            {
                // Runtime lookup by vanilla Royalty weapon tag tolerates an eventual DLC defName change.
                def = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(
                    item => item.weaponTags != null && item.weaponTags.Contains("EltexStaff"));
            }
            if (def == null)
            {
                Log.Error("[Aragorn] Waffendefinition nicht gefunden: " + defName);
                return;
            }
            var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null) as ThingWithComps;
            if (thing == null)
            {
                Log.Error("[Aragorn] Waffe nicht erzeugbar: " + defName);
                return;
            }
            GhostRuntime.IsAssigningWeapon = true;
            try
            {
                pawn.equipment.DestroyAllEquipment();
                pawn.equipment.AddEquipment(thing);
            }
            finally
            {
                GhostRuntime.IsAssigningWeapon = false;
            }
        }
    }

    internal static class GhostRuntime
    {
        // Used only during controlled initial equipment assignment.
        public static bool IsAssigningWeapon;
    }
}
