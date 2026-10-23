using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Away
{
	public class AwayStall : IExposable
	{
		public const int EndNormal = 0;
		public const int EndPackedUp = 1;
		public const int EndMoved = 2;
		public const int EndSoldOut = 3;
		public const int EndLost = 4;

		public const int EventNone = 0;
		public const int EventRival = 1;
		public const int EventFestival = 2;

		public Caravan caravan;
		public Settlement settlement;
		public bool ally;
		public int startTick;
		public int endTick;
		public int nextHourTick;
		public float gross;
		public float fees;
		public float pendingSilver;
		public int itemsSold;
		public int eventKind;
		public int eventUntil = -1;
		public List<ThingDef> keep = new List<ThingDef>();

		public Faction Host => settlement?.Faction;

		public int TicksLeft => Mathf.Max(0, endTick - Find.TickManager.TicksGame);

		public bool EventActive => eventKind != EventNone && Find.TickManager.TicksGame < eventUntil;

		public void ExposeData()
		{
			Scribe_References.Look(ref caravan, "caravan");
			Scribe_References.Look(ref settlement, "settlement");
			Scribe_Values.Look(ref ally, "ally");
			Scribe_Values.Look(ref startTick, "startTick");
			Scribe_Values.Look(ref endTick, "endTick");
			Scribe_Values.Look(ref nextHourTick, "nextHourTick");
			Scribe_Values.Look(ref gross, "gross");
			Scribe_Values.Look(ref fees, "fees");
			Scribe_Values.Look(ref pendingSilver, "pendingSilver");
			Scribe_Values.Look(ref itemsSold, "itemsSold");
			Scribe_Values.Look(ref eventKind, "eventKind");
			Scribe_Values.Look(ref eventUntil, "eventUntil", -1);
			Scribe_Collections.Look(ref keep, "keep", LookMode.Def);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && keep == null) keep = new List<ThingDef>();
		}
	}
}
