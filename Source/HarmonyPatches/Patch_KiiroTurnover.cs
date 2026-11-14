using System.Reflection;
using HarmonyLib;
using RimMarket.Market;
using Verse;

namespace RimMarket.HarmonyPatches
{
	/// <summary>
	/// Kiiro settles its own cashier deals. Mirror the real turnover into RimMarket so the
	/// guest takings and the market appeal match what the cashier actually earned.
	/// </summary>
	[HarmonyPatch]
	public static class Patch_KiiroTurnover
	{
		private static MethodBase target;

		public static bool Prepare()
		{
			System.Type type = AccessTools.TypeByName("Kiiro_Event.Kiiro_Merchant");
			target = type == null ? null : AccessTools.Method(type, "AddTurnover", new[] { typeof(int) });
			return target != null;
		}

		public static MethodBase TargetMethod() => target;

		public static void Postfix(int num)
		{
			if (num <= 0) return;
			MarketManager.Instance?.RecordKiiroSale(num);
		}
	}
}
