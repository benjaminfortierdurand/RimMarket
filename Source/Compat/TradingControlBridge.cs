using System.Collections.Generic;
using Verse;

namespace RimMarket.Compat
{
	public static class TradingControlBridge
	{
		private const string PackageId = "TradingControl.Tad.Rimworld.Core";

		private static readonly string[] SpotDefs = { "TradingSpot", "Marketplace" };
		private static readonly string[] SpotClasses =
		{
			"TradingControl.definitions.TradingSpot",
			"TradingControl.definitions.Marketplace",
		};

		private static bool resolved;
		private static bool active;
		private static readonly List<ThingDef> spots = new List<ThingDef>();

		public static bool Active
		{
			get
			{
				Resolve();
				return active;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			active = ModsConfig.IsActive(PackageId);
			if (!active) return;
			for (int i = 0; i < SpotDefs.Length; i++)
			{
				ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpotDefs[i]);
				if (def?.thingClass != null && def.thingClass.FullName == SpotClasses[i]) spots.Add(def);
			}
		}

		public static bool SpotOnMap(Map map)
		{
			if (map == null || !Active) return false;
			for (int i = 0; i < spots.Count; i++)
			{
				if (map.listerThings.ThingsOfDef(spots[i]).Count > 0) return true;
			}
			return false;
		}
	}
}
