using System.Collections.Generic;
using HarmonyLib;
using RimMarket.Inn;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.DrawColorTwo), MethodType.Getter)]
	public static class Patch_InnBed_DrawColorTwo
	{
		public static void Postfix(Building_Bed __instance, ref Color __result)
		{
			if (!InnBeds.Any || !InnBeds.IsInn(__instance)) return;
			if (__instance.def.building != null && !__instance.def.building.bed_UseSheetColor) return;
			__result = InnUtility.SheetColor;
		}
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.DrawGUIOverlay))]
	public static class Patch_InnBed_DrawGUIOverlay
	{
		public static bool Prefix(Building_Bed __instance)
		{
			if (!InnBeds.Any || !InnBeds.IsInn(__instance) || __instance.OwnersForReading.Count > 0) return true;
			if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Closest)
				GenMapUI.DrawThingLabel(__instance, InnUtility.BedLabel, GenMapUI.DefaultThingLabelColor);
			return false;
		}
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetGizmos))]
	public static class Patch_InnBed_GetGizmos
	{
		public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
		{
			bool inn = InnBeds.Any && InnBeds.IsInn(__instance);
			foreach (Gizmo g in __result)
			{
				if (inn && Locked(g)) g.Disable("RimMarket.InnBedLocked".Translate());
				yield return g;
			}
		}

		private static bool Locked(Gizmo g)
		{
			if (g is Command_SetBedOwnerType) return true;
			if (g is Command_Toggle t)
				return t.defaultLabel == "CommandBedSetAsMedicalLabel".Translate() || t.defaultLabel == "CommandBedSetForPrisonersLabel".Translate();
			if (g is Command_Action a) return a.defaultLabel == "CommandThingSetOwnerLabel".Translate();
			return false;
		}
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.Medical), MethodType.Setter)]
	public static class Patch_InnBed_Medical
	{
		public static void Postfix(Building_Bed __instance) => InnUtility.Notify_PurposeChanged(__instance);
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.ForOwnerType), MethodType.Setter)]
	public static class Patch_InnBed_ForOwnerType
	{
		public static void Postfix(Building_Bed __instance) => InnUtility.Notify_PurposeChanged(__instance);
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.ForPrisoners), MethodType.Setter)]
	public static class Patch_InnBed_ForPrisoners
	{
		public static void Postfix(Building_Bed __instance) => InnUtility.Notify_PurposeChanged(__instance);
	}

	[HarmonyPatch(typeof(RestUtility), nameof(RestUtility.IsValidBedFor))]
	public static class Patch_InnBed_IsValidBedFor
	{
		public static void Postfix(Thing bedThing, ref bool __result)
		{
			if (__result && InnBeds.Any && InnBeds.IsInn(bedThing)) __result = false;
		}
	}

	[HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
	public static class Patch_InnBed_AvailableColonistBeds
	{
		public static void Postfix(Map map, ref int singleBeds, ref int doubleBeds)
		{
			if (!InnBeds.Any) return;
			List<Building_Bed> beds = InnBeds.On(map);
			for (int i = 0; i < beds.Count; i++)
			{
				Building_Bed b = beds[i];
				if (b.ForHumanBabies || b.Medical || !b.ForColonists) continue;
				if (b.SleepingSlotsCount == 1) singleBeds--;
				else doubleBeds--;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
	public static class Patch_InnBed_ExitMap
	{
		public static void Prefix(Pawn __instance)
		{
			if (InnBeds.Any) InnUtility.CheckOut(__instance);
		}
	}
}
