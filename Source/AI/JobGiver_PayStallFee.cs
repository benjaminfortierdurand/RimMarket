using RimMarket.Market;
using Verse;
using Verse.AI;

namespace RimMarket.AI
{
	public class JobGiver_PayStallFee : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			StallVisit v = MarketManager.Instance?.VisitFor(pawn);
			if (v == null || !v.feeDue || !v.StallOk || !v.LordAlive) return null;
			if (!pawn.CanReach(v.stall.MerchantCell, PathEndMode.OnCell, Danger.Some)) return null;
			return JobMaker.MakeJob(RimMarketDefOf.RimMarket_PayStallFee, v.stall.parent, v.stall.MerchantCell);
		}
	}
}
