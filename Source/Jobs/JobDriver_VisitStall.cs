using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimMarket.Jobs
{
	public class JobDriver_VisitStall : JobDriver
	{
		private const int MinTicksForThought = 300;

		private int browseTicks;

		private CompMarketStall Stall => MarketStallDefs.StallOf(job.GetTarget(TargetIndex.A).Thing);

		private bool Buying => job.def == RimMarketDefOf.RimMarket_BuyAtStall;

		private StallVisit Visit => MarketManager.AnyVisits ? MarketManager.Instance.VisitFor(Stall) : null;

		private Building_MarketStall ColonyStall => job.GetTarget(TargetIndex.A).Thing is Building_MarketStall b && b.ColonyStall ? b : null;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed);
		}

		private bool StallOpen()
		{
			Building_MarketStall colony = ColonyStall;
			if (colony != null) return ColonyStalls.OpenForSale(colony);
			StallVisit v = Visit;
			return v != null && !v.closed && v.StallOk && v.TraderOk;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOn(() => !StallOpen());
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			Toil browse = ToilMaker.MakeToil("BrowseStall");
			browse.defaultCompleteMode = ToilCompleteMode.Delay;
			browse.defaultDuration = job.def.joyDuration;
			browse.handlingFacing = true;
			browse.socialMode = RandomSocialMode.Normal;
			browse.initAction = () => pawn.pather.StopDead();
			browse.tickIntervalAction = delegate(int delta)
			{
				browseTicks += delta;
				CompMarketStall stall = Stall;
				if (stall != null) pawn.rotationTracker.FaceCell(stall.Position);
				pawn.GainComfortFromCellIfPossible(delta);
				if (pawn.needs?.joy == null) return;
				JoyUtility.JoyTickCheckEnd(pawn, delta,
					Buying ? JoyTickFullJoyAction.GoToNextToil : JoyTickFullJoyAction.EndJob, 1f, stall?.parent as Building);
			};
			browse.AddFinishAction(delegate
			{
				if (browseTicks < MinTicksForThought) return;
				pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(RimMarketDefOf.RimMarket_BrowsedMarket);
				if (pawn.IsColonist && Fair.FairUtility.Running(pawn.Map))
					pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(RimMarketDefOf.RimMarket_WentToFair);
				MarketManager.Instance?.Notify_Browsed(Stall);
			});
			yield return browse;

			if (!Buying) yield break;

			Toil buy = ToilMaker.MakeToil("BuyAtStall");
			buy.defaultCompleteMode = ToilCompleteMode.Instant;
			buy.initAction = delegate
			{
				Building_MarketStall colony = ColonyStall;
				if (colony != null)
				{
					if (ColonyStalls.Sell(pawn, colony, out Thing sold, out int paid)) Bought(sold, paid);
					return;
				}
				StallVisit v = Visit;
				if (v == null) return;
				if (!MarketUtility.GuestBuy(pawn, v, out Thing bought, out int price)) return;
				MarketManager.Instance?.RecordGuestSale(v, price);
				Bought(bought, price);
			};
			yield return buy;
		}

		private void Bought(Thing bought, int price)
		{
			CompMarketStall stall = Stall;
			if (stall != null && stall.Spawned)
				RimMarketDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(stall.Position, stall.Map));
			if (pawn.Spawned)
				MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
					"RimMarket.MoteBought".Translate(bought.LabelShort, price), 3.5f);
			pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(RimMarketDefOf.RimMarket_BoughtAtMarket);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref browseTicks, "browseTicks");
		}
	}
}
