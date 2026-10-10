using System.Collections.Generic;
using Verse;

namespace RimMarket.Auction
{
	public class AuctionBidder : IExposable
	{
		public string name;
		public int max;
		public Pawn pawn;

		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref max, "max");
			Scribe_References.Look(ref pawn, "pawn");
		}
	}

	public class SlaveAuction : IExposable
	{
		public const int StateWaiting = 0;
		public const int StateLot = 1;

		public const int LeaderNone = 0;
		public const int LeaderPlayer = 1;
		public const int LeaderBidder = 2;

		public int visitId = -1;
		public Building_AuctionBlock block;
		public int state;
		public int nextLotTick;
		public int lotNumber;
		public Pawn lot;
		public int price;
		public int lotEndTick;
		public int leader;
		public int leaderIndex = -1;
		public bool moodGiven;
		public List<AuctionBidder> bidders = new List<AuctionBidder>();
		public List<Pawn> done = new List<Pawn>();

		public bool LotRunning => state == StateLot && lot != null;

		public AuctionBidder Leader => leader == LeaderBidder && leaderIndex >= 0 && leaderIndex < bidders.Count ? bidders[leaderIndex] : null;

		public void ExposeData()
		{
			Scribe_Values.Look(ref visitId, "visitId", -1);
			Scribe_References.Look(ref block, "block");
			Scribe_Values.Look(ref state, "state");
			Scribe_Values.Look(ref nextLotTick, "nextLotTick");
			Scribe_Values.Look(ref lotNumber, "lotNumber");
			Scribe_References.Look(ref lot, "lot");
			Scribe_Values.Look(ref price, "price");
			Scribe_Values.Look(ref lotEndTick, "lotEndTick");
			Scribe_Values.Look(ref leader, "leader");
			Scribe_Values.Look(ref leaderIndex, "leaderIndex", -1);
			Scribe_Values.Look(ref moodGiven, "moodGiven");
			Scribe_Collections.Look(ref bidders, "bidders", LookMode.Deep);
			Scribe_Collections.Look(ref done, "done", LookMode.Reference);
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (bidders == null) bidders = new List<AuctionBidder>();
			bidders.RemoveAll(b => b == null);
			if (done == null) done = new List<Pawn>();
			done.RemoveAll(p => p == null);
		}
	}
}
