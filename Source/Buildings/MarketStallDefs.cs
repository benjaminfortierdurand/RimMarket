using System.Collections.Generic;
using Verse;

namespace RimMarket.Buildings
{
	[StaticConstructorOnStartup]
	public static class MarketStallDefs
	{
		public static readonly List<ThingDef> All = new List<ThingDef>();

		static MarketStallDefs()
		{
			foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (def.GetCompProperties<CompProperties_MarketStall>() != null) All.Add(def);
			}
		}

		public static CompMarketStall StallOf(Thing t)
		{
			return (t as ThingWithComps)?.GetComp<CompMarketStall>();
		}
	}
}
