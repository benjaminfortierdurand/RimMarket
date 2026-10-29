using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class JobDriver_MarketSnatch : JobDriver
	{
		private const int EyeTicks = 200;

		private bool snatched;

		private CompMarketStall Stall => MarketStallDefs.StallOf(job.GetTarget(TargetIndex.A).Thing);

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override string GetReport()
		{
			return snatched ? "RimMarket.ReportFleeing".Translate().ToString() : base.GetReport();
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => !snatched && (Stall == null || !Stall.Spawned));
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			Toil eye = ToilMaker.MakeToil("EyeTheWares");
			eye.defaultCompleteMode = ToilCompleteMode.Delay;
			eye.defaultDuration = EyeTicks;
			eye.handlingFacing = true;
			eye.tickAction = delegate
			{
				CompMarketStall stall = Stall;
				if (stall != null) pawn.rotationTracker.FaceCell(stall.Position);
			};
			yield return eye;

			Toil grab = ToilMaker.MakeToil("Snatch");
			grab.defaultCompleteMode = ToilCompleteMode.Instant;
			grab.initAction = delegate
			{
				snatched = ThiefUtility.TrySnatch(pawn, Stall);
				if (!snatched) EndJobWith(JobCondition.Incompletable);
			};
			yield return grab;

			Toil run = Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
			run.AddPreInitAction(() => job.locomotionUrgency = LocomotionUrgency.Jog);
			yield return run;

			Toil away = ToilMaker.MakeToil("SlipAway");
			away.defaultCompleteMode = ToilCompleteMode.Instant;
			away.initAction = delegate
			{
				Map map = pawn.Map;
				if (map != null && (pawn.Position.OnEdge(map) || map.exitMapGrid.IsExitCell(pawn.Position)))
					pawn.ExitMap(true, CellRect.WholeMap(map).GetClosestEdge(pawn.Position));
			};
			yield return away;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref snatched, "snatched");
		}
	}
}
