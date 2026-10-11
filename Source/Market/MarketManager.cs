using System;
using System.Collections.Generic;
using System.Linq;
using RimMarket.AI;
using RimMarket.Auction;
using RimMarket.Away;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimMarket.Fair;
using RimMarket.Inn;
using RimMarket.Reputation;
using RimMarket.Routes;
using RimMarket.Thieves;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RimMarket.Market
{
	public class MarketManager : GameComponent
	{
		private const int PatrolInterval = 250;
		private const int MaxTickFailures = 20;
		private const int FailCooldownTicks = 2500;
		private const int BrokeCooldownTicks = 30000;
		private const float ShareDueThreshold = 30f;

		public static MarketManager Instance { get; private set; }

		public static bool AnyVisits => Instance != null && Instance.visits.Count > 0;

		private const int TheftTimeoutTicks = 15000;
		private const float GuardAlertRadius = 45f;
		private const int CaughtGoodwill = 2;
		private const int RobbedGoodwill = -3;

		public static bool AnyThefts => Instance != null && Instance.thefts.Count > 0;

		private const int RefusedCooldownTicks = 600000;
		private const int JournalSize = 30;
		private const int ReputationInterval = 2500;
		private const int FairCheckInterval = 250;

		private List<StallVisit> visits = new List<StallVisit>();
		private List<MarketTheft> thefts = new List<MarketTheft>();
		private MarketReputation reputation = new MarketReputation();
		private List<MarketLogEntry> journal = new List<MarketLogEntry>();

		public MarketReputation Reputation => reputation;

		public IReadOnlyList<MarketLogEntry> Journal => journal;

		private const float OwnMemoryWeight = 0.6f;
		private const float GrudgeRefusal = 0.9f;

		private List<FactionLedger> ledgers = new List<FactionLedger>();

		private List<TradeRoute> routes = new List<TradeRoute>();
		private int nextRouteId = 1;

		public IReadOnlyList<TradeRoute> Routes => routes;

		public int NextRouteId() => nextRouteId++;

		public void AddRoute(TradeRoute route)
		{
			if (route != null && !routes.Contains(route)) routes.Add(route);
		}

		public void RemoveRoute(TradeRoute route) => routes.Remove(route);

		public TradeRoute RouteById(int id)
		{
			for (int i = 0; i < routes.Count; i++)
			{
				if (routes[i].id == id) return routes[i];
			}
			return null;
		}

		private RimMarket.Orders.WantedNotice notice;

		public RimMarket.Orders.WantedNotice Notice
		{
			get => notice;
			set => notice = value;
		}

		private List<RimMarket.Orders.SpecialOrder> orders = new List<RimMarket.Orders.SpecialOrder>();
		private int nextOrderId = 1;

		public IReadOnlyList<RimMarket.Orders.SpecialOrder> Orders => orders;

		public int NextOrderId() => nextOrderId++;

		public void AddOrder(RimMarket.Orders.SpecialOrder order)
		{
			if (order != null && !orders.Contains(order)) orders.Add(order);
		}

		public void RemoveOrder(RimMarket.Orders.SpecialOrder order) => orders.Remove(order);

		public RimMarket.Orders.SpecialOrder OrderById(int id)
		{
			for (int i = 0; i < orders.Count; i++)
			{
				if (orders[i].id == id) return orders[i];
			}
			return null;
		}

		public RimMarket.Orders.SpecialOrder OrderForLord(Lord lord)
		{
			if (lord == null) return null;
			for (int i = 0; i < orders.Count; i++)
			{
				if (orders[i].delivered && orders[i].arrivedLordId == lord.loadID) return orders[i];
			}
			return null;
		}

		public TradeRoute RouteForLord(Lord lord)
		{
			if (lord == null) return null;
			for (int i = 0; i < routes.Count; i++)
			{
				if (routes[i].arrivedLordId == lord.loadID) return routes[i];
			}
			return null;
		}

		private Dictionary<int, int> innPaid = new Dictionary<int, int>();

		public int InnPaidUntil(Pawn p)
		{
			return p != null && innPaid.TryGetValue(p.thingIDNumber, out int until) ? until : 0;
		}

		public void SetInnPaid(Pawn p, int until)
		{
			if (p != null) innPaid[p.thingIDNumber] = until;
		}

		private readonly List<int> expiredStays = new List<int>();

		private void InnTick()
		{
			int now = Find.TickManager.TicksGame;
			expiredStays.Clear();
			foreach (KeyValuePair<int, int> kv in innPaid)
			{
				if (kv.Value <= now) expiredStays.Add(kv.Key);
			}
			for (int i = 0; i < expiredStays.Count; i++) innPaid.Remove(expiredStays[i]);
			if (InnBeds.Any) InnUtility.Tidy();
		}

		public IReadOnlyList<FactionLedger> Ledgers => ledgers;

		public MarketReputation LedgerFor(Faction faction, bool create = false)
		{
			if (faction == null || faction.IsPlayer) return null;
			for (int i = 0; i < ledgers.Count; i++)
			{
				if (ledgers[i].faction == faction) return ledgers[i].rep;
			}
			if (!create) return null;
			var ledger = new FactionLedger { faction = faction };
			ledgers.Add(ledger);
			return ledger.rep;
		}

		public float OwnWeight(Faction faction)
		{
			MarketReputation own = LedgerFor(faction);
			return own == null ? 0f : OwnMemoryWeight * own.Familiarity;
		}

		public MarketReputation OpinionOf(Faction faction)
		{
			MarketReputation own = LedgerFor(faction);
			if (own == null) return reputation;
			return MarketReputation.Blend(reputation, own, OwnWeight(faction));
		}

		public bool HoldsGrudge(Faction faction)
		{
			MarketReputation own = LedgerFor(faction);
			return own != null && own.Established && own.Headline <= MarketReputation.HeadlineDubious;
		}

		public float RefusalChance(Faction faction)
		{
			float chance = OpinionOf(faction).RefusalChance;
			MarketReputation own = LedgerFor(faction);
			if (own != null && own.Headline == MarketReputation.HeadlineNotorious) chance = Mathf.Max(chance, GrudgeRefusal);
			return chance;
		}

		public void Record(StallVisit v, Faction faction, int axis, float points, string reason)
		{
			reputation.Add(axis, points);
			LedgerFor(faction, create: true)?.Add(axis, points);
			v?.AddReason(axis, points, reason);
		}

		private const float NpcBidChance = 0.45f;

		private List<SlaveAuction> auctions = new List<SlaveAuction>();

		public SlaveAuction AuctionAt(Building_AuctionBlock block)
		{
			for (int i = 0; i < auctions.Count; i++)
			{
				if (auctions[i].block == block) return auctions[i];
			}
			return null;
		}

		public bool AnyAuctionOn(Map map)
		{
			for (int i = 0; i < auctions.Count; i++)
			{
				if (auctions[i].block != null && auctions[i].block.Map == map) return true;
			}
			return false;
		}

		public bool IsBidder(Pawn pawn)
		{
			for (int i = 0; i < auctions.Count; i++)
			{
				List<AuctionBidder> bidders = auctions[i].bidders;
				for (int j = 0; j < bidders.Count; j++)
				{
					if (bidders[j].pawn == pawn) return true;
				}
			}
			return false;
		}

		private SlaveAuction AuctionFor(int visitId)
		{
			for (int i = 0; i < auctions.Count; i++)
			{
				if (auctions[i].visitId == visitId) return auctions[i];
			}
			return null;
		}

		private void AuctionPatrol()
		{
			if (RimMarketMod.Settings.slaveAuctions)
			{
				for (int i = 0; i < visits.Count; i++)
				{
					StallVisit v = visits[i];
					if (v.closed || v.auctionHeld || AuctionFor(v.id) != null) continue;
					int lots = AuctionUtility.CountLots(v);
					if (lots == 0)
					{
						NoteNoSlaves(v);
						continue;
					}
					Building_AuctionBlock block = AuctionUtility.FindBlock(v, auctions);
					if (block == null)
					{
						NoteNoBlock(v, lots);
						continue;
					}
					v.auctionHeld = true;
					var a = new SlaveAuction
					{
						visitId = v.id,
						block = block,
						state = SlaveAuction.StateWaiting,
						nextLotTick = Find.TickManager.TicksGame + AuctionUtility.AnnounceTicks,
					};
					auctions.Add(a);
					Say("RimMarket.AuctionAnnounced", v.faction?.Name ?? "?", lots, new LookTargets(block), MessageTypeDefOf.NeutralEvent);
					List<string> buyers = AuctionBuyers.Send(v.map, block, v.faction, AuctionUtility.LotsValue(v));
					if (buyers.Count > 0)
						Say("RimMarket.AuctionBuyersComing", string.Join(", ", buyers), new LookTargets(block), MessageTypeDefOf.NeutralEvent);
					Step(() => AuctionUtility.TryAttractBuyers(v.map));
				}
			}
			for (int i = auctions.Count - 1; i >= 0; i--)
			{
				SlaveAuction a = auctions[i];
				StallVisit v = VisitById(a.visitId);
				if (v == null || !v.LordAlive || v.closed || a.block == null || !a.block.Spawned)
				{
					CancelAuction(a);
					continue;
				}
				Step(() => AuctionStep(a, v));
			}
		}

		private const int ShopperDelayMin = 2000;
		private const int ShopperDelayMax = 4500;
		private const int ShopperRetryTicks = 2500;

		private void ShopperArrivals()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < visits.Count; i++)
			{
				StallVisit v = visits[i];
				if (v.closed || v.shoppersSent) continue;
				if (v.shoppersDueTick < 0)
				{
					v.shoppersDueTick = now + Rand.RangeInclusive(ShopperDelayMin, ShopperDelayMax);
					continue;
				}
				if (now < v.shoppersDueTick || !MarketUtility.MerchantAtPost(v)) continue;
				if (!MarketShoppers.RoomForMore(v.map))
				{
					v.shoppersDueTick = now + ShopperRetryTicks;
					continue;
				}
				v.shoppersSent = true;
				string from = MarketShoppers.Send(v);
				if (from != null)
					Say("RimMarket.ShoppersArrived", from, new LookTargets(v.stall.parent), MessageTypeDefOf.NeutralEvent);
			}
		}

		private void ColonyShoppers()
		{
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				Map map = maps[i];
				if (!map.IsPlayerHome || map.dangerWatcher.DangerRating != StoryDanger.None) continue;
				int hour = GenLocalDate.HourOfDay(map);
				if (hour < ColonyStalls.ShopHoursStart || hour >= ColonyStalls.ShopHoursEnd) continue;
				if (MarketShoppers.GroupsOn(map) > 0) continue;
				Building_MarketStall stall = ColonyStalls.FirstStocked(map);
				if (stall == null) continue;
				float pull = ColonyStalls.Pull(map);
				if (!Rand.MTBEventOccurs(ColonyStalls.ShopperMtbDays(pull), GenDate.TicksPerDay, ReputationInterval)) continue;
				string from = MarketShoppers.Send(map, stall.Stall, null);
				if (from == null) continue;
				string key = pull >= ColonyStalls.BargainPull ? "RimMarket.ShoppersForBargains" : "RimMarket.ShoppersForColonyStall";
				Say(key, from, new LookTargets(stall), MessageTypeDefOf.NeutralEvent);
			}
		}

		private void NoteNoSlaves(StallVisit v)
		{
			if (v.auctionNoted || !AuctionUtility.IsSlaver(v) || !AuctionUtility.PlayerHasBlock(v.map)) return;
			v.auctionNoted = true;
			string key = AuctionUtility.FactionShunsSlavery(v.faction) ? "RimMarket.AuctionNoSlavesIdeo" : "RimMarket.AuctionNoSlaves";
			Say(key, v.faction?.Name ?? "?", new LookTargets(v.trader), MessageTypeDefOf.NeutralEvent);
		}

		private void NoteNoBlock(StallVisit v, int lots)
		{
			if (v.auctionNoted || !AuctionUtility.PlayerHasBlock(v.map)) return;
			if (AuctionUtility.FindBlock(v, Enumerable.Empty<SlaveAuction>()) != null) return;
			v.auctionNoted = true;
			Say("RimMarket.AuctionNoBlock", v.faction?.Name ?? "?", lots, new LookTargets(v.trader), MessageTypeDefOf.CautionInput);
		}

		private void AuctionStep(SlaveAuction a, StallVisit v)
		{
			int now = Find.TickManager.TicksGame;
			if (a.state == SlaveAuction.StateWaiting)
			{
				if (now < a.nextLotTick) return;
				StartLot(a, v);
				return;
			}
			if (a.lot == null || !AuctionUtility.ForSale(a.lot, v))
			{
				Say("RimMarket.AuctionLotWithdrawn", a.lot?.LabelShortCap ?? "?", new LookTargets(a.block), MessageTypeDefOf.NeutralEvent);
				FinishLot(a);
				return;
			}
			KeepOnBlock(a);
			NpcBids(a, now);
			if (now >= a.lotEndTick) ResolveLot(a, v);
		}

		private void StartLot(SlaveAuction a, StallVisit v)
		{
			Pawn lot = AuctionUtility.NextLot(v, a);
			if (lot == null)
			{
				Say("RimMarket.AuctionOver", v.faction?.Name ?? "?", new LookTargets(a.block), MessageTypeDefOf.NeutralEvent);
				AuctionBuyers.Dismiss(a.block);
				auctions.Remove(a);
				return;
			}
			a.state = SlaveAuction.StateLot;
			a.lot = lot;
			a.lotNumber++;
			a.price = AuctionUtility.StartPrice(lot);
			a.leader = SlaveAuction.LeaderNone;
			a.leaderIndex = -1;
			a.lotEndTick = Find.TickManager.TicksGame + AuctionUtility.LotTicks;
			a.bidders = AuctionUtility.MakeBidders(v.map, lot, a.block);
			KeepOnBlock(a);
			if (!a.moodGiven)
			{
				a.moodGiven = true;
				AuctionUtility.GiveMoods(v.map);
			}
			Say("RimMarket.AuctionLotOpen", a.lotNumber, lot.LabelShortCap, a.price, new LookTargets(a.block), MessageTypeDefOf.NeutralEvent);
		}

		private void KeepOnBlock(SlaveAuction a)
		{
			Pawn p = a.lot;
			if (p == null || !p.Spawned || p.jobs == null || p.CurJobDef == RimMarketDefOf.RimMarket_StandOnBlock) return;
			if (!p.CanReach(a.block, PathEndMode.OnCell, Danger.Deadly)) return;
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_StandOnBlock, a.block);
			job.expiryInterval = -1;
			p.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		private void NpcBids(SlaveAuction a, int now)
		{
			int next = AuctionUtility.NextBid(a);
			var candidates = new List<int>();
			for (int i = 0; i < a.bidders.Count; i++)
			{
				if (a.leader == SlaveAuction.LeaderBidder && a.leaderIndex == i) continue;
				AuctionBidder b = a.bidders[i];
				if (b.max < next || !AuctionUtility.BidderStillIn(b)) continue;
				if (b.pawn != null && MarketUtility.SilverOf(b.pawn) < next) continue;
				candidates.Add(i);
			}
			if (candidates.Count == 0 || !Rand.Chance(NpcBidChance)) return;
			int index = candidates.RandomElement();
			bool outbidPlayer = a.leader == SlaveAuction.LeaderPlayer;
			a.price = next;
			a.leader = SlaveAuction.LeaderBidder;
			a.leaderIndex = index;
			if (a.lotEndTick - now < AuctionUtility.SoftCloseTicks) a.lotEndTick = now + AuctionUtility.SoftCloseTicks;
			AuctionBidder bidder = a.bidders[index];
			Thing shout = bidder.pawn != null && bidder.pawn.Spawned ? (Thing)bidder.pawn : a.block;
			MoteMaker.ThrowText(shout.DrawPos, shout.Map, next.ToString(), 3.5f);
			if (outbidPlayer)
				Say("RimMarket.AuctionOutbid", bidder.name, next, new LookTargets(a.block), MessageTypeDefOf.CautionInput);
		}

		public void PlayerBid(SlaveAuction a)
		{
			if (a == null || !a.LotRunning || a.leader == SlaveAuction.LeaderPlayer || a.block?.Map == null) return;
			int next = AuctionUtility.NextBid(a);
			if (AuctionUtility.ColonySilver(a.block.Map) < next)
			{
				Messages.Message("RimMarket.AuctionNotEnoughSilver".Translate(next), a.block, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			int now = Find.TickManager.TicksGame;
			a.price = next;
			a.leader = SlaveAuction.LeaderPlayer;
			a.leaderIndex = -1;
			if (a.lotEndTick - now < AuctionUtility.SoftCloseTicks) a.lotEndTick = now + AuctionUtility.SoftCloseTicks;
			MoteMaker.ThrowText(a.block.DrawPos, a.block.Map, next.ToString(), 3.5f);
			RimMarketDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(a.block.Position, a.block.Map));
		}

		private void ResolveLot(SlaveAuction a, StallVisit v)
		{
			Pawn lot = a.lot;
			LookTargets targets = new LookTargets(a.block);
			if (a.leader == SlaveAuction.LeaderPlayer)
			{
				Pawn negotiator = AuctionUtility.BestNegotiator(v.map);
				if (negotiator == null || !AuctionUtility.TakeColonySilver(v.map, a.price))
				{
					Say("RimMarket.AuctionCannotPay", lot.LabelShortCap, targets, MessageTypeDefOf.NegativeEvent);
					a.done.Add(lot);
					FinishLot(a);
					return;
				}
				Pawn trader = v.trader;
				if (trader?.inventory != null)
				{
					Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
					silver.stackCount = a.price;
					trader.inventory.innerContainer.TryAdd(silver);
				}
				ReleaseFromBlock(lot);
				trader?.trader?.GiveSoldThingToPlayer(lot, 1, negotiator);
				Say("RimMarket.AuctionSoldToYou", lot.LabelShortCap, a.price, new LookTargets(lot), MessageTypeDefOf.PositiveEvent);
			}
			else if (a.leader == SlaveAuction.LeaderBidder && a.Leader != null)
			{
				AuctionBidder winner = a.Leader;
				if (winner.pawn != null)
				{
					ThingOwner purse = ThiefUtility.CarrierOf(v)?.inventory?.innerContainer ?? v.trader?.inventory?.innerContainer;
					MarketUtility.TransferSilver(winner.pawn, purse, a.price);
				}
				RecordGuestSale(v, a.price);
				a.done.Add(lot);
				AuctionBuyers.HandOver(lot, winner.pawn);
				Say("RimMarket.AuctionSoldTo", lot.LabelShortCap, winner.name, a.price, targets, MessageTypeDefOf.NeutralEvent);
			}
			else
			{
				a.done.Add(lot);
				Say("RimMarket.AuctionNoTakers", lot.LabelShortCap, targets, MessageTypeDefOf.NeutralEvent);
			}
			FinishLot(a);
		}

		private void FinishLot(SlaveAuction a)
		{
			if (a.lot != null && !a.done.Contains(a.lot)) a.done.Add(a.lot);
			ReleaseFromBlock(a.lot);
			a.lot = null;
			a.state = SlaveAuction.StateWaiting;
			a.leader = SlaveAuction.LeaderNone;
			a.leaderIndex = -1;
			a.bidders.Clear();
			a.nextLotTick = Find.TickManager.TicksGame + AuctionUtility.BetweenLotsTicks;
		}

		private static void ReleaseFromBlock(Pawn p)
		{
			if (p != null && p.CurJobDef == RimMarketDefOf.RimMarket_StandOnBlock)
				p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
		}

		public int DebugAdvanceAuctions()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < auctions.Count; i++)
			{
				if (auctions[i].state == SlaveAuction.StateWaiting) auctions[i].nextLotTick = now;
				else auctions[i].lotEndTick = now;
			}
			return auctions.Count;
		}

		private void CancelAuction(SlaveAuction a)
		{
			ReleaseFromBlock(a.lot);
			AuctionBuyers.Dismiss(a.block);
			auctions.Remove(a);
		}

		private const int AwayCheckInterval = 250;
		private const float AwayGoodwillBig = 1000f;
		private const float AwayGoodwillSmall = 300f;

		private List<AwayStall> awayStalls = new List<AwayStall>();
		private Dictionary<int, int> awayCooldowns = new Dictionary<int, int>();

		public IReadOnlyList<AwayStall> AwayStalls => awayStalls;

		public AwayStall AwayStallFor(Caravan caravan)
		{
			if (caravan == null) return null;
			for (int i = 0; i < awayStalls.Count; i++)
			{
				if (awayStalls[i].caravan == caravan) return awayStalls[i];
			}
			return null;
		}

		public AwayStall AwayStallAt(Settlement settlement)
		{
			if (settlement == null) return null;
			for (int i = 0; i < awayStalls.Count; i++)
			{
				if (awayStalls[i].settlement == settlement) return awayStalls[i];
			}
			return null;
		}

		public int AwayCooldownLeft(Settlement settlement)
		{
			if (settlement == null || !awayCooldowns.TryGetValue(settlement.ID, out int until)) return 0;
			return Mathf.Max(0, until - Find.TickManager.TicksGame);
		}

		public void BeginAwayStall(Caravan caravan, Settlement settlement)
		{
			FloatMenuAcceptanceReport report = AwayStallUtility.CanHoldStall(caravan, settlement);
			if (!report.Accepted)
			{
				string reason = report.FailReason.NullOrEmpty() ? report.FailMessage : report.FailReason;
				if (!reason.NullOrEmpty())
					Messages.Message("RimMarket.AwayCannotStart".Translate(settlement?.LabelCap ?? "?", reason),
						caravan, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			int now = Find.TickManager.TicksGame;
			var st = new AwayStall
			{
				caravan = caravan,
				settlement = settlement,
				ally = settlement.Faction.PlayerRelationKind == FactionRelationKind.Ally,
				startTick = now,
				endTick = now + AwayStallUtility.MarketTicks,
				nextHourTick = now + AwayStallUtility.HourTicks,
			};
			awayStalls.Add(st);
			caravan.pather.StopDead();
			string rate = (AwayStallUtility.FeeRate(st) * 100f).ToString("F0");
			AwayStallUtility.Say("RimMarket.AwayStarted".Translate(caravan.LabelCap, settlement.LabelCap, rate), st, MessageTypeDefOf.PositiveEvent);
		}

		private void AwayTick()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = awayStalls.Count - 1; i >= 0; i--)
			{
				AwayStall st = awayStalls[i];
				Caravan c = st.caravan;
				if (c == null || c.Destroyed || !c.Spawned || st.settlement == null || !st.settlement.Spawned
					|| AwayStallUtility.HostileOrPlayer(st.settlement))
				{
					EndAwayStall(st, AwayStall.EndLost);
					continue;
				}
				if (c.Tile != st.settlement.Tile || c.pather.Moving)
				{
					EndAwayStall(st, AwayStall.EndMoved);
					continue;
				}
				while (now >= st.nextHourTick && st.nextHourTick <= st.endTick)
				{
					st.nextHourTick += AwayStallUtility.HourTicks;
					AwayStallUtility.RunHour(st);
				}
				if (now >= st.endTick)
				{
					EndAwayStall(st, AwayStall.EndNormal);
					continue;
				}
				if (AwayStallUtility.SellableItems(c, st.settlement).Count == 0) EndAwayStall(st, AwayStall.EndSoldOut);
			}
		}

		public void EndAwayStall(AwayStall st, int reason)
		{
			if (st == null || !awayStalls.Remove(st)) return;
			Step(() => AwayStallUtility.PaySilver(st));
			if (st.settlement != null) awayCooldowns[st.settlement.ID] = Find.TickManager.TicksGame + AwayStallUtility.CooldownTicks;
			Faction host = st.Host;
			if (reason != AwayStall.EndLost && host != null && !host.IsPlayer && !host.HostileTo(Faction.OfPlayer) && st.itemsSold > 0)
			{
				Step(() =>
				{
					int goodwill = st.gross >= AwayGoodwillBig ? 2 : (st.gross >= AwayGoodwillSmall ? 1 : 0);
					if (goodwill > 0)
						host.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, canSendMessage: false, canSendHostilityLetter: false,
							RimMarketDefOf.RimMarket_AwayStall);
					Record(null, host, MarketReputation.Appeal, st.gross >= AwayGoodwillBig ? 2f : 1f, null);
					Record(null, host, MarketReputation.Fairness, 1f, null);
				});
			}
			if (reason == AwayStall.EndLost)
			{
				AwayStallUtility.Say("RimMarket.AwayLost".Translate(st.settlement?.LabelCap ?? "?"), st, MessageTypeDefOf.NegativeEvent);
				return;
			}
			string key;
			switch (reason)
			{
				case AwayStall.EndPackedUp: key = "RimMarket.AwayEndPackedUp"; break;
				case AwayStall.EndMoved: key = "RimMarket.AwayEndMoved"; break;
				case AwayStall.EndSoldOut: key = "RimMarket.AwayEndSoldOut"; break;
				default: key = "RimMarket.AwayEndNormal"; break;
			}
			string head = key.Translate(st.caravan?.LabelCap ?? "?", st.settlement?.LabelCap ?? "?");
			string takings = st.itemsSold > 0
				? "RimMarket.AwayTakings".Translate(st.itemsSold, Mathf.RoundToInt(st.gross), Mathf.RoundToInt(st.fees)).ToString()
				: "RimMarket.AwayNoSales".Translate().ToString();
			AwayStallUtility.Say(head + " " + takings, st, st.itemsSold > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
		}

		public void DebugRunAwayDay()
		{
			for (int i = awayStalls.Count - 1; i >= 0; i--)
			{
				AwayStall st = awayStalls[i];
				while (st.nextHourTick <= st.endTick)
				{
					st.nextHourTick += AwayStallUtility.HourTicks;
					AwayStallUtility.RunHour(st);
				}
				EndAwayStall(st, AwayStall.EndNormal);
			}
		}

		public void DebugClearAwayCooldowns()
		{
			awayCooldowns.Clear();
		}

		public void DebugSetLedger(Faction faction, float perAxis)
		{
			LedgerFor(faction, create: true)?.DebugSet(perAxis);
		}

		public void DebugClearLedgers()
		{
			ledgers.Clear();
		}

		public void Notify_Browsed(CompMarketStall stall)
		{
			StallVisit v = VisitFor(stall);
			if (v != null) v.browsers++;
		}
		private int thievesCaught;
		private int thievesEscaped;
		private int thievesDeterred;

		public IReadOnlyList<MarketTheft> Thefts => thefts;

		public int ThievesCaught => thievesCaught;

		public int ThievesEscaped => thievesEscaped;

		public int ThievesDeterred => thievesDeterred;

		public MarketTheft TheftBy(Pawn thief)
		{
			if (thief == null) return null;
			for (int i = 0; i < thefts.Count; i++)
			{
				if (thefts[i].thief == thief) return thefts[i];
			}
			return null;
		}
		private int nextId = 1;
		private int totalFees;
		private int totalGuestSales;
		private int totalColonySales;
		private int tickFailures;
		private readonly Dictionary<int, int> cooldowns = new Dictionary<int, int>();
		private readonly HashSet<int> explained = new HashSet<int>();

		public int TotalFees => totalFees;

		public int TotalGuestSales => totalGuestSales;

		public int TotalColonySales => totalColonySales;

		private List<GreatFair> fairs = new List<GreatFair>();
		private int lastFairTick;
		private bool guideOffered;

		public bool AnyFair => fairs.Count > 0;

		public int LastFairTick
		{
			get => lastFairTick;
			set => lastFairTick = value;
		}

		public GreatFair FairOn(Map map)
		{
			if (map == null) return null;
			for (int i = 0; i < fairs.Count; i++)
			{
				if (fairs[i].map == map) return fairs[i];
			}
			return null;
		}

		public void AddFair(GreatFair fair)
		{
			if (fair != null && !fairs.Contains(fair)) fairs.Add(fair);
		}

		public void OfferGuide(Building_MarketMarker marker)
		{
			if (guideOffered) return;
			guideOffered = true;
			Find.LetterStack.ReceiveLetter("RimMarket.GuideLetterLabel".Translate(), "RimMarket.GuideLetterText".Translate(),
				LetterDefOf.NeutralEvent, new LookTargets(marker));
		}

		public void RemoveFair(GreatFair fair)
		{
			fairs.Remove(fair);
		}

		private void FairTick()
		{
			List<GreatFair> copy = new List<GreatFair>(fairs);
			for (int i = 0; i < copy.Count; i++) FairUtility.Tick(this, copy[i]);
		}

		public void RecordColonySale(int price)
		{
			totalColonySales += Mathf.Max(0, price);
		}

		public void RecordGuestSale(StallVisit v, int price)
		{
			if (v == null || price <= 0) return;
			v.silverEarned += price;
			totalGuestSales += price;
			v.stall?.MarkDisplayDirty();
			if (v.feeMode != RimMarketSettings.FeeShare) return;
			v.shareOwed += price * S.sharePercent;
			if (v.shareOwed >= ShareDueThreshold) v.feeDue = true;
		}

		public void AddFees(int value)
		{
			totalFees += Mathf.Max(0, value);
		}

		public IReadOnlyList<StallVisit> Visits => visits;

		public MarketManager(Game game)
		{
			Instance = this;
			InnBeds.Reset();
		}

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			Instance = this;
		}

		private static RimMarketSettings S => RimMarketMod.Settings;

		public StallVisit VisitFor(Lord lord)
		{
			if (lord == null) return null;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].lord == lord) return visits[i];
			}
			return null;
		}

		public StallVisit VisitFor(Pawn trader)
		{
			if (trader == null) return null;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].trader == trader) return visits[i];
			}
			return null;
		}

		public StallVisit VisitFor(CompMarketStall stall)
		{
			if (stall == null) return null;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].stall == stall) return visits[i];
			}
			return null;
		}

		public StallVisit VisitById(int id)
		{
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].id == id) return visits[i];
			}
			return null;
		}

		public int ActiveVisits(Map map)
		{
			int n = 0;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].map == map) n++;
			}
			return n;
		}

		public bool HasOpenMarket(Map map)
		{
			if (map == null) return false;
			List<Thing> markers = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker);
			for (int i = 0; i < markers.Count; i++)
			{
				if (markers[i] is Building_MarketMarker m && m.Spawned && m.Open) return true;
			}
			return false;
		}

		private bool OnCooldown(Lord lord)
		{
			return cooldowns.TryGetValue(lord.loadID, out int until) && Find.TickManager.TicksGame < until;
		}

		private void Cooldown(Lord lord, int ticks)
		{
			int now = Find.TickManager.TicksGame;
			if (cooldowns.Count > 16)
			{
				var expired = new List<int>();
				foreach (KeyValuePair<int, int> kv in cooldowns)
				{
					if (kv.Value <= now) expired.Add(kv.Key);
				}
				for (int i = 0; i < expired.Count; i++) cooldowns.Remove(expired[i]);
			}
			cooldowns[lord.loadID] = now + ticks;
		}

		public bool WantsStall(Lord lord)
		{
			if (lord == null || lord.Map == null) return false;
			if (VisitFor(lord) != null) return true;
			ClearWaiting(lord);
			if (lord.ticksInToil < 120) return false;
			if (!HasOpenMarket(lord.Map)) return false;
			if (OnCooldown(lord)) return false;
			if (lord.faction == null || lord.faction.IsPlayer || lord.faction.HostileTo(Faction.OfPlayer)) return false;
			Pawn trader = TraderCaravanUtility.FindTrader(lord);
			if (trader == null || !trader.Spawned || trader.Downed || trader.Dead) return false;
			if (RouteForLord(lord) == null && ReputationUtility.RefusesMarket(lord, RefusalChance(lord.faction)))
			{
				Cooldown(lord, RefusedCooldownTicks);
				ExplainSkip(lord, trader, ReputationUtility.RefusalKey(OpinionOf(lord.faction), HoldsGrudge(lord.faction)));
				return false;
			}
			if (ActiveVisits(lord.Map) >= (FairUtility.Running(lord.Map) ? Mathf.Max(S.maxMerchants, FairUtility.MerchantCap) : S.maxMerchants))
			{
				MarkWaiting(lord, "RimMarket.WaitMarketFull");
				ExplainSkip(lord, trader, "RimMarket.MessageMarketFull");
				return false;
			}
			if (MarketUtility.FindFreeStall(lord.Map, trader) != null) return true;
			bool anyFree = MarketUtility.AnyFreeStall(lord.Map);
			MarkWaiting(lord, anyFree ? "RimMarket.WaitUnreachable" : "RimMarket.WaitNoFreeStall");
			ExplainSkip(lord, trader, anyFree ? "RimMarket.MessageNoReachableStall" : "RimMarket.MessageNoFreeStall");
			return false;
		}

		private void ExplainSkip(Lord lord, Pawn trader, string key)
		{
			if (explained.Count > 64) explained.Clear();
			if (!explained.Add(lord.loadID)) return;
			Say(key, lord.faction?.Name ?? "?", new LookTargets(trader), MessageTypeDefOf.NeutralEvent);
		}

		public StallVisit TryBeginVisit(Lord lord)
		{
			StallVisit existing = VisitFor(lord);
			if (existing != null)
			{
				existing.dutiesDirty = true;
				return existing;
			}
			Map map = lord?.Map;
			Pawn trader = lord == null ? null : TraderCaravanUtility.FindTrader(lord);
			if (map == null || trader == null || !trader.Spawned)
			{
				if (lord != null) Cooldown(lord, FailCooldownTicks);
				return null;
			}
			CompMarketStall stall = MarketUtility.FindFreeStall(map, trader);
			if (stall == null)
			{
				Cooldown(lord, FailCooldownTicks);
				return null;
			}
			Building_MarketMarker marker = MarketUtility.MarkerFor(stall);
			RimMarket.Orders.SpecialOrder order = OrderForLord(lord);
			int feeMode = order != null ? RimMarketSettings.FeeNone : marker?.EffectiveFeeMode ?? S.FeeModeSafe;
			int fee = feeMode == RimMarketSettings.FeeArrival ? MarketUtility.FeeFor(lord) : 0;
			if (fee > 0 && S.turnAwayIfBroke)
			{
				float capacity = MarketUtility.PayCapacity(lord.ownedPawns, out int silver);
				float canPay = S.feeInGoods ? capacity : silver;
				if (canPay < fee)
				{
					Cooldown(lord, BrokeCooldownTicks);
					Say("RimMarket.MessageCannotAfford", lord.faction?.Name ?? "?", fee,
						new LookTargets(trader), MessageTypeDefOf.NeutralEvent);
					return null;
				}
			}
			int now = Find.TickManager.TicksGame;
			var v = new StallVisit
			{
				id = nextId++,
				lord = lord,
				map = map,
				faction = lord.faction,
				trader = trader,
				stall = stall,
				startTick = now,
				endTick = now + S.MarketTicks,
				feeMode = feeMode,
				feeAmount = fee,
				feeDue = fee > 0,
				wealthAtArrival = MarketUtility.CarriedWealth(lord),
				tolledToday = ReputationUtility.TolledToday(lord.faction),
				repPriceBonus = S.reputationEffects ? OpinionOf(lord.faction).PriceBonus : 0f,
				routeId = RouteForLord(lord)?.id ?? 0,
				orderId = order?.id ?? 0,
			};
			visits.Add(v);
			stall.Notify_VisitStarted();
			if (order != null && order.Specific) RimMarket.Orders.SpecialOrderUtility.HandOver(order, stall.CustomerCell, map);
			MarketUtility.TopUpFood(lord, full: true);
			MarketUtility.PackMeals(lord);
			Say("RimMarket.MessageTakingStall", lord.faction?.Name ?? "?", trader.LabelShortCap,
				new LookTargets(stall.parent), MessageTypeDefOf.PositiveEvent);
			return v;
		}

		public bool VisitShouldEnd(Lord lord, bool normal)
		{
			StallVisit v = VisitFor(lord);
			if (v == null) return false;
			if (normal) return !v.closed && v.StallOk && Find.TickManager.TicksGame >= v.endTick;
			if (v.closed) return true;
			if (!v.StallOk) return true;
			if (!v.TraderOk) return true;
			return false;
		}

		public void Notify_LeavingStall(Lord lord, bool normal)
		{
			StallVisit v = VisitFor(lord);
			if (v == null) return;
			int reason = normal ? StallVisit.EndNormal : (v.endReason != StallVisit.EndNone ? v.endReason : StallVisit.EndForced);
			EndVisit(v, reason);
		}

		public void EndVisit(StallVisit v, int reason)
		{
			if (v == null) return;
			visits.Remove(v);
			v.endReason = reason;
			SlaveAuction auction = AuctionFor(v.id);
			if (auction != null) Step(() => CancelAuction(auction));
			Step(() =>
			{
				if (v.feeMode == RimMarketSettings.FeeShare && v.shareOwed >= 1f && v.LordAlive && v.StallOk)
					MarketUtility.CollectShare(v);
			});
			Step(() => ReputationUtility.EvaluateVisitEnd(this, v, reason));
			int verdict = 0;
			if (reason != StallVisit.EndLordGone)
			{
				Step(() =>
				{
					verdict = ReputationUtility.Verdict(v);
					AddJournal(ReputationUtility.MakeLogEntry(v, verdict));
				});
				Step(() => TradeRouteUtility.Notify_VisitEnded(this, v, verdict));
			}
			Step(() => v.stall?.Notify_VisitEnded());
			Step(() => Announce(v, reason, verdict));
		}

		private void AddJournal(MarketLogEntry entry)
		{
			if (entry == null) return;
			journal.Insert(0, entry);
			while (journal.Count > JournalSize) journal.RemoveAt(journal.Count - 1);
		}

		private void Announce(StallVisit v, int reason, int verdict)
		{
			string faction = v.faction?.Name ?? "?";
			LookTargets targets = v.StallOk ? new LookTargets(v.stall.parent) : (v.LordAlive ? new LookTargets(v.lord.ownedPawns[0]) : null);
			string text;
			MessageTypeDef type;
			switch (reason)
			{
				case StallVisit.EndNormal:
					if (verdict > 0)
					{
						text = "RimMarket.MessageVerdictPleased".Translate(faction, ReputationUtility.ReasonsText(v, verdict));
						type = MessageTypeDefOf.PositiveEvent;
					}
					else if (verdict < 0)
					{
						text = "RimMarket.MessageVerdictGrumbling".Translate(faction, ReputationUtility.ReasonsText(v, verdict));
						type = MessageTypeDefOf.NegativeEvent;
					}
					else
					{
						text = "RimMarket.MessagePackingUp".Translate(faction);
						type = MessageTypeDefOf.NeutralEvent;
					}
					break;
				case StallVisit.EndClosed:
					text = "RimMarket.MessageMarketClosedLeaving".Translate(faction);
					type = MessageTypeDefOf.NeutralEvent;
					break;
				case StallVisit.EndStallGone:
					text = "RimMarket.MessageStallGone".Translate(faction);
					type = MessageTypeDefOf.NegativeEvent;
					break;
				case StallVisit.EndForced:
					text = "RimMarket.MessageLeavingEarly".Translate(faction);
					type = MessageTypeDefOf.NegativeEvent;
					break;
				case StallVisit.EndFeud:
					text = "RimMarket.MessageFeudLeaving".Translate(faction, v.feudWith ?? "?");
					type = MessageTypeDefOf.NeutralEvent;
					break;
				default:
					return;
			}
			if (!S.showMessages) return;
			Messages.Message(text + " " + ReputationUtility.TakingsText(v), targets, type, historical: false);
		}

		public void Notify_Traded(Pawn trader, float bought, float sold)
		{
			StallVisit v = VisitFor(trader);
			if (v == null) return;
			v.colonyBought += Mathf.Max(0f, bought);
			v.colonySold += Mathf.Max(0f, sold);
			v.stall?.MarkDisplayDirty();
		}

		private const int WaitingFreshTicks = 600;

		private readonly Dictionary<int, int> waitingSince = new Dictionary<int, int>();
		private readonly Dictionary<int, string> waitingReason = new Dictionary<int, string>();

		public bool AnyWaiting => waitingSince.Count > 0;

		public bool WaitingForStall(Lord lord, out string reasonKey)
		{
			reasonKey = null;
			if (lord == null || VisitFor(lord) != null) return false;
			if (!waitingSince.TryGetValue(lord.loadID, out int tick)) return false;
			if (Find.TickManager.TicksGame - tick > WaitingFreshTicks) return false;
			waitingReason.TryGetValue(lord.loadID, out reasonKey);
			return true;
		}

		private void MarkWaiting(Lord lord, string reasonKey)
		{
			int now = Find.TickManager.TicksGame;
			if (waitingSince.Count > 32) PurgeStaleWaiting(now);
			waitingSince[lord.loadID] = now;
			waitingReason[lord.loadID] = reasonKey;
		}

		private void PurgeStaleWaiting(int now)
		{
			if (waitingSince.Count == 0) return;
			var stale = new List<int>();
			foreach (KeyValuePair<int, int> kv in waitingSince)
			{
				if (now - kv.Value > WaitingFreshTicks) stale.Add(kv.Key);
			}
			for (int i = 0; i < stale.Count; i++)
			{
				waitingSince.Remove(stale[i]);
				waitingReason.Remove(stale[i]);
			}
		}

		private void ClearWaiting(Lord lord)
		{
			waitingSince.Remove(lord.loadID);
			waitingReason.Remove(lord.loadID);
		}

		public int ActiveVisits(Building_MarketMarker marker)
		{
			int n = 0;
			for (int i = 0; i < visits.Count; i++)
			{
				if (BelongsTo(visits[i], marker)) n++;
			}
			return n;
		}

		private static bool BelongsTo(StallVisit v, Building_MarketMarker marker)
		{
			return marker != null && v.StallOk && v.map == marker.Map && MarketUtility.MarkerFor(v.stall) == marker;
		}

		public void CloseMarket(Building_MarketMarker marker)
		{
			int n = 0;
			for (int i = 0; i < visits.Count; i++)
			{
				StallVisit v = visits[i];
				if (v.closed || !BelongsTo(v, marker)) continue;
				v.closed = true;
				v.endReason = StallVisit.EndClosed;
				n++;
			}
			if (n > 0) Say("RimMarket.MessageMarketClosed", n, null, MessageTypeDefOf.NeutralEvent);
		}

		public void Notify_StallDespawned(CompMarketStall stall)
		{
			for (int i = visits.Count - 1; i >= 0; i--)
			{
				StallVisit v = visits[i];
				if (v.stall != stall) continue;
				CompMarketStall other = v.map == null ? null : MarketUtility.FindFreeStall(v.map, v.trader, stall);
				if (other != null)
				{
					v.stall = other;
					v.dutiesDirty = true;
					other.Notify_VisitStarted();
					Say("RimMarket.MessageMovingStall", v.faction?.Name ?? "?", new LookTargets(other.parent), MessageTypeDefOf.NeutralEvent);
				}
				else
				{
					v.stall = null;
					v.closed = true;
					v.endReason = StallVisit.EndStallGone;
				}
			}
		}

		public void PayFee(StallVisit v, Pawn payer)
		{
			if (v == null || !v.LordAlive || !v.StallOk) return;
			int amount = v.feeMode == RimMarketSettings.FeeShare ? Mathf.RoundToInt(v.shareOwed) : v.feeAmount;
			v.feeDue = false;
			if (amount <= 0) return;
			int value = MarketUtility.TakeAndPlacePayment(v.lord.ownedPawns, amount, S.feeInGoods, v.stall.CustomerCell, v.map);
			if (v.feeMode == RimMarketSettings.FeeShare) v.shareOwed = 0f;
			else
			{
				v.feeAmount = value;
				v.feePaid = value > 0;
				v.feeShort = value <= 0;
			}
			v.feeCollected += value;
			totalFees += value;
			if (value <= 0)
			{
				if (v.feeMode != RimMarketSettings.FeeShare)
					Say("RimMarket.MessageFeeUnpaid", v.faction?.Name ?? "?", new LookTargets(v.stall.parent), MessageTypeDefOf.NeutralEvent);
				return;
			}
			RimMarketDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(v.stall.CustomerCell, v.map));
			MoteMaker.ThrowText(v.stall.DrawPos, v.map, "+" + value, 3.9f);
			Say(v.feeMode == RimMarketSettings.FeeShare ? "RimMarket.MessageSharePaid" : "RimMarket.MessageFeePaid",
				v.faction?.Name ?? "?", value, new LookTargets(v.stall.parent), MessageTypeDefOf.PositiveEvent);
		}

		public void TryHaggle(StallVisit v, Pawn negotiator)
		{
			if (v == null || v.haggleUsed || negotiator == null) return;
			v.haggleUsed = true;
			int social = negotiator.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			float chance = 0.35f + 0.04f * (social - 6) + (v.faction?.PlayerGoodwill ?? 0) / 400f;
			if (RimTollBridge.Available && v.faction != null) chance -= RimTollBridge.Resentment(v.faction) * 0.15f;
			chance = Mathf.Clamp(chance, 0.10f, 0.90f);
			float bonus = S.haggleBonus;
			negotiator.skills?.Learn(SkillDefOf.Social, 500f, direct: true);
			Map map = v.map;
			Vector3 pos = v.trader != null && v.trader.Spawned ? v.trader.DrawPos : (v.StallOk ? v.stall.DrawPos : negotiator.DrawPos);
			if (Rand.Chance(chance))
			{
				v.haggleWon = true;
				v.haggleBuyFactor = 1f - bonus;
				v.haggleSellFactor = 1f + bonus;
				ReputationUtility.Record(this, v, MarketReputation.Fairness, ReputationUtility.HaggleWon, "RimMarket.ReasonHaggledHard");
				if (map != null) MoteMaker.ThrowText(pos, map, "RimMarket.MoteDeal".Translate(), 3.5f);
				Say("RimMarket.MessageHaggleWon", negotiator.LabelShortCap, (bonus * 100f).ToString("F0"),
					new LookTargets(negotiator), MessageTypeDefOf.PositiveEvent);
			}
			else
			{
				v.haggleWon = false;
				v.haggleBuyFactor = 1f + bonus;
				v.haggleSellFactor = 1f - bonus;
				v.endTick = Mathf.Max(Find.TickManager.TicksGame + 2500, v.endTick - 15000);
				ReputationUtility.Record(this, v, MarketReputation.Fairness, ReputationUtility.HaggleLost, "RimMarket.ReasonInsulted");
				if (map != null) MoteMaker.ThrowText(pos, map, "RimMarket.MoteRefused".Translate(), 3.5f);
				Say("RimMarket.MessageHaggleLost", negotiator.LabelShortCap, v.trader?.LabelShortCap ?? "?",
					new LookTargets(negotiator), MessageTypeDefOf.NegativeEvent);
			}
		}

		public void Notify_Snatched(Pawn thief, Thing loot, StallVisit v)
		{
			var t = new MarketTheft
			{
				thief = thief,
				loot = loot,
				lootLabel = loot.LabelCap,
				visitId = v.id,
				merchantFaction = v.faction,
				map = thief.Map,
				startTick = Find.TickManager.TicksGame,
			};
			thefts.Add(t);
			v.robbed = true;
			thief.guilt?.Notify_Guilty();
			if (v.trader != null && v.trader.Spawned)
				MoteMaker.ThrowText(v.trader.DrawPos, v.map, "RimMarket.MoteThief".Translate(), 4f);
			Say("RimMarket.MessageSnatched", thief.LabelShortCap, t.lootLabel, v.faction?.Name ?? "?",
				new LookTargets(thief), MessageTypeDefOf.ThreatSmall);
			AlertGuards(thief, v);
		}

		private void AlertGuards(Pawn thief, StallVisit robbed)
		{
			for (int i = 0; i < visits.Count; i++)
			{
				StallVisit v = visits[i];
				if (v.map != thief.Map || !v.LordAlive) continue;
				List<Pawn> pawns = v.lord.ownedPawns.ToList();
				for (int j = 0; j < pawns.Count; j++)
				{
					Pawn g = pawns[j];
					if (!ThiefUtility.IsCaravanGuard(g) || !g.Spawned || g.Downed || !g.Awake() || g.InMentalState) continue;
					if (g.WorkTagIsDisabled(WorkTags.Violent) || g.CurJobDef == RimMarketDefOf.RimMarket_CatchThief) continue;
					float range = v == robbed ? GuardAlertRadius : ThiefUtility.NeighborRadius;
					if (!g.Position.InHorDistOf(thief.Position, range)) continue;
					g.jobs?.EndCurrentJob(JobCondition.InterruptForced);
				}
			}
		}

		public void Notify_Deterred(Pawn thief, StallVisit v)
		{
			thievesDeterred++;
			ReputationUtility.Record(this, v, MarketReputation.Safety, ReputationUtility.ThiefDeterred, "RimMarket.ReasonThiefDeterred");
			if (thief.Spawned) MoteMaker.ThrowText(thief.DrawPos, thief.Map, "RimMarket.MoteNotToday".Translate(), 3.5f);
			Say("RimMarket.MessageThiefDeterred", thief.LabelShortCap, new LookTargets(thief), MessageTypeDefOf.NeutralEvent);
			thief.GetLord()?.ReceiveMemo(LordJob_MarketThief.MemoLeave);
		}

		public void Notify_ThiefCaught(Pawn thief, Pawn guard)
		{
			MarketTheft t = TheftBy(thief);
			if (t == null) return;
			thefts.Remove(t);
			ResolveCaught(t, guard);
		}

		private void ResolveCaught(MarketTheft t, Pawn guard)
		{
			thievesCaught++;
			StallVisit v = VisitById(t.visitId);
			Record(v, t.merchantFaction, MarketReputation.Safety, ReputationUtility.ThiefCaught, "RimMarket.ReasonThiefCaught");
			Step(() => ThiefUtility.ReturnLoot(t, v));
			Faction f = t.merchantFaction;
			bool byColonist = guard != null && guard.Faction == Faction.OfPlayer;
			if (byColonist && f != null && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer))
				f.TryAffectGoodwillWith(Faction.OfPlayer, CaughtGoodwill, canSendMessage: true, canSendHostilityLetter: false,
					RimMarketDefOf.RimMarket_ThiefCaught);
			string thiefName = t.thief?.LabelShortCap ?? "?";
			LookTargets targets = t.thief != null && t.thief.Spawned ? new LookTargets(t.thief) : null;
			if (guard != null)
				Say("RimMarket.MessageThiefCaughtBy", guard.LabelShortCap, thiefName, targets, MessageTypeDefOf.PositiveEvent);
			else
				Say("RimMarket.MessageThiefCaught", thiefName, targets, MessageTypeDefOf.PositiveEvent);
			t.thief?.GetLord()?.ReceiveMemo(LordJob_MarketThief.MemoLeave);
		}

		public void Notify_CulpritCaptured(Pawn thief, Faction faction)
		{
			thief.mindState?.SetNoAidRelationsGainUntilTick(int.MaxValue);
			if (!MessagesRepeatAvoider.MessageShowAllowed("RimMarket.CulpritCaptured." + thief.thingIDNumber, 99999f)) return;
			Say("RimMarket.MessageCulpritCaptured", thief.LabelShortCap, faction?.Name ?? "?",
				new LookTargets(thief), MessageTypeDefOf.NeutralEvent);
		}

		private void ResolveEscaped(MarketTheft t)
		{
			thievesEscaped++;
			StallVisit v = VisitById(t.visitId);
			Record(v, t.merchantFaction, MarketReputation.Safety, ReputationUtility.ThiefEscaped, "RimMarket.ReasonRobbed");
			Faction f = t.merchantFaction;
			if (f != null && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer))
				f.TryAffectGoodwillWith(Faction.OfPlayer, RobbedGoodwill, canSendMessage: true, canSendHostilityLetter: false,
					RimMarketDefOf.RimMarket_StallRobbed);
			if (v != null) v.endTick = Mathf.Max(Find.TickManager.TicksGame + 2500, v.endTick - 15000);
			LookTargets targets = v != null && v.StallOk ? new LookTargets(v.stall.parent) : null;
			Say("RimMarket.MessageThiefEscaped", t.lootLabel ?? "?", f?.Name ?? "?", targets, MessageTypeDefOf.NegativeEvent);
		}

		private void ResolveDropped(MarketTheft t)
		{
			StallVisit v = VisitById(t.visitId);
			Step(() => ThiefUtility.ReturnLoot(t, v));
			t.thief?.GetLord()?.ReceiveMemo(LordJob_MarketThief.MemoLeave);
		}

		private void ResolveThefts()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = thefts.Count - 1; i >= 0; i--)
			{
				MarketTheft t = thefts[i];
				Pawn th = t.thief;
				if (th == null)
				{
					thefts.RemoveAt(i);
					continue;
				}
				if (th.Dead || th.Downed || th.IsPrisonerOfColony || th.Faction == Faction.OfPlayer)
				{
					thefts.RemoveAt(i);
					ResolveCaught(t, null);
					continue;
				}
				if (!th.Spawned)
				{
					thefts.RemoveAt(i);
					ResolveEscaped(t);
					continue;
				}
				if (t.loot == null || t.loot.Destroyed || th.carryTracker?.CarriedThing != t.loot)
				{
					thefts.RemoveAt(i);
					ResolveDropped(t);
					continue;
				}
				if (now - t.startTick > TheftTimeoutTicks) thefts.RemoveAt(i);
			}
		}

		private void ThiefArrivals()
		{
			if (!S.thievesEnabled || S.thiefMtbDays <= 0f) return;
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				Map map = maps[i];
				if (ActiveVisits(map) == 0) continue;
				float mtb = FairUtility.Running(map) ? S.thiefMtbDays / FairUtility.ThiefFactor : S.thiefMtbDays;
				if (!Rand.MTBEventOccurs(mtb, 60000f, PatrolInterval)) continue;
				if (ThiefUtility.AnyThiefOnMap(map)) continue;
				ThiefUtility.TrySendThief(map, Rand.RangeInclusive(1500, 4000));
			}
		}

		private void Patrol()
		{
			if (thefts.Count > 0) ResolveThefts();
			if (visits.Count > 0) ThiefArrivals();
			if (visits.Count > 0 || auctions.Count > 0) Step(AuctionPatrol);
			if (visits.Count > 0 && S.shoppersVisit) Step(ShopperArrivals);
			if (visits.Count > 0) Step(FeudPatrol);
			for (int i = visits.Count - 1; i >= 0; i--)
			{
				StallVisit v = visits[i];
				if (!v.LordAlive || v.map == null || v.lord.Map != v.map || !v.map.lordManager.lords.Contains(v.lord))
				{
					EndVisit(v, StallVisit.EndLordGone);
					continue;
				}
				if (!(v.lord.LordJob is LordJob_TradeWithColony))
				{
					EndVisit(v, StallVisit.EndLordGone);
					continue;
				}
				LordToil cur = v.lord.CurLordToil;
				if (cur is LordToil_ExitMapAndEscortCarriers || cur is LordToil_ExitMap || cur is LordToil_ExitMapTraderFighting)
				{
					EndVisit(v, StallVisit.EndLeft);
					continue;
				}
				if (!v.StallOk && !v.closed)
				{
					v.closed = true;
					v.endReason = StallVisit.EndStallGone;
				}
			}
		}

		private void FeudPatrol()
		{
			for (int i = 0; i < visits.Count; i++)
			{
				StallVisit v = visits[i];
				if (!v.LordAlive) continue;
				Lord rival = MarketTruce.RivalOf(v.lord);
				if (rival == null) continue;
				v.feud = true;
				StallVisit other = VisitFor(rival);
				if (other != null)
				{
					other.feud = true;
					StallVisit leaver = other.startTick >= v.startTick ? other : v;
					PackUpForFeud(leaver, leaver == v ? other.faction : v.faction);
					continue;
				}
				if (rival.LordJob is LordJob_MarketShoppers) continue;
				if (rival.LordJob is LordJob_AuctionBuyers)
				{
					rival.ReceiveMemo(LordJob_AuctionBuyers.MemoDone);
					continue;
				}
				PackUpForFeud(v, rival.faction);
			}
		}

		private static void PackUpForFeud(StallVisit v, Faction enemy)
		{
			if (v == null || v.closed) return;
			v.closed = true;
			v.endReason = StallVisit.EndFeud;
			v.feudWith = enemy?.Name ?? "?";
		}

		private void ReputationTick()
		{
			int now = Find.TickManager.TicksGame;
			reputation.Decay(now);
			for (int i = 0; i < ledgers.Count; i++) ledgers[i].rep?.Decay(now);
			PurgeStaleWaiting(now);
			if (!S.reputationEffects) return;
			float perDay = reputation.ExtraCaravanChancePerDay;
			if (perDay <= 0f) return;
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				Map map = maps[i];
				if (!map.IsPlayerHome || !HasOpenMarket(map)) continue;
				if (ActiveVisits(map) >= S.maxMerchants || !MarketUtility.AnyFreeStall(map)) continue;
				if (TraderCaravanOnMap(map)) continue;
				if (!Rand.MTBEventOccurs(1f / perDay, 60000f, ReputationInterval)) continue;
				ReputationUtility.TrySendMarketCaravan(map, announce: true);
			}
		}

		private bool TraderCaravanOnMap(Map map)
		{
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].LordJob is LordJob_TradeWithColony && VisitFor(lords[i]) == null) return true;
			}
			return false;
		}

		public override void GameComponentTick()
		{
			if (Find.TickManager.TicksGame % ReputationInterval == 0)
			{
				Step(ReputationTick);
				if (InnBeds.Any || innPaid.Count > 0) Step(InnTick);
				if (routes.Count > 0) Step(() => TradeRouteUtility.RoutesTick(this));
				if (orders.Count > 0) Step(() => RimMarket.Orders.SpecialOrderUtility.OrdersTick(this));
				if (notice != null) Step(() => RimMarket.Orders.WantedUtility.Tick(this));
				if (S.shoppersVisit) Step(ColonyShoppers);
			}
			if (awayStalls.Count > 0 && Find.TickManager.TicksGame % AwayCheckInterval == 0) Step(AwayTick);
			if (fairs.Count > 0 && Find.TickManager.TicksGame % FairCheckInterval == 0) Step(FairTick);
			if (visits.Count == 0 && thefts.Count == 0) return;
			if (Find.TickManager.TicksGame % PatrolInterval != 0) return;
			try
			{
				Patrol();
				tickFailures = 0;
			}
			catch (Exception e)
			{
				tickFailures++;
				if (tickFailures == 1) Log.Error("[RimMarket] error during the market patrol: " + e);
				if (tickFailures < MaxTickFailures) return;
				Log.Error("[RimMarket] the market patrol keeps failing, ending every visit.");
				List<StallVisit> copy = new List<StallVisit>(visits);
				for (int i = 0; i < copy.Count; i++)
				{
					StallVisit v = copy[i];
					Step(() => EndVisit(v, StallVisit.EndForced));
				}
				visits.Clear();
				tickFailures = 0;
			}
		}

		public void DebugEndAll()
		{
			List<StallVisit> copy = new List<StallVisit>(visits);
			for (int i = 0; i < copy.Count; i++)
			{
				copy[i].closed = true;
				copy[i].endReason = StallVisit.EndClosed;
			}
		}

		private static void Step(Action step)
		{
			try { step(); }
			catch (Exception e) { Log.Error("[RimMarket] end of visit cleanup: " + e); }
		}

		private static void Say(string key, NamedArgument a, LookTargets targets, MessageTypeDef type)
		{
			if (!S.showMessages) return;
			Messages.Message(key.Translate(a), targets, type, historical: false);
		}

		private static void Say(string key, NamedArgument a, NamedArgument b, LookTargets targets, MessageTypeDef type)
		{
			if (!S.showMessages) return;
			Messages.Message(key.Translate(a, b), targets, type, historical: false);
		}

		private static void Say(string key, NamedArgument a, NamedArgument b, NamedArgument c, LookTargets targets, MessageTypeDef type)
		{
			if (!S.showMessages) return;
			Messages.Message(key.Translate(a, b, c), targets, type, historical: false);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref visits, "visits", LookMode.Deep);
			Scribe_Values.Look(ref nextId, "nextId", 1);
			Scribe_Values.Look(ref totalFees, "totalFees");
			Scribe_Values.Look(ref totalGuestSales, "totalGuestSales");
			Scribe_Values.Look(ref totalColonySales, "totalColonySales");
			Scribe_Collections.Look(ref thefts, "thefts", LookMode.Deep);
			Scribe_Values.Look(ref thievesCaught, "thievesCaught");
			Scribe_Values.Look(ref thievesEscaped, "thievesEscaped");
			Scribe_Values.Look(ref thievesDeterred, "thievesDeterred");
			Scribe_Deep.Look(ref reputation, "reputation");
			Scribe_Collections.Look(ref journal, "journal", LookMode.Deep);
			Scribe_Collections.Look(ref ledgers, "ledgers", LookMode.Deep);
			Scribe_Collections.Look(ref awayStalls, "awayStalls", LookMode.Deep);
			Scribe_Collections.Look(ref auctions, "auctions", LookMode.Deep);
			Scribe_Collections.Look(ref awayCooldowns, "awayCooldowns", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref innPaid, "innPaid", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref routes, "routes", LookMode.Deep);
			Scribe_Values.Look(ref nextRouteId, "nextRouteId", 1);
			Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
			Scribe_Values.Look(ref nextOrderId, "nextOrderId", 1);
			Scribe_Deep.Look(ref notice, "wantedNotice");
			Scribe_Collections.Look(ref fairs, "fairs", LookMode.Deep);
			Scribe_Values.Look(ref lastFairTick, "lastFairTick");
			Scribe_Values.Look(ref guideOffered, "guideOffered");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (fairs == null) fairs = new List<GreatFair>();
				fairs.RemoveAll(f => f == null || f.map == null);
				if (innPaid == null) innPaid = new Dictionary<int, int>();
				if (routes == null) routes = new List<TradeRoute>();
				routes.RemoveAll(r => r == null || r.faction == null || r.kind == null);
				if (orders == null) orders = new List<RimMarket.Orders.SpecialOrder>();
				orders.RemoveAll(o => o == null || o.faction == null);
				if (auctions == null) auctions = new List<SlaveAuction>();
				auctions.RemoveAll(a => a == null || a.block == null);
				if (awayStalls == null) awayStalls = new List<AwayStall>();
				awayStalls.RemoveAll(a => a == null || a.caravan == null || a.settlement == null);
				if (awayCooldowns == null) awayCooldowns = new Dictionary<int, int>();
				if (reputation == null) reputation = new MarketReputation();
				if (ledgers == null) ledgers = new List<FactionLedger>();
				ledgers.RemoveAll(l => l == null || l.faction == null);
				if (journal == null) journal = new List<MarketLogEntry>();
				journal.RemoveAll(e => e == null);
				if (visits == null) visits = new List<StallVisit>();
				visits.RemoveAll(v => v == null || v.map == null || v.lord == null);
				if (thefts == null) thefts = new List<MarketTheft>();
				thefts.RemoveAll(t => t == null || t.thief == null);
			}
		}
	}
}
