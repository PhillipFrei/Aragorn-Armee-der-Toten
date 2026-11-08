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
            "AR_GhostSpiritSword01", "AR_GhostSpiritSword02", "AR_GhostSpiritSword03",
            "AR_GhostSpiritArcher01", "AR_GhostSpiritArcher02", "AR_GhostSpiritArcher03",
            "AR_GhostSpiritGrenadier01", "AR_GhostSpiritGrenadier02",
            "AR_GhostSpiritAxe01", "AR_GhostSpiritAxe02",
            "AR_GhostSpiritCaptain01", "AR_GhostSpiritCaptain02", "AR_GhostSpiritKing"
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
            string kind = pawn.kindDef.defName;
            if (kind.StartsWith("AR_GhostSpiritSword") || kind == "AR_GhostSword") return s.swordArmor;
            if (kind.StartsWith("AR_GhostSpiritArcher") || kind == "AR_GhostArcher") return s.archerArmor;
            if (kind.StartsWith("AR_GhostSpiritGrenadier") || kind == "AR_GhostGrenadier") return s.grenadeArmor;
            if (kind.StartsWith("AR_GhostSpiritAxe") || kind == "AR_GhostAxe") return s.axeArmor;
            if (kind.StartsWith("AR_GhostSpiritCaptain") || kind == "AR_GhostCaptain") return s.captainArmor;
            if (kind == "AR_GhostSpiritKing" || kind == "AR_GhostKing") return s.kingArmor;
            return 0f;
        }

        private static readonly HashSet<int> InitializedNaturalAttacks = new HashSet<int>();

        // Animal-like spirits cannot carry human equipment. The original weapon's
        // intrinsic Tool and VerbProperties are instead installed on the matching
        // spirit race. This keeps attack mechanics/projectile defs, but NO gear item.
        public static void ConfigureNaturalAttacks(int index)
        {
            if (index < 0 || index >= PawnKinds.Length || InitializedNaturalAttacks.Contains(index)) return;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(PawnKinds[index]);
            if (kind == null || kind.race == null) return;
            ThingDef weapon = ResolveWeapon(Weapons[index]);
            if (weapon == null)
            {
                Log.Warning("[Aragorn] Natural attack source missing: " + Weapons[index]);
                return;
            }
            if (weapon.tools != null && weapon.tools.Count > 0)
                kind.race.tools = new List<Tool>(weapon.tools);
            if (weapon.Verbs != null && weapon.Verbs.Count > 0)
            {
                var field = HarmonyLib.AccessTools.Field(typeof(ThingDef), "verbs");
                if (field != null) field.SetValue(kind.race, new List<VerbProperties>(weapon.Verbs));
            }
            InitializedNaturalAttacks.Add(index);
        }

        private static ThingDef ResolveWeapon(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null && defName == "MeleeWeapon_PsyfocusStaff")
                def = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(
                    item => item.weaponTags != null && item.weaponTags.Contains("EltexStaff"));
            return def;
        }

        // Worn apparel is intentionally never used for combat ghosts.
        public static void SetFixedWeapon(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.equipment == null) return;
            ThingDef def = ResolveWeapon(defName);
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
