using RimMarket.Market;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class JobGiver_ChaseMarketThief : ThinkNode_JobGiver
	{
		private const int ChaseExpiry = 2500;

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!MarketManager.AnyThefts) return null;
			StallVisit v = MarketManager.Instance?.VisitFor(pawn.GetLord());
			if (v == null) return null;
			if (pawn.Downed || !pawn.Awake() || pawn.InMentalState) return null;
			if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return null;
			Pawn thief = ThiefUtility.ThiefToChase(pawn, v.id);
			if (thief == null) return null;
			Job chase = JobMaker.MakeJob(RimMarketDefOf.RimMarket_CatchThief, thief);
			chase.locomotionUrgency = LocomotionUrgency.Sprint;
			chase.expiryInterval = ChaseExpiry;
			return chase;
		}
	}
}
