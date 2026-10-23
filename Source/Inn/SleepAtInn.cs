using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Inn
{
	public class JobGiver_SleepAtInn : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!InnBeds.Any || pawn?.Map == null) return null;
			if (!InnUtility.WantsSleep(pawn) || !InnUtility.MayLodge(pawn) || !InnUtility.CanAfford(pawn)) return null;
			Building_Bed bed = InnUtility.FindBed(pawn);
			if (bed == null) return null;
			return JobMaker.MakeJob(RimMarketDefOf.RimMarket_SleepAtInn, bed);
		}
	}

	public class JobDriver_SleepAtInn : JobDriver_LayDown
	{
		protected override IEnumerable<Toil> MakeNewToils()
		{
			AddFailCondition(() => Bed == null || !InnBeds.IsInn(Bed));
			yield return Toils_Bed.ClaimBedIfNonMedical(TargetIndex.A);
			yield return Toils_Bed.GotoBed(TargetIndex.A);
			Toil pay = ToilMaker.MakeToil("RimMarketPayForBed");
			pay.initAction = () =>
			{
				if (InnUtility.Charge(pawn, Bed)) return;
				InnUtility.CheckOut(pawn);
				EndJobWith(JobCondition.Incompletable);
			};
			pay.defaultCompleteMode = ToilCompleteMode.Instant;
			pay.FailOn(() => !Bed.OccupiedRect().Contains(pawn.Position));
			yield return pay;
			Toil lay = LayDownToil(true);
			lay.FailOn(() => !Bed.OccupiedRect().Contains(pawn.Position));
			yield return lay;
		}

		public override string GetReport()
		{
			if (!asleep && pawn.CurrentBed() != Bed) return "RimMarket.ReportGoingToInn".Translate();
			return base.GetReport();
		}
	}
}
