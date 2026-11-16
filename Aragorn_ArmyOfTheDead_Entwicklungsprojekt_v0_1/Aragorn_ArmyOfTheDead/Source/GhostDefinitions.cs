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

        // Shared class labels, internal PawnKind IDs stay unchanged for existing saves.
        private static readonly string[] NameKeys = {
            "AR_Role_Sword", "AR_Role_Sword", "AR_Role_Sword",
            "AR_Role_Archer", "AR_Role_Archer", "AR_Role_Archer",
            "AR_Role_Bombardier", "AR_Role_Bombardier",
            "AR_Role_Axe", "AR_Role_Axe",
            "AR_Role_Captain", "AR_Role_Captain", "AR_Role_King"
        };
        // Resolve on access so a language change never leaves a stale static array.
        public static string[] Names => NameKeys.Select(AragornText.T).ToArray();

        public static string RoleName(Pawn pawn)
        {
            if (!IsGhost(pawn)) return null;
            int index = Array.IndexOf(PawnKinds, pawn.kindDef.defName);
            if (index >= 0) return Names[index];
            string kind = pawn.kindDef.defName;
            if (kind == "AR_GhostSword") return AragornText.T("AR_Role_Sword");
            if (kind == "AR_GhostArcher") return AragornText.T("AR_Role_Archer");
            if (kind == "AR_GhostGrenadier") return AragornText.T("AR_Role_Bombardier");
            if (kind == "AR_GhostAxe") return AragornText.T("AR_Role_Axe");
            if (kind == "AR_GhostCaptain") return AragornText.T("AR_Role_Captain");
            if (kind == "AR_GhostKing") return AragornText.T("AR_Role_King");
            return null;
        }

        // Visual update v0.28: All 39 new character textures have NO painted weapons.
        // The only weapons rendered are the equipped ThingWithComps from
        // GhostDefinitions.EnsureGhostWeapon; no second weapon is embedded.
        public static bool HasEmbeddedWeapon(Pawn pawn) => false;
        public static bool HasEmbeddedWeapon(Pawn pawn, Rot4 facing) => false;

        // Migrate old auto-generated numbered names, but preserve manual renames.
        public static void NormalizeGeneratedName(Pawn pawn)
        {
            string role = RoleName(pawn);
            if (role == null) return;
            string current = pawn.Name?.ToStringShort ?? "";
            int index = Array.IndexOf(PawnKinds, pawn.kindDef.defName);
            string oldGerman = index < 0 ? role : new[] {
                "Schwertgeist", "Schwertgeist", "Schwertgeist",
                "Bogenschütze", "Bogenschütze", "Bogenschütze",
                "Bombardier", "Bombardier", "Axtgeist", "Axtgeist",
                "Verfluchter Hauptmann", "Verfluchter Hauptmann", "König der Toten"
            }[index];
            string oldEnglish = index < 0 ? role : new[] {
                "Spectral Swordsman", "Spectral Swordsman", "Spectral Swordsman",
                "Spectral Archer", "Spectral Archer", "Spectral Archer",
                "Bombardier", "Bombardier", "Spectral Axeman", "Spectral Axeman",
                "Cursed Captain", "Cursed Captain", "King of the Dead"
            }[index];
            bool generated = string.IsNullOrEmpty(current) || current == role ||
                current == oldGerman || current == oldEnglish;
            foreach (string known in new[] { role, oldGerman, oldEnglish })
                foreach (string numeral in new[] { " I", " II", " III" })
                    if (current == known + numeral) generated = true;
            if (generated)
            {
                if (current != role) pawn.Name = new NameSingle(role);
            }
        }

        public static bool IsGhost(Pawn pawn) => pawn != null && pawn.kindDef != null &&
            pawn.kindDef.defName.StartsWith("AR_Ghost", StringComparison.Ordinal);

        public static bool IsAragorn(Pawn pawn) => pawn != null && pawn.kindDef != null &&
            pawn.kindDef.defName == "AR_Aragorn";

        public static bool HasAnduril(Pawn pawn) => IsAragorn(pawn) && pawn.equipment != null &&
            pawn.equipment.Primary != null && pawn.equipment.Primary.def.defName == "AR_Anduril";

        // All 13 spirits use the same global armor slider (v0.29).
        // Older per-class settings are read for compatibility but no longer applied.
        public static float ArmorMultiplier(Pawn pawn) => IsGhost(pawn) ? 1f : 0f;

        // Gives a spectral ToolUser its immutable original weapon.
        // Also repairs already-summoned ghosts saved while the race was Animal.
        public static bool EnsureGhostWeapon(Pawn pawn)
        {
            if (!IsGhost(pawn) || pawn.kindDef == null) return false;
            int index = Array.IndexOf(PawnKinds, pawn.kindDef.defName);
            if (index < 0) return false; // legacy human kinds keep their existing gear

            if (!pawn.RaceProps.ToolUser)
            {
                Log.ErrorOnce("[Aragorn] Ghost race is not ToolUser: " + pawn.def.defName, 504021);
                return false;
            }
            if (pawn.equipment == null)
                PawnComponentsUtility.CreateInitialComponents(pawn);
            if (pawn.equipment == null)
            {
                Log.ErrorOnce("[Aragorn] Ghost equipment tracker not created for " + pawn.kindDef.defName, 504022);
                return false;
            }

            ThingDef weapon = ResolveWeapon(Weapons[index]);
            if (weapon == null)
            {
                Log.ErrorOnce("[Aragorn] Ghost weapon definition missing: " + Weapons[index], 504023 + index);
                return false;
            }
            if (pawn.equipment.Primary?.def == weapon) return true;
            SetFixedWeapon(pawn, Weapons[index]);
            if (pawn.equipment.Primary?.def != weapon)
            {
                Log.ErrorOnce("[Aragorn] Could not equip " + weapon.defName + " on " + pawn.kindDef.defName,
                    504041 + index);
                return false;
            }
            return true;
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
                // Immediately refresh visuals after the developer/quest sword swap.
                if (pawn.Drawer?.renderer != null)
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
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
