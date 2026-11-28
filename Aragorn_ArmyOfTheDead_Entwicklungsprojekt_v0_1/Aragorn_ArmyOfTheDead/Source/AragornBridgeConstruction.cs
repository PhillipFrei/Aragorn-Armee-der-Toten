using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AragornArmy
{
    public sealed partial class AragornRescueGame
    {
        private void EnsureBridgeSite()
        {
            if (!moatRequiresPreparation || rescueSite == null || !rescueSite.HasMap) return;
            Map map = rescueSite.Map;
            ThingDef signDef = DefDatabase<ThingDef>.GetNamed("AR_BridgeWorksite");
            if (map.listerThings.ThingsOfDef(signDef).Count != 0) return;
            // One-time upgrade of existing moat sites; never rebuild their walls or NPCs.
            AragornCryptInteriors.RoofRoom(map, map.Center, -13, -10, 13, 10);
            AragornRescueMoatFortress.RevealExterior(map);
            AragornCryptInteriors.RefreshDiscoverableRooms(map);
            var sign = (Building_AragornBridge)ThingMaker.MakeThing(signDef);
            sign.finished = moatCrossingPrepared || AragornRescueMoatFortress.CrossingIsPrepared(map);
            GenSpawn.Spawn(sign, AragornRescueMoatFortress.SouthBank(map) + new IntVec3(2, 0, 0), map);
        }

        private void PrepareMoatCrossing(Pawn pawn)
        {
            if (pawn?.Map == null) return;
            EnsureBridgeSite();
            var sign = pawn.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("AR_BridgeWorksite"))
                .OfType<Building_AragornBridge>().FirstOrDefault();
            sign?.OrderWork(pawn);
        }
    }

    public sealed class Building_AragornBridge : Building
    {
        public const int WoodRequired = 500;
        public const int WorkRequired = 10000; // four in-game hours at normal work rate
        private bool paid;
        private int work;
        public bool finished;
        public float Progress => (float)work / WorkRequired;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref paid, "bridgeWoodPaid");
            Scribe_Values.Look(ref work, "bridgeWork");
            Scribe_Values.Look(ref finished, "bridgeFinished");
        }

        private List<Thing> AvailableWood(Pawn pawn) => Map.listerThings.ThingsOfDef(ThingDefOf.WoodLog)
            .Where(t => t.Spawned && !t.IsForbidden(pawn) && t.Position.DistanceTo(Position) <= 5f &&
                pawn.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some)).ToList();

        public override string GetInspectString() => base.GetInspectString() + "\n" +
            (finished ? AragornText.T("AR_Bridge_Finished") :
                AragornText.F("AR_Bridge_Status", paid ? WoodRequired : 0, WoodRequired,
                    (int)(Progress * 100f)) + "\n" + AragornText.T("AR_Bridge_Instructions"));

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (var option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!finished && pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Humanlike)
                yield return new FloatMenuOption(AragornText.T("AR_Bridge_Build"), () => OrderWork(pawn));
        }

        public void OrderWork(Pawn pawn)
        {
            if (finished || pawn == null || !pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)) return;
            if (!paid && AvailableWood(pawn).Sum(t => t.stackCount) < WoodRequired)
            {
                Messages.Message(AragornText.T("AR_Bridge_NeedWood"), this, MessageTypeDefOf.RejectInput);
                return;
            }
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("AR_BuildMoatBridge"), this), JobTag.Misc);
        }

        public bool PayWood(Pawn pawn)
        {
            if (paid) return true;
            var stacks = AvailableWood(pawn);
            if (stacks.Sum(t => t.stackCount) < WoodRequired) return false;
            int remaining = WoodRequired;
            foreach (Thing stack in stacks)
            {
                int count = Math.Min(remaining, stack.stackCount);
                stack.SplitOff(count).Destroy(DestroyMode.Vanish);
                remaining -= count;
                if (remaining == 0) break;
            }
            paid = true;
            return true;
        }

        public void Work(Pawn pawn)
        {
            if (!paid || finished) return;
            work = Math.Min(work + 1, WorkRequired);
            if (work < WorkRequired) return;
            Current.Game.GetComponent<AragornRescueGame>().FinishMoatCrossing(pawn);
            finished = AragornRescueMoatFortress.CrossingIsPrepared(Map);
        }
    }

    public sealed class JobDriver_BuildMoatBridge : JobDriver
    {
        private Building_AragornBridge Site => job.targetA.Thing as Building_AragornBridge;
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil build = new Toil();
            build.initAction = () => {
                if (Site == null || !Site.PayWood(pawn)) EndJobWith(JobCondition.Incompletable);
            };
            build.tickAction = () => {
                if (Site == null) { EndJobWith(JobCondition.Incompletable); return; }
                pawn.rotationTracker.FaceTarget(Site);
                Site.Work(pawn);
                if (Site.finished) ReadyForNextToil();
            };
            build.defaultCompleteMode = ToilCompleteMode.Never;
            build.WithProgressBar(TargetIndex.A, () => Site?.Progress ?? 0f);
            yield return build;
        }
    }
}
