using System.Collections.Generic;
using RimMarket.UI;
using Verse;

namespace RimMarket.Buildings
{
	public class Building_MarketSpot : Building
	{
		private bool spotHidden;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref spotHidden, "spotHidden");
		}

		public override void Print(SectionLayer layer)
		{
			if (spotHidden) return;
			base.Print(layer);
		}

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			if (!spotHidden) return s;
			string hidden = "RimMarket.SpotHidden".Translate();
			return s.NullOrEmpty() ? hidden : s + "\n" + hidden;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			if (Faction == RimWorld.Faction.OfPlayer) RimWorld.LessonAutoActivator.TeachOpportunity(RimMarketDefOf.RimMarket_Concept_Spots, RimWorld.OpportunityType.GoodToKnow);
			yield return new Command_Toggle
			{
				defaultLabel = "RimMarket.ShowSpotLabel".Translate(),
				defaultDesc = "RimMarket.ShowSpotDesc".Translate(),
				icon = RimMarketIcons.ShowMarker,
				isActive = () => !spotHidden,
				toggleAction = delegate
				{
					spotHidden = !spotHidden;
					if (Spawned) DirtyMapMesh(Map);
				},
			};
		}
	}
}
