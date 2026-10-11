using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.UI;
using RimWorld;
using Verse;

namespace RimMarket.Buildings
{
	public class CompProperties_MarketDeals : CompProperties
	{
		public CompProperties_MarketDeals()
		{
			compClass = typeof(CompMarketDeals);
		}
	}

	public class CompMarketDeals : ThingComp
	{
		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
			if (parent.Faction != Faction.OfPlayer) yield break;
			int n = Window_MarketDeals.ActiveCount(MarketManager.Instance);
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.DealsGizmo".Translate(n),
				defaultDesc = "RimMarket.DealsGizmoDesc".Translate(),
				icon = RimMarketIcons.Deals,
				action = Window_MarketDeals.Open,
			};
		}
	}
}
