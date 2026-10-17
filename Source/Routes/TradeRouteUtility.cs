using System.Collections.Generic;
using RimMarket.Auction;
using RimMarket.Compat;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Routes
{
	public static class TradeRouteUtility
	{
		public const int Weekly = 7;
		public const int Fortnightly = 14;
		public const int MaxStrikes = 3;
		private const int TicksPerDay = 60000;
		private const int FirstDepartureMin = 20000;
		private const int FirstDepartureMax = 70000;
		private const int GiveUpTicks = 60000;
		private const int MaxPathFailures = 3;
		private const int BasePrice = 300;
		private const int OpenBase = 500;
		private const float TollFactor = 0.6f;
		private const int PricePerTile = 5;
		private const float WeeklyFactor = 1.75f;
		private const int GoodVisitGoodwill = 1;
		private const float NearEnemyRadius = 3f;
		private const float NearEnemyChance = 0.06f;
		private const float PirateChance = 0.08f;
		private const float TerritoryChance = 0.05f;
		private const float BothBonus = 0.02f;
		private const float RoadChance = 0.004f;
		private const float EscortFactor = 0.35f;
		private const float EscortPriceFactor = 1.5f;
		private const int AmbushMinTicks = 7500;
		private const int AmbushMaxTicks = 15000;
		private static readonly float[] OutcomeOdds = { 0.5f, 0.35f, 0.15f };
		private static readonly float[] EscortOutcomeOdds = { 0.75f, 0.2f, 0.05f };
		private const float PlunderMin = 0.3f;
		private const float PlunderMax = 0.6f;

		private static MarketManager Mgr => MarketManager.Instance;

		private static bool ShowMessages => RimMarketMod.Settings.showMessages;

		public static int MaxRoutes(MarketManager mgr)
		{
			int h = mgr?.Reputation?.Headline ?? MarketReputation.HeadlineUnknown;
			int n = 1;
			if (h >= MarketReputation.HeadlineRespectable) n++;
			if (h >= MarketReputation.HeadlineRenowned) n++;
			return n;
		}

		public static int Price(int tiles, int intervalDays, bool escort)
		{
			float price = BasePrice + PricePerTile * Mathf.Max(0, tiles);
			if (intervalDays < Fortnightly) price *= WeeklyFactor;
			if (escort) price *= EscortPriceFactor;
			return 10 * Mathf.RoundToInt(price / 10f);
		}

		public static int OpenPrice(int tiles)
		{
			return 10 * Mathf.RoundToInt((OpenBase + PricePerTile * Mathf.Max(0, tiles)) * RimMarketMod.Settings.routeOpenFactor / 10f);
		}

		public static int Toll(int tiles, bool escort)
		{
			float toll = (BasePrice + PricePerTile * Mathf.Max(0, tiles)) * TollFactor;
			if (escort) toll *= EscortPriceFactor;
			return 10 * Mathf.RoundToInt(toll / 10f);
		}

		public static int EffectiveToll(int toll)
		{
			return 10 * Mathf.RoundToInt(toll * RimMarketMod.Settings.routeTollFactor / 10f);
		}

		public static float DangerAt(PlanetTile tile, Faction traveller, out Faction attacker)
		{
			attacker = null;
			float best = 0f;
			if (!tile.Valid || traveller == null) return 0f;
			List<Settlement> towns = Find.WorldObjects.Settlements;
			for (int i = 0; i < towns.Count; i++)
			{
				Settlement s = towns[i];
				Faction f = s.Faction;
				if (f == null || f == traveller || f.IsPlayer || f.defeated || !f.HostileTo(traveller)) continue;
				if (s.Tile.Layer != tile.Layer || Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile) > NearEnemyRadius) continue;
				float c = f.def.permanentEnemy ? PirateChance : NearEnemyChance;
				if (c <= best) continue;
				best = c;
				attacker = f;
			}
			Faction claimant = TerritoriesBridge.HostileClaimant(tile, traveller);
			if (claimant != null)
			{
				if (attacker == null)
				{
					best = TerritoryChance;
					attacker = claimant;
				}
				else best += BothBonus;
			}
			return best;
		}

		public static Faction RoadBandits(Faction traveller)
		{
			Faction pick = null;
			List<Faction> all = Find.FactionManager.AllFactionsListForReading;
			for (int i = 0; i < all.Count; i++)
			{
				Faction f = all[i];
				if (f == traveller || f.IsPlayer || f.defeated || f.Hidden || !f.def.humanlikeFaction || !f.HostileTo(traveller)) continue;
				if (pick == null || (f.def.permanentEnemy && !pick.def.permanentEnemy)) pick = f;
			}
			return pick;
		}

		public static float ChanceAt(PlanetTile tile, Faction traveller, bool escort, out Faction attacker)
		{
			float c = DangerAt(tile, traveller, out attacker);
			if (attacker == null)
			{
				attacker = RoadBandits(traveller);
				c = attacker == null ? 0f : RoadChance;
			}
			return escort ? c * EscortFactor : c;
		}

		public static float TripRisk(List<int> nodes, PlanetLayer layer, Faction traveller, bool escort, out Faction threat)
		{
			threat = null;
			if (nodes == null || nodes.Count < 3 || layer == null) return 0f;
			float keep = 1f;
			float worst = 0f;
			for (int i = 1; i < nodes.Count - 1; i++)
			{
				float c = ChanceAt(new PlanetTile(nodes[i], layer), traveller, escort, out Faction attacker);
				keep *= 1f - c;
				if (c > worst)
				{
					worst = c;
					threat = attacker;
				}
			}
			return 1f - keep;
		}

		public static string RiskText(float risk, Faction threat)
		{
			if (risk < 0.03f) return "RimMarket.RouteRiskSafe".Translate();
			return "RimMarket.RouteRisk".Translate(risk.ToStringPercent(), threat?.Name ?? "?");
		}

		public static bool RollAmbush(TradeConvoy convoy, PlanetTile tile, out Faction attacker)
		{
			attacker = null;
			if (convoy?.Faction == null) return false;
			float c = ChanceAt(tile, convoy.Faction, convoy.escort, out attacker);
			return attacker != null && Rand.Chance(c);
		}

		public static string FromName(TradeConvoy convoy)
		{
			TradeRoute r = RouteOf(convoy);
			if (r != null) return r.SettlementName;
			return Orders.SpecialOrderUtility.OrderOf(convoy)?.SettlementName ?? "?";
		}

		public static MapParent HomeOf(TradeConvoy convoy)
		{
			TradeRoute r = RouteOf(convoy);
			if (r != null) return r.home;
			return Orders.SpecialOrderUtility.OrderOf(convoy)?.home;
		}

		public static void StartAmbush(TradeConvoy convoy, Faction attacker)
		{
			int now = Find.TickManager.TicksGame;
			convoy.BeginAmbush(attacker, now + Rand.Range(AmbushMinTicks, AmbushMaxTicks));
			TradeRoute r = RouteOf(convoy);
			if (r != null) r.ambushes++;
			if (ConvoyBattleUtility.CanHelp(convoy).Accepted)
			{
				int hold = ConvoyBattleUtility.HoldOutTicks(convoy);
				convoy.ambushUntil = now + hold;
				Find.LetterStack.ReceiveLetter("RimMarket.ConvoyAmbushedLabel".Translate(),
					"RimMarket.ConvoyAmbushedHelp".Translate(FromName(convoy), attacker?.Name ?? "?", hold.ToStringTicksToPeriod()), LetterDefOf.ThreatSmall, new LookTargets(convoy));
				return;
			}
			if (ShowMessages)
				Messages.Message("RimMarket.ConvoyAmbushed".Translate(FromName(convoy), attacker?.Name ?? "?"),
					new LookTargets(convoy), MessageTypeDefOf.ThreatSmall, historical: true);
		}

		public static void ResolveAmbush(TradeConvoy convoy)
		{
			TradeRoute r = RouteOf(convoy);
			string from = FromName(convoy);
			string enemy = convoy.attacker?.Name ?? "?";
			float[] odds = convoy.escort ? EscortOutcomeOdds : OutcomeOdds;
			float roll = Rand.Value;
			if (roll < odds[0])
			{
				convoy.EndAmbush(0f);
				if (ShowMessages)
					Messages.Message("RimMarket.ConvoyRepelled".Translate(from, enemy), new LookTargets(convoy), MessageTypeDefOf.NeutralEvent, historical: true);
				return;
			}
			if (roll < odds[0] + odds[1])
			{
				float plunder = Rand.Range(PlunderMin, PlunderMax);
				convoy.EndAmbush(plunder);
				if (ShowMessages)
					Messages.Message("RimMarket.ConvoyPlundered".Translate(from, enemy, plunder.ToStringPercent()), new LookTargets(convoy), MessageTypeDefOf.NegativeEvent, historical: true);
				return;
			}
			LoseConvoy(convoy);
			if (ShowMessages)
				Messages.Message("RimMarket.ConvoyLost".Translate(from, enemy), MessageTypeDefOf.NegativeEvent, historical: true);
		}

		public static void LoseConvoy(TradeConvoy convoy)
		{
			TradeRoute r = RouteOf(convoy);
			if (r != null)
			{
				r.convoysLost++;
				r.convoy = null;
			}
			Orders.SpecialOrderUtility.Notify_ConvoyLost(convoy);
			if (!convoy.Destroyed) convoy.Destroy();
		}

		public static void Plunder(Lord lord, float fraction)
		{
			if (lord == null || fraction <= 0f) return;
			var items = new List<Thing>();
			float total = 0f;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				ThingOwner inv = lord.ownedPawns[i].inventory?.innerContainer;
				if (inv == null) continue;
				for (int j = 0; j < inv.Count; j++)
				{
					Thing t = inv[j];
					if (t is Pawn) continue;
					items.Add(t);
					total += t.MarketValue * t.stackCount;
				}
			}
			float target = total * fraction;
			float removed = 0f;
			items.Shuffle();
			for (int i = 0; i < items.Count && removed < target; i++)
			{
				Thing t = items[i];
				float unit = t.MarketValue;
				if (unit <= 0f || t.Destroyed) continue;
				int count = Mathf.Min(t.stackCount, Mathf.CeilToInt((target - removed) / unit));
				if (count <= 0) continue;
				removed += unit * count;
				if (count >= t.stackCount) t.Destroy();
				else t.SplitOff(count).Destroy();
			}
		}

		public static bool TryPath(PlanetTile from, PlanetTile to, out List<int> nodes, out int ticks)
		{
			nodes = null;
			ticks = 0;
			if (!from.Valid || !to.Valid || from.Layer != to.Layer) return false;
			using (WorldPath path = from.Layer.Pather.FindPath(from, to, null))
			{
				if (path == null || !path.Found) return false;
				List<PlanetTile> reversed = path.NodesReversed;
				nodes = new List<int>(reversed.Count);
				for (int i = reversed.Count - 1; i >= 0; i--) nodes.Add(reversed[i].tileId);
			}
			for (int i = 0; i < nodes.Count - 1; i++)
				ticks += Caravan_PathFollower.CostToMove(CaravanTicksPerMoveUtility.DefaultTicksPerMove,
					new PlanetTile(nodes[i], from.Layer), new PlanetTile(nodes[i + 1], from.Layer));
			return nodes.Count > 0;
		}

		public static List<TraderKindDef> KindsFor(Faction faction, Map map, Pawn negotiator)
		{
			var kinds = new List<TraderKindDef>();
			if (faction?.def.caravanTraderKinds == null) return kinds;
			foreach (TraderKindDef k in faction.def.caravanTraderKinds)
			{
				if (!k.requestable || !ReputationUtility.TraderKindAllowed(k, faction, map)) continue;
				if (k.TitleRequiredToTrade != null && (negotiator?.royalty == null || k.TitleRequiredToTrade.seniority > negotiator.GetCurrentTitleSeniorityIn(faction))) continue;
				kinds.Add(k);
			}
			return kinds;
		}

		public static string CannotOpen(MarketManager mgr, Faction faction, Map map)
		{
			if (mgr == null || faction == null || map == null) return "?";
			if (faction.HostileTo(Faction.OfPlayer)) return "RimMarket.RouteHostile".Translate();
			if (!mgr.HasOpenMarket(map)) return "RimMarket.RouteNeedsMarket".Translate();
			if (mgr.LedgerFor(faction) == null) return "RimMarket.RouteUnknown".Translate(faction.Name);
			if (mgr.HoldsGrudge(faction)) return "RimMarket.RouteGrudge".Translate(faction.Name);
			int max = MaxRoutes(mgr);
			if (mgr.Routes.Count >= max) return "RimMarket.RouteLimit".Translate(max);
			return null;
		}

		public static TradeRoute RouteFrom(MarketManager mgr, Settlement s)
		{
			if (mgr == null || s == null) return null;
			IReadOnlyList<TradeRoute> routes = mgr.Routes;
			for (int i = 0; i < routes.Count; i++)
			{
				if (routes[i].settlement == s) return routes[i];
			}
			return null;
		}

		public static TradeRoute RouteOf(TradeConvoy convoy)
		{
			return convoy == null ? null : Mgr?.RouteById(convoy.routeId);
		}

		public static TradeRoute Open(Faction faction, Settlement settlement, Map home, TraderKindDef kind, int intervalDays, bool escort, int toll)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || faction == null || settlement == null || home == null || kind == null) return null;
			int now = Find.TickManager.TicksGame;
			var route = new TradeRoute
			{
				id = mgr.NextRouteId(),
				faction = faction,
				settlement = settlement,
				home = home.Parent,
				kind = kind,
				intervalDays = intervalDays,
				escort = escort,
				toll = toll,
				openedTick = now,
				nextDepartureTick = now + Rand.Range(FirstDepartureMin, FirstDepartureMax),
			};
			mgr.AddRoute(route);
			return route;
		}

		public static void Close(TradeRoute route, string messageKey)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || route == null) return;
			mgr.RemoveRoute(route);
			if (route.convoy != null && !route.convoy.Destroyed) route.convoy.Destroy();
			route.convoy = null;
			if (!messageKey.NullOrEmpty() && ShowMessages)
				Messages.Message(messageKey.Translate(route.faction?.Name ?? "?", route.SettlementName), MessageTypeDefOf.NegativeEvent, historical: false);
		}

		private static string BrokenReason(MarketManager mgr, TradeRoute r)
		{
			if (r.faction == null || r.faction.defeated) return "RimMarket.RouteClosedGone";
			if (r.settlement == null || !r.settlement.Spawned || r.settlement.Faction != r.faction) return "RimMarket.RouteClosedGone";
			if (r.faction.HostileTo(Faction.OfPlayer)) return "RimMarket.RouteClosedHostile";
			if (r.home == null || !r.home.Spawned || !r.home.HasMap) return "RimMarket.RouteClosedHome";
			if (mgr.HoldsGrudge(r.faction)) return "RimMarket.RouteClosedGrudge";
			if (r.strikes >= MaxStrikes) return "RimMarket.RouteClosedStrikes";
			if (r.pathFailures >= MaxPathFailures) return "RimMarket.RouteClosedNoRoad";
			return null;
		}

		public static void RoutesTick(MarketManager mgr)
		{
			var copy = new List<TradeRoute>(mgr.Routes);
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < copy.Count; i++)
			{
				TradeRoute r = copy[i];
				string broken = BrokenReason(mgr, r);
				if (broken != null)
				{
					Close(r, broken);
					continue;
				}
				if (r.convoy != null && (r.convoy.Destroyed || !r.convoy.Spawned)) r.convoy = null;
				if (!RimMarketMod.Settings.tradeRoutes || r.convoy != null || now < r.nextDepartureTick) continue;
				Depart(r);
			}
		}

		public static bool Depart(TradeRoute r)
		{
			int now = Find.TickManager.TicksGame;
			if (!TryPath(r.settlement.Tile, r.home.Tile, out List<int> nodes, out int ticks))
			{
				r.pathFailures++;
				r.nextDepartureTick = now + TicksPerDay;
				return false;
			}
			r.pathFailures = 0;
			var convoy = (TradeConvoy)WorldObjectMaker.MakeWorldObject(RimMarketDefOf.RimMarket_TradeConvoy);
			convoy.Tile = r.settlement.Tile;
			convoy.SetFaction(r.faction);
			convoy.Setup(r.id, nodes);
			convoy.escort = r.escort;
			Find.WorldObjects.Add(convoy);
			r.convoy = convoy;
			r.convoysSent++;
			r.nextDepartureTick = now + r.intervalDays * TicksPerDay;
			if (ShowMessages)
				Messages.Message("RimMarket.RouteConvoyLeft".Translate(r.SettlementName, r.kind?.label ?? "?", ticks.ToStringTicksToPeriod()),
					new LookTargets(convoy), MessageTypeDefOf.PositiveEvent, historical: false);
			return true;
		}

		public static bool TryArrive(TradeConvoy convoy)
		{
			if (convoy.orderId > 0) return Orders.SpecialOrderUtility.TryArrive(convoy);
			TradeRoute r = RouteOf(convoy);
			Map map = r?.home?.Map;
			if (r == null || map == null || r.faction == null || r.faction.HostileTo(Faction.OfPlayer))
			{
				if (!convoy.Destroyed) convoy.Destroy();
				return false;
			}
			int now = Find.TickManager.TicksGame;
			bool welcome = MarketTruce.Welcome(map, r.faction);
			int toll = EffectiveToll(r.toll);
			if (welcome && toll > 0 && AuctionUtility.ColonySilver(map) < toll)
			{
				r.strikes++;
				r.convoy = null;
				if (!convoy.Destroyed) convoy.Destroy();
				Messages.Message("RimMarket.RouteTollUnpaid".Translate(r.SettlementName, toll, r.strikes, MaxStrikes), MessageTypeDefOf.NegativeEvent, historical: true);
				return false;
			}
			if (welcome)
			{
				var parms = new IncidentParms
				{
					target = map,
					faction = r.faction,
					traderKind = r.kind,
					forced = true,
				};
				if (ConvoyBattleUtility.ArriveWith(convoy, () => RimTollBridge.WithoutBooth(() => IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms))))
				{
					Lord lord = NewestTraderLord(map, r.faction);
					r.arrivedLordId = lord?.loadID ?? -1;
					r.arrivedTick = now;
					if (convoy.plunder > 0f) Plunder(lord, convoy.plunder);
					if (toll > 0 && AuctionUtility.TakeColonySilver(map, toll) && ShowMessages)
						Messages.Message("RimMarket.RouteTollPaid".Translate(r.SettlementName, toll), MessageTypeDefOf.NeutralEvent, historical: true);
					r.convoy = null;
					if (!convoy.Destroyed) convoy.Destroy();
					return true;
				}
			}
			if (now - convoy.waitingSince >= GiveUpTicks)
			{
				r.convoy = null;
				if (!convoy.Destroyed) convoy.Destroy();
				if (ShowMessages)
					Messages.Message("RimMarket.RouteConvoyTurnedBack".Translate(r.SettlementName), MessageTypeDefOf.NegativeEvent, historical: false);
				return false;
			}
			if (now == convoy.waitingSince && ShowMessages)
				Messages.Message((welcome ? "RimMarket.RouteConvoyDelayed" : "RimMarket.RouteConvoyWaiting").Translate(r.SettlementName),
					new LookTargets(convoy), MessageTypeDefOf.NeutralEvent, historical: false);
			return false;
		}

		public static Lord NewestTraderLord(Map map, Faction faction)
		{
			List<Lord> lords = map.lordManager.lords;
			for (int i = lords.Count - 1; i >= 0; i--)
			{
				if (lords[i].faction == faction && lords[i].LordJob is LordJob_TradeWithColony) return lords[i];
			}
			return null;
		}

		public static void Notify_VisitEnded(MarketManager mgr, StallVisit v, int verdict)
		{
			if (mgr == null || v == null || v.routeId <= 0) return;
			TradeRoute r = mgr.RouteById(v.routeId);
			if (r == null) return;
			bool bad = verdict < 0 || v.robbed || v.trader == null || v.trader.Dead || v.trader.Downed;
			if (bad)
			{
				r.strikes++;
				if (r.strikes < MaxStrikes && ShowMessages)
					Messages.Message("RimMarket.RouteStrike".Translate(r.faction?.Name ?? "?", r.strikes, MaxStrikes),
						MessageTypeDefOf.NegativeEvent, historical: false);
				return;
			}
			if (verdict <= 0) return;
			r.goodVisits++;
			if (r.strikes > 0) r.strikes--;
			r.faction?.TryAffectGoodwillWith(Faction.OfPlayer, GoodVisitGoodwill, canSendMessage: false, canSendHostilityLetter: false,
				RimMarketDefOf.RimMarket_TradeRoute);
		}
	}
}
