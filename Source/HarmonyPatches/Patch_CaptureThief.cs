using HarmonyLib;
using RimMarket.Market;
using RimMarket.Thieves;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberCaptured))]
	public static class Patch_Faction_MemberCaptured
	{
		public static bool Prefix(Faction __instance, Pawn member, Faction violator)
		{
			if (violator == null || !violator.IsPlayer || violator == __instance) return true;
			if (!ThiefUtility.IsCulprit(member)) return true;
			__instance.ideos?.Notify_MemberGainedOrLost();
			MarketManager.Instance?.Notify_CulpritCaptured(member, __instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberStripped))]
	public static class Patch_Faction_MemberStripped
	{
		public static bool Prefix(Pawn member, Faction violator)
		{
			return violator == null || !violator.IsPlayer || !ThiefUtility.CaughtThief(member);
		}
	}

	[HarmonyPatch(typeof(FloatMenuOptionProvider_CapturePawn), "GetSingleOptionFor",
		new[] { typeof(Pawn), typeof(FloatMenuContext) })]
	public static class Patch_FloatMenuCapture
	{
		public static void Postfix(FloatMenuOption __result, Pawn clickedPawn, FloatMenuContext context)
		{
			if (__result == null || !ThiefUtility.IsCulprit(clickedPawn)) return;
			string warn = ": " + "AngersFaction".Translate().CapitalizeFirst();
			string label = __result.Label;
			if (label != null && label.Contains(warn))
				__result.Label = label.Replace(warn, ": " + "RimMarket.CaughtRedHanded".Translate());
			Pawn capturer = context?.FirstSelectedPawn;
			if (capturer == null || __result.action == null || __result.Disabled) return;
			__result.action = () => ThiefUtility.OrderCapture(capturer, clickedPawn);
		}
	}
}
