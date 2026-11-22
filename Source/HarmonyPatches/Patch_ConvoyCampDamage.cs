using HarmonyLib;
using RimMarket.Routes;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_BuildingTookDamage))]
	public static class Patch_ConvoyCampDamage
	{
		public static bool Prefix(Faction __instance, Building building, DamageInfo dinfo)
		{
			if (dinfo.Instigator?.Faction != Faction.OfPlayer) return true;
			if (!(building?.Map?.Parent is ConvoyBattle battle)) return true;
			return battle.convoy?.Faction != __instance;
		}
	}
}
