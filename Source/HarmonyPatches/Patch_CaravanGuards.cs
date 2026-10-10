using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch]
	public static class Patch_CaravanGuards
	{
		private static MethodBase target;

		public static bool Prepare()
		{
			target = AccessTools.Method(typeof(PawnGroupKindWorker_Trader), "GenerateGuards");
			return target != null;
		}

		public static MethodBase TargetMethod() => target;

		public static void Prefix(List<Pawn> outPawns, out int __state)
		{
			__state = outPawns?.Count ?? 0;
		}

		public static void Postfix(PawnGroupMakerParms parms, List<Pawn> outPawns, int __state)
		{
			if (outPawns == null || parms == null || !parms.tile.Valid) return;
			int cap = RimMarketMod.Settings.GuardCap;
			if (outPawns.Count - __state <= cap) return;
			Map map = Current.Game?.FindMap(parms.tile);
			if (map == null || map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker).Count == 0) return;
			List<Pawn> extra = outPawns.Skip(__state).OrderByDescending(g => g.kindDef.combatPower).Skip(cap).ToList();
			for (int i = 0; i < extra.Count; i++)
			{
				Pawn g = extra[i];
				outPawns.Remove(g);
				if (!g.Spawned && !Find.WorldPawns.Contains(g)) Find.WorldPawns.PassToWorld(g, PawnDiscardDecideMode.Decide);
			}
		}
	}
}
