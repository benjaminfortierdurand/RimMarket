using RimMarket.Compat;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.AI
{
	public class JoyGiver_VisitStalls : JoyGiver
	{
		private const float BuyerChanceFactor = 2f;
		private const float BrokeGuestChanceFactor = 0.5f;

		public override float GetChance(Pawn pawn)
		{
			if (pawn?.Map == null) return 0f;
			bool merchants = MarketManager.AnyVisits && MarketManager.Instance.ActiveVisits(pawn.Map) > 0;
			RimMarketSettings s = RimMarketMod.Settings;
			if (pawn.IsColonist)
			{
				if (!merchants || !s.colonistsBrowse) return 0f;
				return base.GetChance(pawn) * (Fair.FairUtility.Running(pawn.Map) ? Fair.FairUtility.JoyFactor : 1f);
			}
			if (!s.guestsShop || !MarketUtility.IsShoppingGuest(pawn)) return 0f;
			if (!merchants && !ColonyStalls.AnyOpen(pawn.Map)) return 0f;
			bool canBuy = MarketUtility.SilverOf(pawn) >= MarketUtility.MinGuestPurchase;
			return base.GetChance(pawn) * (canBuy ? BuyerChanceFactor : BrokeGuestChanceFactor);
		}

		public override Job TryGiveJob(Pawn pawn)
		{
			if (GetChance(pawn) <= 0f) return null;
			return MarketUtility.MakeVisitJob(pawn);
		}
	}
}
