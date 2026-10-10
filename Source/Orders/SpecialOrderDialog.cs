using System.Collections.Generic;
using System.Linq;
using RimMarket.Auction;
using RimMarket.Market;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Orders
{
	public static class SpecialOrderDialog
	{
		private const int SettlementChoices = 8;

		public static DiaOption RootOption(Pawn negotiator, Faction faction)
		{
			MarketManager mgr = MarketManager.Instance;
			Map map = negotiator?.Map;
			if (mgr == null || map == null || faction == null) return null;
			var opt = new DiaOption("RimMarket.OrderOption".Translate());
			if (negotiator.skills != null && negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
			{
				opt.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
				return opt;
			}
			SpecialOrder pending = SpecialOrderUtility.OrderFrom(mgr, faction);
			if (pending != null)
			{
				opt.linkLateBind = () => PendingNode(negotiator, faction, pending);
				return opt;
			}
			string why = SpecialOrderUtility.CannotOrder(mgr, faction, map);
			if (why != null)
			{
				opt.Disable(why);
				return opt;
			}
			opt.linkLateBind = () => SettlementNode(negotiator, faction);
			return opt;
		}

		private static DiaOption Back(Pawn negotiator, Faction faction)
		{
			return new DiaOption("GoBack".Translate()) { linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator) };
		}

		private static DiaNode PendingNode(Pawn negotiator, Faction faction, SpecialOrder o)
		{
			string status;
			if (o.convoy != null && !o.convoy.Destroyed)
			{
				if (o.convoy.UnderAttack) status = "RimMarket.RouteStatusAttacked".Translate(o.convoy.attacker?.Name ?? "?").ToString();
				else if (o.convoy.Waiting) status = "RimMarket.RouteStatusWaiting".Translate().ToString();
				else status = "RimMarket.RouteStatusOnRoad".Translate(o.convoy.TicksLeft.ToStringTicksToPeriod()).ToString();
			}
			else
			{
				int left = System.Math.Max(0, o.departTick - Find.TickManager.TicksGame);
				status = "RimMarket.OrderStatusPacking".Translate(left.ToStringTicksToPeriod()).ToString();
			}
			var node = new DiaNode("RimMarket.OrderPendingNode".Translate(o.SettlementName, SpecialOrderUtility.What(o), o.paid, status));
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaNode SettlementNode(Pawn negotiator, Faction faction)
		{
			Map map = negotiator.Map;
			PlanetTile home = map.Tile;
			var node = new DiaNode("RimMarket.OrderPickSettlement".Translate(faction.Name));
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
				opt.linkLateBind = () => WhatNode(negotiator, faction, s, tiles);
				node.options.Add(opt);
			}
			if (towns.Count == 0) node.text += "\n\n" + "RimMarket.RouteNoTowns".Translate();
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaNode WhatNode(Pawn negotiator, Faction faction, Settlement s, int tiles)
		{
			Map map = negotiator.Map;
			int silver = AuctionUtility.ColonySilver(map);
			var node = new DiaNode("RimMarket.OrderPickWhat".Translate(s.LabelCap, silver));
			for (int i = 0; i < SpecialOrderUtility.Families.Length; i++)
			{
				SpecialOrderUtility.OrderFamily fam = SpecialOrderUtility.Families[i];
				node.options.Add(FamilyOption(negotiator, faction, s, tiles, fam, false, silver));
				node.options.Add(FamilyOption(negotiator, faction, s, tiles, fam, true, silver));
			}
			node.options.Add(Back(negotiator, faction));
			return node;
		}

		private static DiaOption FamilyOption(Pawn negotiator, Faction faction, Settlement s, int tiles, SpecialOrderUtility.OrderFamily fam, bool large, int silver)
		{
			Map map = negotiator.Map;
			int price = SpecialOrderUtility.FamilyPrice(tiles, large);
			var opt = new DiaOption((large ? "RimMarket.OrderFamilyLarge" : "RimMarket.OrderFamilySmall").Translate(fam.Label, price));
			if (SpecialOrderUtility.Candidates(fam, faction).Count == 0)
			{
				opt.Disable("RimMarket.OrderFamilyNothing".Translate(faction.Name));
				return opt;
			}
			if (silver < price)
			{
				opt.Disable("RimMarket.RouteNoSilver".Translate(price));
				return opt;
			}
			var done = new DiaNode("RimMarket.OrderPlaced".Translate(s.LabelCap, (large ? "RimMarket.OrderWhatLarge" : "RimMarket.OrderWhatSmall").Translate(fam.Label), price));
			done.options.Add(new DiaOption("OK".Translate()) { linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator) });
			opt.action = () =>
			{
				if (!AuctionUtility.TakeColonySilver(map, price)) return;
				SpecialOrderUtility.Place(faction, s, map, fam, large, null, price);
			};
			opt.link = done;
			return opt;
		}
	}
}
