using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class JobDriver_StandAtStall : JobDriver
	{
		private const int Duration = 1500;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil stand = ToilMaker.MakeToil("StandAtStall");
			stand.defaultCompleteMode = ToilCompleteMode.Delay;
			stand.defaultDuration = Duration;
			stand.handlingFacing = true;
			stand.socialMode = RandomSocialMode.Normal;
			stand.initAction = () => pawn.pather.StopDead();
			stand.tickAction = delegate
			{
				if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
			};
			yield return stand;
		}
	}
}
