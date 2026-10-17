using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Routes
{
	public class TradeRoute : IExposable
	{
		public int id;
		public Faction faction;
		public Settlement settlement;
		public MapParent home;
		public TraderKindDef kind;
		public int intervalDays = 14;
		public int nextDepartureTick;
		public TradeConvoy convoy;
		public int openedTick;
		public int convoysSent;
		public int goodVisits;
		public int strikes;
		public int pathFailures;
		public int arrivedLordId = -1;
		public int arrivedTick = -1;
		public bool escort;
		public int ambushes;
		public int convoysLost;
		public int toll;
		public bool freeVisit;

		public string SettlementName => settlement?.LabelCap ?? "?";

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref settlement, "settlement");
			Scribe_References.Look(ref home, "home");
			Scribe_Defs.Look(ref kind, "kind");
			Scribe_Values.Look(ref intervalDays, "intervalDays", 14);
			Scribe_Values.Look(ref nextDepartureTick, "nextDepartureTick");
			Scribe_References.Look(ref convoy, "convoy");
			Scribe_Values.Look(ref openedTick, "openedTick");
			Scribe_Values.Look(ref convoysSent, "convoysSent");
			Scribe_Values.Look(ref goodVisits, "goodVisits");
			Scribe_Values.Look(ref strikes, "strikes");
			Scribe_Values.Look(ref pathFailures, "pathFailures");
			Scribe_Values.Look(ref arrivedLordId, "arrivedLordId", -1);
			Scribe_Values.Look(ref arrivedTick, "arrivedTick", -1);
			Scribe_Values.Look(ref escort, "escort");
			Scribe_Values.Look(ref ambushes, "ambushes");
			Scribe_Values.Look(ref convoysLost, "convoysLost");
			Scribe_Values.Look(ref toll, "toll");
			Scribe_Values.Look(ref freeVisit, "freeVisit");
		}
	}
}
