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

        public override string SettingsCategory() => "Aragorn – Armee der Toten";

        public override void DoSettingsWindowContents(Rect rect)
        {
            // Reuse the existing king's turquoise icon, no extra artwork needed.
            Texture2D kingPortrait = ContentFinder<Texture2D>.Get("Aragorn/Gizmos/KingsLightning", false);
            if (kingPortrait != null)
                GUI.DrawTexture(new Rect(rect.xMax - 112f, rect.y + 6f, 104f, 104f),
                    kingPortrait, ScaleMode.ScaleToFit);

            Rect left = new Rect(rect.x, rect.y, Mathf.Max(260f, rect.width - 120f), rect.height);
            var ls = new Listing_Standard();
            ls.Begin(left);
            ls.Label("QUESTS");
            ls.Label("Globale Questgefahr: x" + Config.questDangerMultiplier.ToString("0.00") +
                " (alle Aragorn-Quests, maximal 100 Gegner gleichzeitig)");
            Config.questDangerMultiplier = Mathf.Round(
                ls.Slider(Config.questDangerMultiplier, .5f, 5f) * 4f) / 4f;
            ls.Label("Beeinflusst Rettung, Schmiedewellen, Geisterprüfungen und Mechanoidenfestung.");
            ls.GapLine();

            ls.Label("GEISTER");
            ls.Label("Beschwörungsdauer: " + Config.summonHours +
                " Ingame-Stunden (13 Stunden Abklingzeit)");
            Config.summonHours = Mathf.RoundToInt(ls.Slider(Config.summonHours, 6, 13));

            ls.Label("Geisterrüstung (alle Klassen): x" + Config.ghostArmorMultiplier.ToString("0.00"));
            Config.ghostArmorMultiplier = Mathf.Round(
                ls.Slider(Config.ghostArmorMultiplier, .05f, 2f) * 20f) / 20f;
            ls.Label("Bei x1: +1000 % natürliche Rüstung zu den Basiswerten. " +
                "Ersetzt die früheren Klassenregler und die Rüstungsstufen.");

            ls.Label("Königsbuff: x" + Config.kingBuff.ToString("0.0") +
                " (Bewegung schneller, Angriff/Visierzeit kürzer)");
            Config.kingBuff = 1.5f +
                Mathf.Round(ls.Slider((Config.kingBuff - 1.5f) * 2f, 0f, 3f)) * .5f;
            ls.GapLine();

            ls.Label("FÄHIGKEITEN");
            ls.Label("Aragorns EMP-Sturm: " + Config.empDamage.ToString("0") +
                " Schaden pro Mechanoid (+ 10 s Betäubung)");
            Config.empDamage = Mathf.Round(ls.Slider(Config.empDamage, 0f, 300f) / 10f) * 10f;
            ls.Label("Blitz des Königs: " + Config.kingLightningDamage.ToString("0") +
                " Brandschaden pro Treffer (5 Blitze)");
            Config.kingLightningDamage =
                Mathf.Round(ls.Slider(Config.kingLightningDamage, 5f, 100f) / 5f) * 5f;
            ls.GapLine();

            if (Prefs.DevMode)
            {
                ls.Label("ENTWICKLERMODUS");
                var rescue = Current.Game?.GetComponent<AragornRescueGame>();
                if (ls.ButtonText("Aragorn-Rettungsquest jetzt anbieten"))
                    Messages.Message(rescue == null
                        ? "Keine Quest-Komponente: Spielstand laden."
                        : rescue.ForceOfferQuest(), MessageTypeDefOf.NeutralEvent);
                var forge = Current.Game?.GetComponent<AragornForgeGame>();
                if (forge != null && forge.Completed &&
                    ls.ButtonText("Andúril-Schmiedequest erneut testen"))
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
