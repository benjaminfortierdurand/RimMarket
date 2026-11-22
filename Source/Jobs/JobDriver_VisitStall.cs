using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RimMarket.Jobs
{
	public class JobDriver_VisitStall : JobDriver
	{
		private const int MinTicksForThought = 300;
		private const int LineCheck = 30;
		private const int MaxLineWait = 3000;
		private const int ServedBrowse = 450;

		private int browseTicks;
		private int waited;

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

			if (Buying) yield return WaitInLine();

			Toil browse = ToilMaker.MakeToil("BrowseStall");
			browse.defaultCompleteMode = ToilCompleteMode.Delay;
			browse.defaultDuration = job.def.joyDuration;
			browse.handlingFacing = true;
			browse.socialMode = RandomSocialMode.Normal;
			browse.initAction = delegate
			{
				pawn.pather.StopDead();
				if (waited > 0 && ticksLeftThisToil > ServedBrowse) ticksLeftThisToil = ServedBrowse;
			};
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
				if (!RimMarket.AI.MarketBreaks.CaravanFolk(pawn)) MarketManager.Instance?.Notify_Browsed(Stall);
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

		private int LineIndex() => MarketUtility.QueueIndex(Stall, job.GetTarget(TargetIndex.B).Cell);

		private bool Taken(IntVec3 cell)
		{
			List<Thing> things = cell.GetThingList(pawn.Map);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is Pawn other && other != pawn && other.jobs?.curDriver is JobDriver_VisitStall) return true;
			}
			return false;
		}

		private Toil WaitInLine()
		{
			Toil wait = ToilMaker.MakeToil("WaitInLine");
			wait.defaultCompleteMode = ToilCompleteMode.Never;
			wait.handlingFacing = true;
			wait.socialMode = RandomSocialMode.Normal;
			wait.initAction = delegate
			{
				if (LineIndex() <= 0) ReadyForNextToil();
			};
			wait.tickIntervalAction = delegate(int delta)
			{
				CompMarketStall stall = Stall;
				if (stall == null || pawn.pather.Moving) return;
				int index = LineIndex();
				if (index <= 0)
				{
					ReadyForNextToil();
					return;
				}
				pawn.rotationTracker.FaceCell(stall.Position);
				waited += delta;
				if (waited > MaxLineWait)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}
				if (!pawn.IsHashIntervalTick(LineCheck, delta)) return;
				IntVec3 ahead = MarketUtility.QueueCell(stall, index - 1);
				if (Taken(ahead) || !pawn.CanReserve(ahead)) return;
				pawn.Map.reservationManager.Release(job.GetTarget(TargetIndex.B), pawn, job);
				if (!pawn.Reserve(ahead, job, 1, -1, null, false))
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}
				job.SetTarget(TargetIndex.B, ahead);
				pawn.pather.StartPath(ahead, PathEndMode.OnCell);
			};
			return wait;
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
			(pawn.GetLord()?.LordJob as LordJob_MarketShoppers)?.Notify_Bought(pawn, bought.def);
			UseNow(bought.def);
		}

		private void UseNow(ThingDef def)
		{
			if (def?.ingestible == null || pawn.IsColonist || !pawn.RaceProps.Humanlike || pawn.jobs == null) return;
			Thing item = MarketSnacks.Carried(pawn, def);
			if (item == null || !MarketSnacks.WantsNow(pawn, item)) return;
			pawn.jobs.jobQueue.EnqueueFirst(MarketSnacks.UseJob(pawn, item));
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref browseTicks, "browseTicks");
			Scribe_Values.Look(ref waited, "waited");
		}
	}
}
