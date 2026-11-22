using System;
using HarmonyLib;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
	public static class Patch_BillIngredients_SkipStallStock
	{
		public static void Prefix(Pawn pawn, ref Predicate<Thing> thingValidator)
		{
			if (thingValidator == null || !ColonyStalls.AnyHeldStock(pawn?.Map)) return;
			thingValidator = Wrap(thingValidator);
		}

		private static Predicate<Thing> Wrap(Predicate<Thing> inner)
		{
			return t => !(t.ParentHolder is Building_MarketStall) && inner(t);
		}
	}
}
