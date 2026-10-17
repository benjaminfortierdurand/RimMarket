using System.Collections.Generic;
using HarmonyLib;
using RimMarket.Routes;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(IncidentWorker_NeutralGroup), "SpawnPawns")]
	public static class Patch_NeutralGroup_SpawnPawns
	{
		public static bool Prefix(IncidentParms parms, ref List<Pawn> __result)
		{
			List<Pawn> pawns = ConvoyBattleUtility.TakeReplay(parms);
			if (pawns == null) return true;
			__result = pawns;
			return false;
		}
	}
}
