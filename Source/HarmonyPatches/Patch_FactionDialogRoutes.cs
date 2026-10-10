using HarmonyLib;
using RimMarket.Orders;
using RimMarket.Routes;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor))]
	public static class Patch_FactionDialogRoutes
	{
		public static void Postfix(Pawn negotiator, Faction faction, DiaNode __result)
		{
			if (__result == null || negotiator?.Map == null || faction == null || faction.IsPlayer) return;
			if (!negotiator.Map.IsPlayerHome) return;
			if (faction.def.permanentEnemy || !faction.def.humanlikeFaction || faction.def.caravanTraderKinds.NullOrEmpty()) return;
			int at = Mathf.Max(0, __result.options.Count - 1);
			if (RimMarketMod.Settings.specialOrders)
			{
				DiaOption order = SpecialOrderDialog.RootOption(negotiator, faction);
				if (order != null) __result.options.Insert(at, order);
			}
			if (!RimMarketMod.Settings.tradeRoutes) return;
			DiaOption opt = TradeRouteDialog.RootOption(negotiator, faction);
			if (opt == null) return;
			__result.options.Insert(at, opt);
		}
	}
}
