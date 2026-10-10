using HarmonyLib;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
	public static class Patch_TradeDeal_TryExecute
	{
		public static void Prefix(TradeDeal __instance, out Vector2 __state)
		{
			__state = Vector2.zero;
			if (!MarketManager.AnyVisits || TradeSession.giftMode) return;
			if (!(TradeSession.trader is Pawn trader) || MarketManager.Instance.VisitFor(trader) == null) return;
			foreach (Tradeable t in __instance.AllTradeables)
			{
				if (t == null || t.IsCurrency) continue;
				if (t.ActionToDo == TradeAction.PlayerBuys) __state.x += t.CurTotalCurrencyCostForSource;
				else if (t.ActionToDo == TradeAction.PlayerSells) __state.y += t.CurTotalCurrencyCostForDestination;
			}
		}

		public static void Postfix(bool __result, ref bool actuallyTraded, Vector2 __state)
		{
			if (!__result || !actuallyTraded || __state == Vector2.zero) return;
			if (!(TradeSession.trader is Pawn trader)) return;
			MarketManager.Instance?.Notify_Traded(trader, __state.x, __state.y);
		}
	}
}
