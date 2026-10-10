using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimMarket.Reputation
{
	public class MarketLogEntry : IExposable
	{
		public int tick;
		public Faction faction;
		public string factionName;
		public string traderName;
		public int verdict;
		public int colonyBought;
		public int colonySold;
		public int guestSales;
		public int fees;
		public List<float> deltas = new List<float> { 0f, 0f, 0f, 0f };
		public List<string> reasons = new List<string>();

		public void ExposeData()
		{
			Scribe_Values.Look(ref tick, "tick");
			Scribe_References.Look(ref faction, "faction");
			Scribe_Values.Look(ref factionName, "factionName");
			Scribe_Values.Look(ref traderName, "traderName");
			Scribe_Values.Look(ref verdict, "verdict");
			Scribe_Values.Look(ref colonyBought, "colonyBought");
			Scribe_Values.Look(ref colonySold, "colonySold");
			Scribe_Values.Look(ref guestSales, "guestSales");
			Scribe_Values.Look(ref fees, "fees");
			Scribe_Collections.Look(ref deltas, "deltas", LookMode.Value);
			Scribe_Collections.Look(ref reasons, "reasons", LookMode.Value);
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (deltas == null) deltas = new List<float>();
			while (deltas.Count < MarketReputation.AxisCount) deltas.Add(0f);
			if (reasons == null) reasons = new List<string>();
		}
	}
}
