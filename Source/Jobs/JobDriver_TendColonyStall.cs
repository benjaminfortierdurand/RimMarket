using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class WorkGiver_TendColonyStall : WorkGiver_Scanner
	{
		private static readonly List<Building_MarketStall> buffer = new List<Building_MarketStall>();

		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (!ColonyStalls.KeepersNeeded || pawn.Map == null) return true;
			return !ColonyStalls.CustomersAround(pawn.Map);
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			ColonyStalls.Collect(pawn.Map, buffer);
			return buffer.Cast<Thing>().ToList();
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!(t is Building_MarketStall s) || !s.ColonyStall || !s.Spawned || t.IsForbidden(pawn)) return false;
			CompMarketStall stall = s.Stall;
			if (stall == null || !ColonyStalls.InMarket(s)) return false;
			if (!ColonyStalls.HasStock(s))
			{
				JobFailReason.Is("RimMarket.ColonyStallNothingToSell".Translate());
				return false;
			}
			Pawn keeper = ColonyStalls.KeeperAt(s);
			if (keeper != null && keeper != pawn) return false;
			IntVec3 cell = stall.MerchantCell;
			if (!cell.InBounds(s.Map) || !cell.Standable(s.Map)) return false;
			if (!pawn.CanReserve(cell, 1, -1, null, forced)) return false;
			return pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some);
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			CompMarketStall stall = (t as Building_MarketStall)?.Stall;
			if (stall == null) return null;
			return JobMaker.MakeJob(RimMarketDefOf.RimMarket_TendColonyStall, t, stall.MerchantCell, stall.MerchantLook);
		}
	}

	public class JobDriver_TendColonyStall : JobDriver
	{
		private const int Duration = 2500;
		private const int CheckInterval = 250;

		private Building_MarketStall Stall => job.GetTarget(TargetIndex.A).Thing as Building_MarketStall;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => Stall == null || !Stall.ColonyStall || !ColonyStalls.KeepersNeeded);
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			Toil tend = ToilMaker.MakeToil("TendColonyStall");
			tend.defaultCompleteMode = ToilCompleteMode.Delay;
			tend.defaultDuration = Duration;
			tend.handlingFacing = true;
			tend.socialMode = RandomSocialMode.Normal;
			tend.initAction = () => pawn.pather.StopDead();
			tend.tickIntervalAction = delegate(int delta)
			{
				Building_MarketStall s = Stall;
				if (s == null) return;
				s.keeper = pawn;
				s.keeperTick = Find.TickManager.TicksGame;
				if (job.GetTarget(TargetIndex.C).IsValid) pawn.rotationTracker.FaceCell(job.GetTarget(TargetIndex.C).Cell);
				if (!pawn.IsHashIntervalTick(CheckInterval, delta)) return;
				if (!ColonyStalls.CustomersAround(pawn.Map) || !ColonyStalls.InMarket(s) || !ColonyStalls.HasStock(s))
					EndJobWith(JobCondition.Succeeded);
			};
			tend.AddFinishAction(delegate
			{
				Building_MarketStall s = Stall;
				if (s != null && s.keeper == pawn) s.keeper = null;
			});
			yield return tend;
		}
	}
}
