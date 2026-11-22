using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
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
			UseNow(bought.def);
		}

		private const float SnackBelow = 0.75f;
		private const float CravingBelow = 0.5f;

		private void UseNow(ThingDef def)
		{
			if (def?.ingestible == null || pawn.IsColonist || !pawn.RaceProps.Humanlike || pawn.inventory == null || pawn.jobs == null) return;
			ThingOwner carried = pawn.inventory.innerContainer;
			Thing item = null;
			for (int i = 0; i < carried.Count; i++)
			{
				if (carried[i].def == def)
				{
					item = carried[i];
					break;
				}
			}
			if (item == null || !WantsNow(item)) return;
			Job use = JobMaker.MakeJob(JobDefOf.Ingest, item);
			use.count = def.IsDrug ? 1 : Mathf.Clamp(FoodUtility.WillIngestStackCountOf(pawn, def, FoodUtility.GetNutrition(pawn, item, def)), 1, item.stackCount);
			pawn.jobs.jobQueue.EnqueueFirst(use);
		}

		private bool WantsNow(Thing item)
		{
			ThingDef def = item.def;
			if (def.IsDrug)
			{
				if (!pawn.CanTakeDrug(def)) return false;
				ChemicalDef chemical = item.TryGetComp<CompDrug>()?.Props.chemical;
				if (chemical != null && AddictionUtility.IsAddicted(pawn, chemical))
				{
					Need craving = chemical.addictionHediff?.chemicalNeed == null ? null : pawn.needs?.TryGetNeed(chemical.addictionHediff.chemicalNeed);
					return craving != null && craving.CurLevelPercentage < CravingBelow;
				}
				if (def.ingestible.drugCategory != DrugCategory.Social) return false;
				List<IngestionOutcomeDoer> effects = def.ingestible.outcomeDoers;
				if (effects == null) return true;
				for (int i = 0; i < effects.Count; i++)
				{
					if (effects[i] is IngestionOutcomeDoer_GiveHediff high && high.hediffDef != null && pawn.health.hediffSet.HasHediff(high.hediffDef)) return false;
				}
				return true;
			}
			if (!def.IsNutritionGivingIngestible || def.ingestible.preferability < FoodPreferability.RawTasty) return false;
			Need_Food food = pawn.needs?.food;
			return food != null && food.CurLevelPercentage < SnackBelow && pawn.WillEat(item);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref browseTicks, "browseTicks");
		}
	}
}
