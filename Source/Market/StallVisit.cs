using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public class StallVisit : IExposable
	{
		public const int EndNone = 0;
		public const int EndNormal = 1;
		public const int EndClosed = 2;
		public const int EndStallGone = 3;
		public const int EndLordGone = 4;
		public const int EndLeft = 5;
		public const int EndForced = 6;
		public const int EndFeud = 7;

		public int id;
		public Lord lord;
		public Map map;
		public Faction faction;
		public Pawn trader;
		public CompMarketStall stall;
		public int startTick;
		public int endTick;
		public int feeMode;
		public int feeAmount;
		public bool feeDue;
		public bool feePaid;
		public bool feeShort;
		public int feeCollected;
		public float shareOwed;
		public int silverEarned;
		public float colonyBought;
		public float colonySold;
		public bool haggleUsed;
		public bool haggleWon;
		public float haggleBuyFactor = 1f;
		public float haggleSellFactor = 1f;
		public bool closed;
		public int endReason;
		public bool dutiesDirty;
		public float wealthAtArrival;
		public bool tolledToday;
		public bool robbed;
		public int browsers;
		public bool auctionHeld;
		public bool auctionNoted;
		public int shoppersDueTick = -1;
		public bool shoppersSent;
		public int innEarned;
		public int innNights;
		public int innComfort;
		public bool innSteep;
		public bool feud;
		public string feudWith;
		public int routeId;
		public int orderId;
		public List<Pawn> posted = new List<Pawn>();
		public List<int> postedSince = new List<int>();
		public Pawn reliefIn;
		public int reliefSlot = -1;
		public int reliefSince = -1;
		public int nextReliefTick;
		public float repPriceBonus;
		public List<float> repDeltas = new List<float> { 0f, 0f, 0f, 0f };
		public List<string> reasonKeys = new List<string>();
		public List<float> reasonValues = new List<float>();

		public void AddReason(int axis, float points, string key)
		{
			if (points == 0f) return;
			if (axis >= 0 && axis < repDeltas.Count) repDeltas[axis] += points;
			if (key.NullOrEmpty()) return;
			int i = reasonKeys.IndexOf(key);
			if (i >= 0)
			{
				reasonValues[i] += points;
				return;
			}
			reasonKeys.Add(key);
			reasonValues.Add(points);
		}

		public float RepTotal
		{
			get
			{
				float t = 0f;
				for (int i = 0; i < repDeltas.Count; i++) t += repDeltas[i];
				return t;
			}
		}

		public bool LordAlive => lord != null && lord.ownedPawns.Count > 0;

		public bool StallOk => stall != null && stall.Spawned && !stall.Destroyed;

		public bool TraderOk => trader != null && trader.Spawned && !trader.Dead && !trader.Downed && trader.GetLord() == lord;

		public int TicksLeft => Mathf.Max(0, endTick - Find.TickManager.TicksGame);

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_References.Look(ref lord, "lord");
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref trader, "trader");
			Thing stallThing = stall?.parent;
			Scribe_References.Look(ref stallThing, "stall");
			if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs) stall = MarketStallDefs.StallOf(stallThing);
			Scribe_Values.Look(ref startTick, "startTick");
			Scribe_Values.Look(ref endTick, "endTick");
			Scribe_Values.Look(ref feeMode, "feeMode");
			Scribe_Values.Look(ref feeAmount, "feeAmount");
			Scribe_Values.Look(ref feeDue, "feeDue");
			Scribe_Values.Look(ref feePaid, "feePaid");
			Scribe_Values.Look(ref feeShort, "feeShort");
			Scribe_Values.Look(ref feeCollected, "feeCollected");
			Scribe_Values.Look(ref colonyBought, "colonyBought");
			Scribe_Values.Look(ref colonySold, "colonySold");
			Scribe_Values.Look(ref shareOwed, "shareOwed");
			Scribe_Values.Look(ref silverEarned, "silverEarned");
			Scribe_Values.Look(ref haggleUsed, "haggleUsed");
			Scribe_Values.Look(ref haggleWon, "haggleWon");
			Scribe_Values.Look(ref haggleBuyFactor, "haggleBuyFactor", 1f);
			Scribe_Values.Look(ref haggleSellFactor, "haggleSellFactor", 1f);
			Scribe_Values.Look(ref closed, "closed");
			Scribe_Values.Look(ref endReason, "endReason");
			Scribe_Values.Look(ref dutiesDirty, "dutiesDirty");
			Scribe_Values.Look(ref wealthAtArrival, "wealthAtArrival");
			Scribe_Values.Look(ref tolledToday, "tolledToday");
			Scribe_Values.Look(ref robbed, "robbed");
			Scribe_Values.Look(ref browsers, "browsers");
			Scribe_Values.Look(ref auctionHeld, "auctionHeld");
			Scribe_Values.Look(ref auctionNoted, "auctionNoted");
			Scribe_Values.Look(ref shoppersDueTick, "shoppersDueTick", -1);
			Scribe_Values.Look(ref shoppersSent, "shoppersSent");
			Scribe_Values.Look(ref innEarned, "innEarned");
			Scribe_Values.Look(ref innNights, "innNights");
			Scribe_Values.Look(ref innComfort, "innComfort");
			Scribe_Values.Look(ref innSteep, "innSteep");
			Scribe_Values.Look(ref feud, "feud");
			Scribe_Values.Look(ref feudWith, "feudWith");
			Scribe_Values.Look(ref routeId, "routeId");
			Scribe_Values.Look(ref orderId, "orderId");
			Scribe_Collections.Look(ref posted, "posted", LookMode.Reference);
			Scribe_Collections.Look(ref postedSince, "postedSince", LookMode.Value);
			Scribe_References.Look(ref reliefIn, "reliefIn");
			Scribe_Values.Look(ref reliefSlot, "reliefSlot", -1);
			Scribe_Values.Look(ref reliefSince, "reliefSince", -1);
			Scribe_Values.Look(ref nextReliefTick, "nextReliefTick");
			Scribe_Values.Look(ref repPriceBonus, "repPriceBonus");
			Scribe_Collections.Look(ref repDeltas, "repDeltas", LookMode.Value);
			Scribe_Collections.Look(ref reasonKeys, "reasonKeys", LookMode.Value);
			Scribe_Collections.Look(ref reasonValues, "reasonValues", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (repDeltas == null) repDeltas = new List<float>();
				while (repDeltas.Count < 4) repDeltas.Add(0f);
				if (reasonKeys == null) reasonKeys = new List<string>();
				if (reasonValues == null) reasonValues = new List<float>();
				if (reasonValues.Count != reasonKeys.Count)
				{
					reasonKeys.Clear();
					reasonValues.Clear();
				}
				if (posted == null) posted = new List<Pawn>();
				if (postedSince == null) postedSince = new List<int>();
				if (posted.Count != postedSince.Count)
				{
					posted.Clear();
					postedSince.Clear();
				}
				if (reliefSlot < 0 || reliefSlot >= posted.Count)
				{
					reliefIn = null;
					reliefSlot = -1;
				}
			}
		}
	}
}
