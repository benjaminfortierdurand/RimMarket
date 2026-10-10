using RimWorld;
using Verse;

namespace RimMarket.Reputation
{
	public class FactionLedger : IExposable
	{
		public Faction faction;
		public MarketReputation rep = new MarketReputation();

		public void ExposeData()
		{
			Scribe_References.Look(ref faction, "faction");
			Scribe_Deep.Look(ref rep, "rep");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && rep == null) rep = new MarketReputation();
		}
	}
}
