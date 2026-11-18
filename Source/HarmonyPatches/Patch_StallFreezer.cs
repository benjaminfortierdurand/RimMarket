using HarmonyLib;
using RimMarket.Buildings;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.TryGetFixedTemperature))]
	public static class Patch_StallFreezer_TryGetFixedTemperature
	{
		public static void Postfix(IThingHolder holder, ref float temperature, ref bool __result)
		{
			if (__result || !(holder is Building_MarketStall stall)) return;
			if (!stall.TryGetFreezerTemperature(out float cold)) return;
			temperature = cold;
			__result = true;
		}
	}
}
