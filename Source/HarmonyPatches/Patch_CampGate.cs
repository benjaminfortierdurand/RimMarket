using HarmonyLib;
using RimMarket.AI;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
	public static class Patch_CampGate_PawnCanOpen
	{
		public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
		{
			if (__result || p?.RaceProps == null || !p.RaceProps.Animal || !p.CanOpenDoors) return;
			if (__instance.Faction != Faction.OfPlayer || p.Faction == null || p.Faction == Faction.OfPlayer || p.Faction.HostileTo(Faction.OfPlayer)) return;
			Lord lord = p.lord;
			if (lord?.LordJob is LordJob_TradeWithColony) __result = Stragglers.Leaving(lord.CurLordToil) || CaravanCamp.GateOfCamp(__instance);
			else if (lord?.LordJob is LordJob_ExitMapBest) __result = true;
		}
	}
}
