using HarmonyLib;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
	public static class Patch_Tradeable_GetPriceFor
	{
		[System.ThreadStatic] private static bool inner;

		public static void Postfix(Tradeable __instance, TradeAction action, ref float __result)
		{
			if (inner) return;
			if (!MarketManager.AnyVisits || TradeSession.giftMode || TradeSession.TradeCurrency == TradeCurrency.Favor) return;
			if (action == TradeAction.None || __instance.IsCurrency) return;
			if (!(TradeSession.trader is Pawn trader)) return;
			StallVisit v = MarketManager.Instance.VisitFor(trader);
			if (v == null || (!v.haggleUsed && v.repPriceBonus == 0f)) return;
			float baseBuy, baseSell;
			inner = true;
			try
			{
				baseBuy = action == TradeAction.PlayerBuys ? __result : __instance.GetPriceFor(TradeAction.PlayerBuys);
				baseSell = action == TradeAction.PlayerSells ? __result : __instance.GetPriceFor(TradeAction.PlayerSells);
			}
			finally
			{
				inner = false;
			}
			float buy = baseBuy * v.haggleBuyFactor * (1f - v.repPriceBonus);
			float sell = baseSell * v.haggleSellFactor * (1f + v.repPriceBonus);
			if (sell > buy)
			{
				float meet = Mathf.Clamp((sell + buy) / 2f, Mathf.Min(baseSell, baseBuy), Mathf.Max(baseSell, baseBuy));
				buy = meet;
				sell = meet;
			}
			__result = action == TradeAction.PlayerBuys ? buy : sell;
		}
	}
}
