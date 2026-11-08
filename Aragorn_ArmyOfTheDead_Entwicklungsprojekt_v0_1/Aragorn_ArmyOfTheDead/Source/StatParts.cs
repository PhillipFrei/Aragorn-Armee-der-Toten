using RimWorld;
using Verse;

namespace AragornArmy
{
    // Attached to the three natural armor stats; armor is NOT attached to clothing.
    public class StatPartGhostNaturalArmor : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (!GhostDefinitions.IsGhost(pawn) || AragornMod.Config == null) return;
            // ArmorRating stats use 1.0 = 100%; 10.0 = 1000%.
            val += AragornMod.Config.BaseArmorPercent / 100f * GhostDefinitions.ArmorMultiplier(pawn);
        }
        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            return GhostDefinitions.IsGhost(pawn) ? "Aragorn: körpereigene Geisterrüstung (Modmenü)" : null;
        }
    }

    // The status Hediff determines eligibility; this stat part supplies the live multiplier.
    public class StatPartKinglyCommand : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (!GhostDefinitions.IsGhost(pawn) || (pawn.kindDef.defName == "AR_GhostKing" || pawn.kindDef.defName == "AR_GhostSpiritKing")) return;
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("AR_KinglyCommand");
            if (def == null || pawn.health == null || !pawn.health.hediffSet.HasHediff(def)) return;
            float b = AragornMod.Config == null ? 2f : AragornMod.Config.kingBuff;
            // Reduced cooldown / aiming factor means faster attacks.
            if (parentStat == StatDefOf.MoveSpeed) val *= b;
            else val /= b;
        }
        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (!GhostDefinitions.IsGhost(pawn) || (pawn.kindDef.defName == "AR_GhostKing" || pawn.kindDef.defName == "AR_GhostSpiritKing")) return null;
            return "Königlicher Befehl: Bewegung und Angriffszeit";
        }
    }
}
