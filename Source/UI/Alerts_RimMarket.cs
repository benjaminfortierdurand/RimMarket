using System.Collections.Generic;
using System.Text;
using RimMarket.Fair;
using RimMarket.Market;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.UI
{
	public class Alert_CaravanNeedsStall : Alert
	{
		private readonly List<Pawn> traders = new List<Pawn>();
		private readonly List<string> reasons = new List<string>();

		public Alert_CaravanNeedsStall()
		{
			defaultPriority = AlertPriority.Medium;
		}

		public override string GetLabel()
		{
			return "RimMarket.AlertNoStall".Translate();
		}

		public override TaggedString GetExplanation()
		{
			var sb = new StringBuilder();
			for (int i = 0; i < traders.Count; i++)
			{
				sb.AppendLine("  - " + (traders[i].Faction?.Name ?? traders[i].LabelShortCap) + ": " + reasons[i].Translate());
			}
			return "RimMarket.AlertNoStallDesc".Translate(sb.ToString().TrimEndNewlines());
		}

		public override AlertReport GetReport()
		{
			traders.Clear();
			reasons.Clear();
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || !mgr.AnyWaiting) return false;
			List<Map> maps = Find.Maps;
			for (int m = 0; m < maps.Count; m++)
			{
				List<Lord> lords = maps[m].lordManager.lords;
				for (int i = 0; i < lords.Count; i++)
				{
					Lord lord = lords[i];
					if (!(lord.LordJob is LordJob_TradeWithColony)) continue;
					if (!mgr.WaitingForStall(lord, out string reason) || reason == null) continue;
					Pawn trader = TraderCaravanUtility.FindTrader(lord);
					if (trader == null || !trader.Spawned) continue;
					traders.Add(trader);
					reasons.Add(reason);
				}
			}
			return AlertReport.CulpritsAre(traders);
		}
	}

	public class Alert_MarketThief : Alert
	{
		private readonly List<Pawn> thieves = new List<Pawn>();
		private readonly List<string> loot = new List<string>();

		public Alert_MarketThief()
		{
			defaultPriority = AlertPriority.High;
		}

		public override string GetLabel()
		{
			return "RimMarket.AlertThief".Translate();
		}

		public override TaggedString GetExplanation()
		{
			var sb = new StringBuilder();
			for (int i = 0; i < thieves.Count; i++)
			{
				sb.AppendLine("  - " + thieves[i].LabelShortCap + " (" + loot[i] + ")");
			}
			return "RimMarket.AlertThiefDesc".Translate(sb.ToString().TrimEndNewlines());
		}

		public override AlertReport GetReport()
		{
			thieves.Clear();
			loot.Clear();
			if (!MarketManager.AnyThefts) return false;
			IReadOnlyList<MarketTheft> thefts = MarketManager.Instance.Thefts;
			for (int i = 0; i < thefts.Count; i++)
			{
				Pawn th = thefts[i].thief;
				if (th == null || !th.Spawned || th.Dead || th.Downed) continue;
				thieves.Add(th);
				loot.Add(thefts[i].lootLabel ?? "?");
			}
			return AlertReport.CulpritsAre(thieves);
		}
	}

	public class Alert_WantedOffers : Alert
	{
		public Alert_WantedOffers()
		{
			defaultPriority = AlertPriority.Medium;
		}

		private static Orders.WantedNotice Notice => MarketManager.Instance?.Notice;

		public override string GetLabel()
		{
			return "RimMarket.AlertWantedOffers".Translate(Notice?.offers.Count ?? 0);
		}

		public override TaggedString GetExplanation()
		{
			return "RimMarket.AlertWantedOffersDesc".Translate();
		}

		public override AlertReport GetReport()
		{
			Orders.WantedNotice n = Notice;
			return n != null && n.offers.Count > 0;
		}

		protected override void OnClick()
		{
			if (Notice != null) Find.WindowStack.Add(new Orders.Dialog_WantedBoard());
		}
	}

	public class Alert_GreatFair : Alert
	{
		private GreatFair fair;

		public Alert_GreatFair()
		{
			defaultPriority = AlertPriority.Medium;
		}

		public override string GetLabel()
		{
			if (fair == null) return "";
			if (fair.state == GreatFair.Running) return "RimMarket.AlertFairToday".Translate();
			return "RimMarket.AlertFairSoon".Translate(System.Math.Max(0, fair.startTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
		}

		public override TaggedString GetExplanation()
		{
			if (fair == null) return "";
			if (fair.state == GreatFair.Running) return "RimMarket.AlertFairTodayDesc".Translate(FairUtility.CloseHour);
			return "RimMarket.AlertFairSoonDesc".Translate(FairUtility.DateOf(fair.startTick, fair.map), FairUtility.OpenHour);
		}

		public override AlertReport GetReport()
		{
			fair = null;
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || !mgr.AnyFair) return false;
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				GreatFair f = mgr.FairOn(maps[i]);
				if (f == null || f.state == GreatFair.Done) continue;
				fair = f;
				return f.marker != null && f.marker.Spawned ? AlertReport.CulpritIs(f.marker) : AlertReport.Active;
			}
			return false;
		}
	}
}
