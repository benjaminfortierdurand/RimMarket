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
		public static void Postfix(Tradeable __instance, TradeAction action, ref float __result)
		{
			if (!MarketManager.AnyVisits) return;
			if (action == TradeAction.None || __instance.IsCurrency) return;
			if (!(TradeSession.trader is Pawn trader)) return;
			StallVisit v = MarketManager.Instance.VisitFor(trader);
			if (v == null || (!v.haggleUsed && v.repPriceBonus == 0f)) return;
			if (action == TradeAction.PlayerBuys)
			{
				__result *= v.haggleBuyFactor * (1f - v.repPriceBonus);
				return;
			}
			float sell = __result * v.haggleSellFactor * (1f + v.repPriceBonus);
			float buy = __instance.GetPriceFor(TradeAction.PlayerBuys);
			__result = Mathf.Min(sell, buy);
		}
	}
}
