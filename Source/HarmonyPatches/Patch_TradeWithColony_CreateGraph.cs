using System;
using HarmonyLib;
using RimMarket.AI;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(LordJob_TradeWithColony), nameof(LordJob_TradeWithColony.CreateGraph))]
	public static class Patch_TradeWithColony_CreateGraph
	{
		private static readonly AccessTools.FieldRef<LordJob_TradeWithColony, IntVec3> ChillSpotRef =
			AccessTools.FieldRefAccess<LordJob_TradeWithColony, IntVec3>("chillSpot");

		private static readonly AccessTools.FieldRef<LordJob_TradeWithColony, Faction> FactionRef =
			AccessTools.FieldRefAccess<LordJob_TradeWithColony, Faction>("faction");

		private static bool warned;

		public static void Postfix(LordJob_TradeWithColony __instance, StateGraph __result)
		{
			try
			{
				MarketGraph.Graft(__instance, __result, ChillSpotRef(__instance), FactionRef(__instance));
			}
			catch (Exception e)
			{
				if (warned) return;
				warned = true;
				Log.Warning("[RimMarket] could not graft the stall toil onto a trade caravan: " + e);
			}
		}
	}
}
