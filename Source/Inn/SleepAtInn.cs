using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Inn
{
	public class JobGiver_SleepAtInn : ThinkNode_JobGiver
	{
		private const int NoBedRetryTicks = 500;
		private const int MaxRemembered = 200;
		private static readonly Dictionary<int, int> noBedUntil = new Dictionary<int, int>();

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!InnBeds.Any || pawn?.Map == null) return null;
			int now = Find.TickManager.TicksGame;
			if (noBedUntil.TryGetValue(pawn.thingIDNumber, out int until) && now < until && until - now <= NoBedRetryTicks) return null;
			if (!InnUtility.WantsSleep(pawn) || !InnUtility.MayLodge(pawn) || !InnUtility.CanAfford(pawn)) return null;
			Building_Bed bed = InnUtility.FindBed(pawn);
			if (bed == null)
			{
				if (noBedUntil.Count >= MaxRemembered) noBedUntil.Clear();
				noBedUntil[pawn.thingIDNumber] = now + NoBedRetryTicks;
				return null;
			}
			noBedUntil.Remove(pawn.thingIDNumber);
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
