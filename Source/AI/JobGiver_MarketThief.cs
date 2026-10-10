using RimMarket.Market;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class JobGiver_MarketThief : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			Lord lord = pawn.GetLord();
			if (!(lord?.LordJob is LordJob_MarketThief thiefJob)) return null;
			if (pawn.carryTracker?.CarriedThing != null) return null;
			if (lord.ticksInToil < thiefJob.LoiterTicks) return null;
			StallVisit v = ThiefUtility.PickTarget(pawn);
			if (v == null) return null;
			IntVec3 cell = MarketUtility.FindBrowseCell(v.stall, pawn);
			if (!cell.IsValid) return null;
			if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit)) return null;
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_Snatch, v.stall.parent, cell, exit);
			job.exitMapOnArrival = true;
			job.locomotionUrgency = LocomotionUrgency.Walk;
			return job;
		}
	}
}
