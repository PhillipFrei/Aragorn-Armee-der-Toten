using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    public sealed class AragornMod : Mod
    {
        public static AragornSettings Config;
        public AragornMod(ModContentPack content) : base(content)
        {
            Config = GetSettings<AragornSettings>();
            new Harmony("aragorn.armyofthedead.patch").PatchAll();
        }

        public override string SettingsCategory() => AragornText.T("AR_Mod_Name");

        public override void DoSettingsWindowContents(Rect rect)
        {
            // The ModIcon lives at About/ModIcon.png and is shown by
            // RimWorld in the mod-options list, not beside these sliders.
            var ls = new Listing_Standard();
            ls.Begin(rect);
            ls.Label(AragornText.T("AR_Settings_SectionQuests"));
            ls.Label(AragornText.F("AR_Settings_QuestDanger", Config.questDangerMultiplier.ToString("0.00")));
            Config.questDangerMultiplier = Mathf.Round(
                ls.Slider(Config.questDangerMultiplier, .5f, 5f) * 4f) / 4f;
            ls.Label(AragornText.T("AR_Settings_DangerHelp"));
            ls.GapLine();

            ls.Label(AragornText.T("AR_Settings_SectionSpirits"));
            ls.Label(AragornText.F("AR_Settings_SummonDuration", Config.summonHours));
            Config.summonHours = Mathf.RoundToInt(ls.Slider(Config.summonHours, 6, 13));

            ls.Label(AragornText.F("AR_Settings_Armor", Config.ghostArmorMultiplier.ToString("0.00")));
            Config.ghostArmorMultiplier = Mathf.Round(
                ls.Slider(Config.ghostArmorMultiplier, .05f, 2f) * 20f) / 20f;
            ls.Label(AragornText.T("AR_Settings_ArmorHelp"));

            ls.Label(AragornText.F("AR_Settings_KingBuff", Config.kingBuff.ToString("0.0")));
            Config.kingBuff = 1.5f +
                Mathf.Round(ls.Slider((Config.kingBuff - 1.5f) * 2f, 0f, 3f)) * .5f;
            ls.GapLine();

            ls.Label(AragornText.T("AR_Settings_SectionAbilities"));
            ls.Label(AragornText.F("AR_Settings_Emp", Config.empDamage.ToString("0")));
            Config.empDamage = Mathf.Round(ls.Slider(Config.empDamage, 0f, 300f) / 10f) * 10f;
            ls.Label(AragornText.F("AR_Settings_Lightning", Config.kingLightningDamage.ToString("0")));
            Config.kingLightningDamage =
                Mathf.Round(ls.Slider(Config.kingLightningDamage, 5f, 100f) / 5f) * 5f;
            ls.GapLine();

            if (Prefs.DevMode)
            {
                ls.Label(AragornText.T("AR_Settings_SectionDev"));
                var rescue = Current.Game?.GetComponent<AragornRescueGame>();
                if (ls.ButtonText(AragornText.T("AR_Settings_OfferRescue")))
                    Messages.Message(rescue == null
                        ? AragornText.T("AR_Settings_NoQuestComponent")
                        : rescue.ForceOfferQuest(), MessageTypeDefOf.NeutralEvent);
                var forge = Current.Game?.GetComponent<AragornForgeGame>();
                if (forge != null && forge.Completed &&
                    ls.ButtonText(AragornText.T("AR_Settings_RetryForge")))
                    Messages.Message(forge.RestartForTesting(), MessageTypeDefOf.NeutralEvent);
            }
            ls.End();
        }
    }

    public sealed class AragornSettings : ModSettings
    {
        public float questDangerMultiplier = 2f;
        public int summonHours = 6;
        public int armorLevel = 10;
        public int profanedCount = 20; // legacy save setting; all trials now use a fixed baseline
        public float kingBuff = 2f;
        public float ghostArmorMultiplier = 1f;
        public float empDamage = 100f;
        public float kingLightningDamage = 20f;
        public float swordArmor = 1f;
        public float archerArmor = .75f;
        public float grenadeArmor = .85f;
        public float axeArmor = 1.10f;
        public float captainArmor = 1.40f;
        public float kingArmor = 1.60f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref questDangerMultiplier, "questDangerMultiplier", 2f);
            Scribe_Values.Look(ref summonHours, "summonHours", 6);
            Scribe_Values.Look(ref armorLevel, "armorLevel", 10);
            Scribe_Values.Look(ref profanedCount, "profanedCount", 20);
            Scribe_Values.Look(ref kingBuff, "kingBuff", 2f);
            // Persist a single armor multiplier; migrate the older level (1..20)
            // the first time a player loads the simplified v0.29 settings.
            Scribe_Values.Look(ref ghostArmorMultiplier, "ghostArmorMultiplier", -1f);
            if (Scribe.mode == LoadSaveMode.LoadingVars && ghostArmorMultiplier < 0f)
                ghostArmorMultiplier = Mathf.Clamp(
                    (armorLevel <= 10 ? 50f + (armorLevel - 1f) * (950f / 9f)
                                      : 1000f + (armorLevel - 10f) * 100f) / 1000f,
                    .05f, 2f);
            Scribe_Values.Look(ref empDamage, "empDamage", 100f);
            Scribe_Values.Look(ref kingLightningDamage, "kingLightningDamage", 20f);
            Scribe_Values.Look(ref swordArmor, "swordArmor", 1f);
            Scribe_Values.Look(ref archerArmor, "archerArmor", .75f);
            Scribe_Values.Look(ref grenadeArmor, "grenadeArmor", .85f);
            Scribe_Values.Look(ref axeArmor, "axeArmor", 1.1f);
            Scribe_Values.Look(ref captainArmor, "captainArmor", 1.4f);
            Scribe_Values.Look(ref kingArmor, "kingArmor", 1.6f);
            base.ExposeData();
        }

        public float BaseArmorPercent => 1000f * Mathf.Clamp(ghostArmorMultiplier, .05f, 2f);
    }
}
