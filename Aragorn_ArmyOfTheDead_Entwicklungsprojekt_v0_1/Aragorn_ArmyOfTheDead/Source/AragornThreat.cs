using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // Only our quest enemies; vanilla storyteller raids are untouched.
    public static class AragornThreat
    {
        public const int MaxEnemies = 100;

        private static float SettlementPoints()
        {
            try
            {
                Map home = Find.Maps?.Where(m => m != null && m.IsPlayerHome)
                    .OrderByDescending(m => m.wealthWatcher?.WealthTotal ?? 0f)
                    .FirstOrDefault();
                if (home != null)
                    return StorytellerUtility.DefaultThreatPointsNow(home);
                return StorytellerUtility.DefaultThreatPointsNow(Find.World);
            }
            catch (Exception ex)
            {
                Log.WarningOnce("[Aragorn] Could not measure settlement raid points: " + ex.Message, 921461);
                return 1000f;
            }
        }

        public static float ThreatFactor(float questBase)
        {
            float difficulty = AragornMod.Config?.questDangerMultiplier ?? 2f;
            // StorytellerUtility already includes settlement wealth and
            // difficulty threat scale; normalize to a stable reference budget.
            float wealthAndStoryteller = Mathf.Clamp(
                Mathf.Sqrt(Mathf.Max(100f, SettlementPoints()) / 1000f), .5f, 4f);
            // The minimum slider setting must ALWAYS lower encounter numbers.
            // At x0.5, high colony wealth cannot negate the low-danger choice.
            // Wealth/storyteller influence increases smoothly with the slider:
            // 0.5x = half the baseline, 2x = ~one-third of full wealth scaling,
            // 5x = full storyteller-and-wealth scaling.
            float wealthWeight = Mathf.InverseLerp(.5f, 5f, difficulty);
            float adjustedWealth = Mathf.Lerp(1f, wealthAndStoryteller, wealthWeight);
            return Mathf.Clamp(difficulty * questBase * adjustedWealth, .1f, 20f);
        }

        public static int CountAliveQuestHostiles(Map map, Faction faction)
        {
            if (map == null || faction == null) return 0;
            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p != null && !p.Dead && !p.Downed && p.Faction == faction);
        }

        public static int ScaleCount(Map map, Faction faction, int baseCount, float questBase)
        {
            int remaining = Math.Max(0, MaxEnemies - CountAliveQuestHostiles(map, faction));
            return Math.Min(remaining, Math.Max(0,
                Mathf.RoundToInt(baseCount * ThreatFactor(questBase))));
        }

        public static float Points(float questBase)
        {
            return Mathf.Clamp(SettlementPoints() * (AragornMod.Config?.questDangerMultiplier ?? 2f)
                * questBase, 200f, 100000f);
        }
    }
}
