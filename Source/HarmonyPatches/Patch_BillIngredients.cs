using System;
using HarmonyLib;
using RimMarket.Buildings;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
	public static class Patch_BillIngredients_SkipStallStock
	{
		public static void Prefix(ref Predicate<Thing> thingValidator)
		{
			Predicate<Thing> inner = thingValidator;
			if (inner == null) return;
			thingValidator = t => !(t.ParentHolder is Building_MarketStall) && inner(t);
		}
	}
}
