using System;
using System.Text;
using RimWorld;
using Verse;

namespace AragornArmy
{
    // Displays what the king changes on THIS ghost, including the resulting
    // numeric stats. No fabricated change to the health capacity "Moving".
    internal static class KingBuffDetails
    {
        public static bool Active(Pawn pawn)
        {
            if (!GhostDefinitions.IsGhost(pawn) || pawn.health == null ||
                GhostDefinitions.RoleName(pawn) == "König der Toten") return false;
            HediffDef status = DefDatabase<HediffDef>.GetNamedSilentFail("AR_KinglyCommand");
            return status != null && pawn.health.hediffSet.HasHediff(status);
        }

        public static string Build(Pawn pawn)
        {
            if (pawn == null) return "";
            if (!Active(pawn)) return "Königlicher Befehl: inaktiv";
            float factor = Math.Max(1.01f, AragornMod.Config?.kingBuff ?? 2f);
            float speed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            float speedWithoutKing = speed / factor;
            float speedBonusPercent = (factor - 1f) * 100f;
            float timeReductionPercent = (1f - 1f / factor) * 100f;

            var result = new StringBuilder();
            result.AppendLine("Königlicher Befehl (aktiv):");
            result.Append("• Laufgeschwindigkeit: ")
                .Append(speedWithoutKing.ToString("0.00")).Append(" → ")
                .Append(speed.ToString("0.00")).Append(" Felder/s (+")
                .Append(speedBonusPercent.ToString("0")).AppendLine(" %)");
            AppendTimeFactor(result, pawn, "Nahkampf-Abklingzeit",
                "MeleeCooldownFactor", factor, timeReductionPercent);
            AppendTimeFactor(result, pawn, "Fernkampf-Abklingzeit",
                "RangedCooldownFactor", factor, timeReductionPercent);
            AppendTimeFactor(result, pawn, "Ziel-/Visierzeit",
                "AimingDelayFactor", factor, timeReductionPercent);
            result.Append("Werte vor dem Pfeil: berechnet ohne Königsbuff, ")
                .Append("mit allen anderen aktuellen Boni.");
            return result.ToString();
        }

        private static void AppendTimeFactor(StringBuilder result, Pawn pawn,
            string label, string statName, float kingFactor, float reductionPercent)
        {
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statName);
            if (stat == null)
            {
                result.Append("• ").Append(label)
                    .AppendLine(": Stat in dieser Modliste nicht verfügbar");
                return;
            }
            float current = pawn.GetStatValue(stat);
            float withoutKing = current * kingFactor;
            result.Append("• ").Append(label).Append(": Faktor ")
                .Append(withoutKing.ToString("0.00")).Append(" → ")
                .Append(current.ToString("0.00")).Append(" (−")
                .Append(reductionPercent.ToString("0")).AppendLine(" %)");
        }
    }
}