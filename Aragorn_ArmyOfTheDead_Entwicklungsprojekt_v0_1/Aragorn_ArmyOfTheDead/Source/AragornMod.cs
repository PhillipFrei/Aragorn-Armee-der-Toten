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
            var ls = new Listing_Standard();
            ls.Begin(rect);
            ls.Label("Aragorn-Rettungsquest (Test)");
            var rescue = Current.Game?.GetComponent<AragornRescueGame>();
            ls.Label(rescue == null
                ? "Status: Noch kein Spielstand geladen / Quest-Komponente fehlt."
                : "Status: " + rescue.DiagnosticStatus);
            if (ls.ButtonText("Aragorn-Rettungsquest JETZT erstellen / anzeigen"))
            {
                string status = rescue == null
                    ? "Keine Quest-Komponente: Ist dieser Mod aktiv und ein Spielstand geladen?"
                    : rescue.ForceOfferQuest();
                Messages.Message(status, MessageTypeDefOf.NeutralEvent);
            }
            ls.GapLine();
            ls.Label("Beschwörungsdauer: " + Config.summonHours + " Ingame-Stunden (Cooldown: 13 h)");
            Config.summonHours = Mathf.RoundToInt(ls.Slider(Config.summonHours, 6, 13));
            ls.Label("Rüstungsstufe: " + Config.armorLevel + " / 20   (Stufe 1: 50 %, 10: 1000 %, 20: 2000 %)");
            Config.armorLevel = Mathf.RoundToInt(ls.Slider(Config.armorLevel, 1, 20));
            ls.Label("Profaned-Verstärkung pro Geisterprüfung: " + Config.profanedCount);
            Config.profanedCount = Mathf.RoundToInt(ls.Slider(Config.profanedCount, 5, 50));
            ls.Label("Goldschmiede: 12 Stunden, alle 1 Stunde eine Profaned-Welle");
            ls.Label("Goldschmiede: Basiszahl pro Welle " + Config.forgeWaveBaseCount +
                " (spätere Wellen: bis zu 22 weitere + viele Elitegegner)");
            Config.forgeWaveBaseCount = Mathf.RoundToInt(ls.Slider(Config.forgeWaveBaseCount, 12, 50));
            ls.Label("Königsbuff: x" + Config.kingBuff.ToString("0.0") + " (Geister: Bewegung und Angriffstempo)");
            Config.kingBuff = 1.5f + Mathf.Round(ls.Slider((Config.kingBuff - 1.5f) * 2f, 0f, 3f)) * .5f;
            ls.GapLine();
            ls.Label("Rüstung pro Klasse: Multiplikator zur globalen Rüstungsstufe");
            RoleSlider(ls, "Schwertgeister", ref Config.swordArmor);
            RoleSlider(ls, "Bogenschützen", ref Config.archerArmor);
            RoleSlider(ls, "Granatwerfer", ref Config.grenadeArmor);
            RoleSlider(ls, "Axtgeister", ref Config.axeArmor);
            RoleSlider(ls, "Hauptleute", ref Config.captainArmor);
            RoleSlider(ls, "König", ref Config.kingArmor);
            ls.End();
        }

        private static void RoleSlider(Listing_Standard ls, string name, ref float factor)
        {
            ls.Label(name + ": x" + factor.ToString("0.00"));
            factor = Mathf.Round(ls.Slider(factor, .25f, 3f) * 20f) / 20f;
        }
    }

    public sealed class AragornSettings : ModSettings
    {
        public int summonHours = 6;
        public int armorLevel = 10;
        public int profanedCount = 20;
        public int forgeWaveBaseCount = 26;
        public float kingBuff = 2f;
        public float swordArmor = 1f;
        public float archerArmor = .75f;
        public float grenadeArmor = .85f;
        public float axeArmor = 1.10f;
        public float captainArmor = 1.40f;
        public float kingArmor = 1.60f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref summonHours, "summonHours", 6);
            Scribe_Values.Look(ref armorLevel, "armorLevel", 10);
            Scribe_Values.Look(ref profanedCount, "profanedCount", 20);
            Scribe_Values.Look(ref forgeWaveBaseCount, "forgeWaveBaseCount", 26);
            Scribe_Values.Look(ref kingBuff, "kingBuff", 2f);
            Scribe_Values.Look(ref swordArmor, "swordArmor", 1f);
            Scribe_Values.Look(ref archerArmor, "archerArmor", .75f);
            Scribe_Values.Look(ref grenadeArmor, "grenadeArmor", .85f);
            Scribe_Values.Look(ref axeArmor, "axeArmor", 1.1f);
            Scribe_Values.Look(ref captainArmor, "captainArmor", 1.4f);
            Scribe_Values.Look(ref kingArmor, "kingArmor", 1.6f);
            base.ExposeData();
        }

        public float BaseArmorPercent
        {
            get
            {
                // Precisely meets the three agreed anchor points.
                return armorLevel <= 10 ? 50f + (armorLevel - 1) * (950f / 9f)
                                       : 1000f + (armorLevel - 10) * 100f;
            }
        }
    }
}
