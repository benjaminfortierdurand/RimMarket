using System.Reflection;
using HarmonyLib;
using RimMarket.Routes;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch]
	public static class Patch_TerritoriesConvoyFight
	{
		private static MethodBase target;

		public static bool Prepare()
		{
			System.Type type = AccessTools.TypeByName("FactionTerritories.GameComponent_FactionTerritories");
			target = type == null ? null : AccessTools.Method(type, "TryClassifyEligibleMap");
			return target != null;
		}

		public static MethodBase TargetMethod() => target;

		public static void Postfix(Map map, ref bool __result)
		{
			if (__result && map?.Parent is ConvoyBattle) __result = false;
		}
	}
}
