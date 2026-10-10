using RimWorld;
using Verse;

namespace RimMarket.Thieves
{
	public class MarketTheft : IExposable
	{
		public Pawn thief;
		public Thing loot;
		public string lootLabel;
		public int visitId = -1;
		public Faction merchantFaction;
		public Map map;
		public int startTick;

		public void ExposeData()
		{
			Scribe_References.Look(ref thief, "thief");
			Scribe_References.Look(ref loot, "loot");
			Scribe_Values.Look(ref lootLabel, "lootLabel");
			Scribe_Values.Look(ref visitId, "visitId", -1);
			Scribe_References.Look(ref merchantFaction, "merchantFaction");
			Scribe_References.Look(ref map, "map");
			Scribe_Values.Look(ref startTick, "startTick");
		}
	}
}
