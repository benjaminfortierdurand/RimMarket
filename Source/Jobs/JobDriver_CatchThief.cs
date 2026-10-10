using System.Collections.Generic;
using RimMarket.Thieves;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class JobDriver_CatchThief : JobDriver
	{
		private const int RepathInterval = 30;

		private Pawn Thief => job.GetTarget(TargetIndex.A).Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => !ThiefUtility.IsFleeingThief(Thief));

			Toil chase = ToilMaker.MakeToil("ChaseThief");
			chase.defaultCompleteMode = ToilCompleteMode.Never;
			chase.initAction = () => pawn.pather.StartPath(Thief, PathEndMode.Touch);
			chase.tickAction = delegate
			{
				Pawn thief = Thief;
				if (thief == null) return;
				if (pawn.Position.AdjacentTo8WayOrInside(thief.Position))
				{
					ReadyForNextToil();
					return;
				}
				if (!pawn.pather.Moving || pawn.IsHashIntervalTick(RepathInterval))
					pawn.pather.StartPath(thief, PathEndMode.Touch);
			};
			yield return chase;

			Toil tackle = ToilMaker.MakeToil("Tackle");
			tackle.defaultCompleteMode = ToilCompleteMode.Instant;
			tackle.initAction = () => ThiefUtility.TryTackle(pawn, Thief);
			yield return tackle;
		}
	}
}
