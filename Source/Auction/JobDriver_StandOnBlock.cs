using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimMarket.Auction
{
	public class JobDriver_StandOnBlock : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			Toil stand = ToilMaker.MakeToil("StandOnBlock");
			stand.defaultCompleteMode = ToilCompleteMode.Never;
			stand.handlingFacing = true;
			stand.initAction = () => pawn.pather.StopDead();
			stand.tickAction = () => pawn.rotationTracker.FaceCell(pawn.Position + IntVec3.South);
			yield return stand;
		}
	}
}
