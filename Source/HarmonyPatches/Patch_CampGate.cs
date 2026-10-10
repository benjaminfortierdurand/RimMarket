using HarmonyLib;
using RimMarket.AI;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
	public static class Patch_CampGate_PawnCanOpen
	{
		public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
		{
			if (__result || p?.RaceProps == null || !p.RaceProps.Animal) return;
			if (__instance.Faction != Faction.OfPlayer || !(p.lord?.LordJob is LordJob_TradeWithColony)) return;
			if (CaravanCamp.GateOfCamp(__instance)) __result = true;
		}
	}
}
