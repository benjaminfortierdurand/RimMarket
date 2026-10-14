using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.ExposeData))]
	public static class Patch_WorkSettings_ExposeData
	{
		private const string PackageId = "forti.rimmarket";
		private const string ShopkeepDef = "RimMarket_Shopkeeping";

		private static readonly AccessTools.FieldRef<Pawn_WorkSettings, DefMap<WorkTypeDef, int>> Priorities =
			AccessTools.FieldRefAccess<Pawn_WorkSettings, DefMap<WorkTypeDef, int>>("priorities");

		private static readonly AccessTools.FieldRef<DefMap<WorkTypeDef, int>, List<int>> Values =
			AccessTools.FieldRefAccess<DefMap<WorkTypeDef, int>, List<int>>("values");

		public static void Postfix(Pawn_WorkSettings __instance)
		{
			if (Scribe.mode != LoadSaveMode.LoadingVars || Current.ProgramState != ProgramState.MapInitializing || !AddedMidSave()) return;
			WorkTypeDef shopkeep = DefDatabase<WorkTypeDef>.GetNamedSilentFail(ShopkeepDef);
			DefMap<WorkTypeDef, int> map = Priorities(__instance);
			List<int> values = map == null ? null : Values(map);
			if (shopkeep == null || values == null || shopkeep.index >= values.Count) return;
			values.Insert(shopkeep.index, 0);
			values.RemoveAt(values.Count - 1);
		}

		private static bool AddedMidSave()
		{
			List<string> ids = ScribeMetaHeaderUtility.loadedModIdsList;
			if (ids == null) return false;
			for (int i = 0; i < ids.Count; i++)
			{
				if (ids[i] != null && ids[i].EqualsIgnoreCase(PackageId)) return false;
			}
			return true;
		}
	}
}
