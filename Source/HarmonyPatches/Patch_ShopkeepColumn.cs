using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(PawnColumnDefGenerator), nameof(PawnColumnDefGenerator.ImpliedPawnColumnDefs))]
	public static class Patch_ShopkeepColumn
	{
		public static void Prefix()
		{
			WorkTypeDef shopkeeping = DefDatabase<WorkTypeDef>.GetNamedSilentFail("RimMarket_Shopkeeping");
			if (shopkeeping != null) shopkeeping.visible = RimMarketMod.Settings?.shopkeepers ?? false;
		}
	}
}
