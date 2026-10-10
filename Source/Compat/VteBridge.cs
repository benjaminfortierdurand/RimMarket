using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimMarket.Compat
{
	public static class VteBridge
	{
		public const string PackageId = "VanillaExpanded.VanillaTradingExpanded";

		private const int ErrorKey = 0x524D4B03;

		private static bool activeResolved;
		private static bool active;
		private static bool resolved;
		private static bool failed;
		private static FieldInfo instanceField;
		private static MethodInfo registerSold;

		public static bool Active
		{
			get
			{
				if (!activeResolved)
				{
					activeResolved = true;
					active = ModsConfig.IsActive(PackageId);
				}
				return active;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			try
			{
				Type manager = AccessTools.TypeByName("VanillaTradingExpanded.TradingManager");
				if (manager == null) return;
				instanceField = manager.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
				registerSold = manager.GetMethod("RegisterSoldThing", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { typeof(Thing), typeof(int) }, null);
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Vanilla Trading Expanded bridge unavailable: " + e.Message);
				instanceField = null;
				registerSold = null;
			}
		}

		public static void Notify_PlayerSold(Thing thing, int count)
		{
			if (failed || thing?.def == null || count <= 0 || !Active) return;
			Resolve();
			if (instanceField == null || registerSold == null) return;
			try
			{
				object manager = instanceField.GetValue(null);
				if (manager != null) registerSold.Invoke(manager, new object[] { thing, count });
			}
			catch (Exception e)
			{
				failed = true;
				Log.ErrorOnce("[RimMarket] could not tell Vanilla Trading Expanded about a sale, the bridge is now off: " + e, ErrorKey);
			}
		}
	}
}
