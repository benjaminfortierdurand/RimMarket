using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class JobDriver_PayStallFee : JobDriver
	{
		private const int CountDuration = 120;

		private CompMarketStall Stall => MarketStallDefs.StallOf(job.GetTarget(TargetIndex.A).Thing);

		private StallVisit Visit => MarketManager.Instance?.VisitFor(pawn);

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOn(() =>
			{
				StallVisit v = Visit;
				return v == null || !v.feeDue || v.stall != Stall;
			});
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
			var count = new Toil
			{
				defaultCompleteMode = ToilCompleteMode.Delay,
				defaultDuration = CountDuration,
				handlingFacing = true,
			};
			count.tickAction = () => pawn.rotationTracker.FaceTarget(job.GetTarget(TargetIndex.A));
			count.WithProgressBarToilDelay(TargetIndex.A);
			yield return count;
			var drop = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
			drop.initAction = () => MarketManager.Instance?.PayFee(Visit, pawn);
			yield return drop;
		}
	}
}
