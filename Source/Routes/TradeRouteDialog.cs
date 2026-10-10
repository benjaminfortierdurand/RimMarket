using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMarket.Auction;
using RimMarket.Market;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Routes
{
	public static class TradeRouteDialog
	{
		private const int SettlementChoices = 8;

		public static DiaOption RootOption(Pawn negotiator, Faction faction)
		{
			MarketManager mgr = MarketManager.Instance;
			Map map = negotiator?.Map;
			if (mgr == null || map == null || faction == null) return null;
			var opt = new DiaOption("RimMarket.RoutesOption".Translate(mgr.Routes.Count, TradeRouteUtility.MaxRoutes(mgr)));
			if (faction.HostileTo(Faction.OfPlayer))
			{
				opt.Disable("RimMarket.RouteHostile".Translate());
				return opt;
			}
			if (negotiator.skills != null && negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
			{
				opt.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
				return opt;
			}
			opt.linkLateBind = () => RoutesNode(negotiator, faction);
			return opt;
		}

		private static DiaOption Back(Pawn negotiator, Faction faction)
		{
			return new DiaOption("GoBack".Translate()) { linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator) };
		}

		private static DiaNode RoutesNode(Pawn negotiator, Faction faction)
		{
			MarketManager mgr = MarketManager.Instance;
			Map map = negotiator.Map;
			var text = new StringBuilder();
			text.Append("RimMarket.RoutesNode".Translate(faction.Name, TradeRouteUtility.MaxRoutes(mgr)));
			List<TradeRoute> theirs = mgr.Routes.Where(r => r.faction == faction).ToList();
			for (int i = 0; i < theirs.Count; i++)
			{
				TradeRoute r = theirs[i];
				text.Append("\n\n");
				text.Append("RimMarket.RouteLine".Translate(r.SettlementName, r.kind?.label ?? "?", r.intervalDays, RouteStatus(r)));
			}
			var node = new DiaNode(text.ToString());
			var open = new DiaOption("RimMarket.RouteOpen".Translate());
			string why = TradeRouteUtility.CannotOpen(mgr, faction, map);
			if (why != null) open.Disable(why);
			else open.linkLateBind = () => SettlementNode(negotiator, faction);
			node.options.Add(open);
			for (int i = 0; i < theirs.Count; i++)
			{
				TradeRoute r = theirs[i];
				node.options.Add(new DiaOption("RimMarket.RouteClose".Translate(r.SettlementName)) { link = CloseNode(negotiator, faction, r) });
			}
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static string RouteStatus(TradeRoute r)
		{
			string status;
			if (r.convoy != null && !r.convoy.Destroyed)
			{
				if (r.convoy.UnderAttack) status = "RimMarket.RouteStatusAttacked".Translate(r.convoy.attacker?.Name ?? "?").ToString();
				else if (r.convoy.Waiting) status = "RimMarket.RouteStatusWaiting".Translate().ToString();
				else status = "RimMarket.RouteStatusOnRoad".Translate(r.convoy.TicksLeft.ToStringTicksToPeriod()).ToString();
			}
			else
			{
				int left = r.nextDepartureTick - Find.TickManager.TicksGame;
				status = "RimMarket.RouteStatusNext".Translate(System.Math.Max(0, left).ToStringTicksToPeriod()).ToString();
			}
			if (r.escort) status += " " + "RimMarket.RouteStatusEscort".Translate();
			int toll = TradeRouteUtility.EffectiveToll(r.toll);
			if (toll > 0) status += " " + "RimMarket.RouteStatusToll".Translate(toll);
			if (r.ambushes > 0) status += " " + "RimMarket.RouteStatusAmbushes".Translate(r.ambushes, r.convoysLost);
			return status;
		}

		private static DiaNode CloseNode(Pawn negotiator, Faction faction, TradeRoute r)
		{
			var node = new DiaNode("RimMarket.RouteCloseConfirm".Translate(r.SettlementName));
			var yes = new DiaOption("RimMarket.RouteCloseYes".Translate())
			{
				action = () => TradeRouteUtility.Close(r, null),
				linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator),
			};
			node.options.Add(yes);
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaNode SettlementNode(Pawn negotiator, Faction faction)
		{
			MarketManager mgr = MarketManager.Instance;
			Map map = negotiator.Map;
			PlanetTile home = map.Tile;
			var node = new DiaNode("RimMarket.RoutePickSettlement".Translate(faction.Name));
			List<Settlement> towns = Find.WorldObjects.Settlements
				.Where(s => s.Faction == faction && s.Spawned)
				.OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, home))
				.Take(SettlementChoices)
				.ToList();
			for (int i = 0; i < towns.Count; i++)
			{
				Settlement s = towns[i];
				if (!TradeRouteUtility.TryPath(s.Tile, home, out List<int> nodes, out int ticks))
				{
					var none = new DiaOption(s.LabelCap);
					none.Disable("RimMarket.RouteNoRoad".Translate());
					node.options.Add(none);
					continue;
				}
				int tiles = nodes.Count - 1;
				float risk = TradeRouteUtility.TripRisk(nodes, s.Tile.Layer, faction, false, out Faction threat);
				var opt = new DiaOption("RimMarket.RouteSettlementOption".Translate(s.LabelCap, tiles, ticks.ToStringTicksToPeriod(),
					TradeRouteUtility.RiskText(risk, threat)));
				if (TradeRouteUtility.RouteFrom(mgr, s) != null) opt.Disable("RimMarket.RouteExists".Translate());
				else
				{
					List<int> path = nodes;
					opt.linkLateBind = () => KindNode(negotiator, faction, s, path);
				}
				node.options.Add(opt);
			}
			if (towns.Count == 0) node.text += "\n\n" + "RimMarket.RouteNoTowns".Translate();
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaNode KindNode(Pawn negotiator, Faction faction, Settlement s, List<int> path)
		{
			Map map = negotiator.Map;
			var node = new DiaNode("RimMarket.RoutePickKind".Translate(s.LabelCap));
			List<TraderKindDef> kinds = TradeRouteUtility.KindsFor(faction, map, negotiator);
			for (int i = 0; i < kinds.Count; i++)
			{
				TraderKindDef k = kinds[i];
				node.options.Add(new DiaOption(k.LabelCap) { link = CadenceNode(negotiator, faction, s, path, k) });
			}
			if (kinds.Count == 0) node.text += "\n\n" + "RimMarket.RouteNoKinds".Translate();
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaNode CadenceNode(Pawn negotiator, Faction faction, Settlement s, List<int> path, TraderKindDef kind)
		{
			Map map = negotiator.Map;
			int silver = AuctionUtility.ColonySilver(map);
			int tiles = path.Count - 1;
			float risk = TradeRouteUtility.TripRisk(path, s.Tile.Layer, faction, false, out Faction threat);
			float escorted = TradeRouteUtility.TripRisk(path, s.Tile.Layer, faction, true, out _);
			string text = "RimMarket.RoutePickCadence".Translate(kind.label, s.LabelCap, silver);
			if (risk >= 0.03f) text += "\n\n" + "RimMarket.RouteEscortNote".Translate(risk.ToStringPercent(), threat?.Name ?? "?", escorted.ToStringPercent());
			var node = new DiaNode(text);
			node.options.Add(CadenceOption(negotiator, faction, s, tiles, kind, TradeRouteUtility.Weekly, false, "RimMarket.RouteWeekly", silver));
			node.options.Add(CadenceOption(negotiator, faction, s, tiles, kind, TradeRouteUtility.Weekly, true, "RimMarket.RouteWeeklyEscort", silver));
			node.options.Add(CadenceOption(negotiator, faction, s, tiles, kind, TradeRouteUtility.Fortnightly, false, "RimMarket.RouteFortnightly", silver));
			node.options.Add(CadenceOption(negotiator, faction, s, tiles, kind, TradeRouteUtility.Fortnightly, true, "RimMarket.RouteFortnightlyEscort", silver));
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaOption CadenceOption(Pawn negotiator, Faction faction, Settlement s, int tiles, TraderKindDef kind, int days, bool escort, string key, int silver)
		{
			Map map = negotiator.Map;
			int price = TradeRouteUtility.OpenPrice(tiles);
			int toll = TradeRouteUtility.Toll(tiles, escort);
			var opt = new DiaOption(key.Translate(price, TradeRouteUtility.EffectiveToll(toll)));
			if (silver < price)
			{
				opt.Disable("RimMarket.RouteNoSilver".Translate(price));
				return opt;
			}
			var done = new DiaNode("RimMarket.RouteOpened".Translate(s.LabelCap, kind.label));
			done.options.Add(new DiaOption("OK".Translate()) { linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator) });
			opt.action = () =>
			{
				if (!AuctionUtility.TakeColonySilver(map, price)) return;
				TradeRouteUtility.Open(faction, s, map, kind, days, escort, toll);
			};
			opt.link = done;
			return opt;
		}
	}
}
