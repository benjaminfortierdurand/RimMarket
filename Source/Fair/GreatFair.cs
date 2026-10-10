using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using Verse;

namespace RimMarket.Fair
{
	public class GreatFair : IExposable
	{
		public const int Announced = 0;
		public const int Running = 1;
		public const int Done = 2;

		public Map map;
		public Building_MarketMarker marker;
		public int state;
		public int announcedTick;
		public int startTick;
		public int endTick;
		public int caravansDue;
		public int caravansSent;
		public int nextCaravanTick = -1;
		public int nextWaveTick = -1;
		public int waves;
		public List<Faction> factions = new List<Faction>();
		public int feesAtStart;
		public int guestSalesAtStart;
		public int colonySalesAtStart;
		public int caughtAtStart;
		public int escapedAtStart;
		public bool judged;
		public Thing winnerStall;
		public Faction winnerFaction;
		public string winnerName;
		public int winnerScore;
		public Dictionary<int, int> visitBaselines = new Dictionary<int, int>();
		public Dictionary<int, int> colonyBaselines = new Dictionary<int, int>();
		private List<int> visitKeys;
		private List<int> visitValues;
		private List<int> colonyKeys;
		private List<int> colonyValues;

		public void ExposeData()
		{
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref marker, "marker");
			Scribe_Values.Look(ref state, "state");
			Scribe_Values.Look(ref announcedTick, "announcedTick");
			Scribe_Values.Look(ref startTick, "startTick");
			Scribe_Values.Look(ref endTick, "endTick");
			Scribe_Values.Look(ref caravansDue, "caravansDue");
			Scribe_Values.Look(ref caravansSent, "caravansSent");
			Scribe_Values.Look(ref nextCaravanTick, "nextCaravanTick", -1);
			Scribe_Values.Look(ref nextWaveTick, "nextWaveTick", -1);
			Scribe_Values.Look(ref waves, "waves");
			Scribe_Collections.Look(ref factions, "factions", LookMode.Reference);
			Scribe_Values.Look(ref feesAtStart, "feesAtStart");
			Scribe_Values.Look(ref guestSalesAtStart, "guestSalesAtStart");
			Scribe_Values.Look(ref colonySalesAtStart, "colonySalesAtStart");
			Scribe_Values.Look(ref caughtAtStart, "caughtAtStart");
			Scribe_Values.Look(ref escapedAtStart, "escapedAtStart");
			Scribe_Values.Look(ref judged, "judged");
			Scribe_References.Look(ref winnerStall, "winnerStall");
			Scribe_References.Look(ref winnerFaction, "winnerFaction");
			Scribe_Values.Look(ref winnerName, "winnerName");
			Scribe_Values.Look(ref winnerScore, "winnerScore");
			Scribe_Collections.Look(ref visitBaselines, "visitBaselines", LookMode.Value, LookMode.Value, ref visitKeys, ref visitValues);
			Scribe_Collections.Look(ref colonyBaselines, "colonyBaselines", LookMode.Value, LookMode.Value, ref colonyKeys, ref colonyValues);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (visitBaselines == null) visitBaselines = new Dictionary<int, int>();
				if (colonyBaselines == null) colonyBaselines = new Dictionary<int, int>();
				if (factions == null) factions = new List<Faction>();
				factions.RemoveAll(f => f == null);
			}
		}
	}
}
