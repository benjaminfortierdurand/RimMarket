using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.UI;
using RimWorld;
using Verse;

namespace RimMarket.Auction
{
	public class Building_AuctionBlock : Building
	{
		private SlaveAuction Auction => MarketManager.Instance?.AuctionAt(this);

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			var lines = new List<string>();
			if (!s.NullOrEmpty()) lines.Add(s);
			SlaveAuction a = Auction;
			if (a == null)
			{
				lines.Add("RimMarket.AuctionIdle".Translate());
				if (Spawned && MarketUtility.MarkerAt(Map, Position) == null) lines.Add("RimMarket.StallNoMarker".Translate());
			}
			else if (!a.LotRunning)
			{
				int wait = a.nextLotTick - Find.TickManager.TicksGame;
				lines.Add("RimMarket.AuctionNextLot".Translate((wait > 0 ? wait : 0).ToStringTicksToPeriod()));
			}
			else
			{
				lines.Add("RimMarket.AuctionLotLine".Translate(a.lotNumber, a.lot.LabelShortCap, UnityEngine.Mathf.RoundToInt(a.lot.MarketValue)));
				lines.Add("RimMarket.AuctionBidLine".Translate(a.price, LeaderLabel(a),
					(a.lotEndTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()));
				lines.Add("RimMarket.AuctionColonySilver".Translate(AuctionUtility.ColonySilver(Map)));
			}
			return string.Join("\n", lines);
		}

		public static string LeaderLabel(SlaveAuction a)
		{
			switch (a.leader)
			{
				case SlaveAuction.LeaderPlayer: return "RimMarket.AuctionLeaderYou".Translate();
				case SlaveAuction.LeaderBidder: return a.Leader?.name ?? "?";
				default: return "RimMarket.AuctionLeaderNone".Translate();
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			SlaveAuction a = Auction;
			if (a == null || !a.LotRunning) yield break;
			int next = AuctionUtility.NextBid(a);
			var bid = new Command_Action
			{
				defaultLabel = "RimMarket.AuctionBidLabel".Translate(next),
				defaultDesc = "RimMarket.AuctionBidDesc".Translate(a.lot.LabelShortCap, next),
				icon = RimMarketIcons.Bid,
				action = () => MarketManager.Instance?.PlayerBid(a),
			};
			if (a.leader == SlaveAuction.LeaderPlayer) bid.Disable("RimMarket.AuctionYouLead".Translate());
			else if (AuctionUtility.ColonySilver(Map) < next) bid.Disable("RimMarket.AuctionNotEnoughSilver".Translate(next));
			yield return bid;
		}
	}
}
