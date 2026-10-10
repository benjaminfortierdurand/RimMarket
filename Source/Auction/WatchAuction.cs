using System.Collections.Generic;
using RimMarket.Compat;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Auction
{
	public class JoyGiver_WatchAuction : JoyGiver_WatchBuilding
	{
		private const float BidderChanceFactor = 2f;

		public override float GetChance(Pawn pawn)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || pawn?.Map == null || pawn.IsColonist || !mgr.AnyAuctionOn(pawn.Map)) return 0f;
			if (!MarketUtility.IsShoppingGuest(pawn)) return 0f;
			return base.GetChance(pawn) * (mgr.IsBidder(pawn) ? BidderChanceFactor : 1f);
		}

		protected override bool CanInteractWith(Pawn pawn, Thing t, bool inBed)
		{
			if (inBed || !(t is Building_AuctionBlock block)) return false;
			if (MarketManager.Instance?.AuctionAt(block) == null) return false;
			return base.CanInteractWith(pawn, t, inBed);
		}
	}

	public class JobDriver_WatchAuction : JobDriver_WatchBuilding
	{
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => !(TargetThingA is Building_AuctionBlock block) || MarketManager.Instance?.AuctionAt(block) == null);
			foreach (Toil toil in base.MakeNewToils())
			{
				if (toil.defaultCompleteMode == ToilCompleteMode.Delay) toil.socialMode = RandomSocialMode.SuperActive;
				yield return toil;
			}
		}
	}
}
