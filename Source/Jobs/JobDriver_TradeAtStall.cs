using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class JobDriver_TradeAtStall : JobDriver
	{
		private CompMarketStall Stall => MarketStallDefs.StallOf(job.GetTarget(TargetIndex.A).Thing);

		private Pawn Trader => job.GetTarget(TargetIndex.B).Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOnDespawnedOrNull(TargetIndex.B);
			this.FailOn(() => Trader == null || !Trader.CanTradeNow || Trader.mindState.traderDismissed);
			CompMarketStall target = Stall;
			yield return Toils_Goto.GotoCell(target != null ? target.CustomerCell : pawn.Position, PathEndMode.OnCell);
			Toil trade = ToilMaker.MakeToil("TradeAtStall");
			trade.defaultCompleteMode = ToilCompleteMode.Instant;
			trade.initAction = delegate
			{
				Pawn actor = trade.actor;
				CompMarketStall stall = Stall;
				Pawn trader = Trader;
				if (trader == null || !trader.CanTradeNow) return;
				if (stall != null) actor.rotationTracker.FaceCell(stall.Position);
				Find.WindowStack.Add(new Dialog_Trade(actor, trader));
				stall?.MarkDisplayDirty();
				PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.InteractingWithTraders, KnowledgeAmount.Total);
			};
			yield return trade;
		}
	}
}
