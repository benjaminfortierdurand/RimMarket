using HarmonyLib;
using RimMarket.Buildings;
using RimWorld;
using Verse.AI;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(JobGiver_PickUpOpportunisticWeapon), "TryGiveJob")]
	public static class Patch_OpportunisticPickup_TryGiveJob
	{
		public static void Postfix(ref Job __result)
		{
			if (__result?.targetA.Thing?.ParentHolder is Building_MarketStall) __result = null;
		}
	}
}
