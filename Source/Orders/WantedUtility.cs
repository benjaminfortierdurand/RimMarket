using System.Collections.Generic;
using System.Linq;
using RimMarket.Auction;
using RimMarket.Market;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Orders
{
	public static class WantedUtility
	{
		private const int ReplyMinTicks = 12000;
		private const int ReplyMaxTicks = 180000;
		private const int OfferLifeTicks = 240000;
		private const int AcceptPrepTicks = 30000;
		private const int MaxReplies = 6;
		private const float BaseReplyChance = 0.55f;
		private const float GrudgeReplyFactor = 0.6f;
		private const float PartialChance = 0.2f;
		private const float BarterChance = 0.16f;
		private const float BarterSlack = 0.15f;
		private const float BarterSurcharge = 1.25f;
		private const float TripFactor = 1.5f;
		private const float FriendMarkup = 1.5f;
		private const float StrangerMarkup = 2.2f;
		private const float BarterValue = 1.1f;
		private const int RefusedGoodwill = -10;
		private const float HaggleXp = 500f;
		private const float HaggleSkillWeight = 0.04f;
		private const int HaggleSkillPivot = 6;
		private const float HaggleGoodwillWeight = 1f / 400f;
		private const float HaggleMinChance = 0.05f;
		private const float HaggleMaxChance = 0.95f;

		public static readonly float[] HaggleCuts = { 0.1f, 0.2f, 0.35f };
		private static readonly float[] HaggleBase = { 0.85f, 0.6f, 0.35f };
		public static readonly float[] HaggleWalkout = { 0f, 0.5f, 1f };

		private static MarketManager Mgr => MarketManager.Instance;

		private static int Now => Find.TickManager.TicksGame;

		private static int Round10(float v) => 10 * Mathf.RoundToInt(v / 10f);

		public static string CannotPost(Map map)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || map == null) return "?";
			if (!RimMarketMod.Settings.specialOrders) return "RimMarket.WantedDisabled".Translate();
			if (!mgr.HasOpenMarket(map)) return "RimMarket.RouteNeedsMarket".Translate();
			if (mgr.Notice != null) return "RimMarket.WantedAlreadyOut".Translate();
			return null;
		}

		private static bool Answers(Faction f)
		{
			if (f == null || f.IsPlayer || f.defeated || f.Hidden || f.temporary) return false;
			if (!f.def.humanlikeFaction || f.def.permanentEnemy || f.def.caravanTraderKinds.NullOrEmpty()) return false;
			return !f.HostileTo(Faction.OfPlayer);
		}

		private static bool CanSupplyAny(Faction f, List<ThingDefCountClass> goods)
		{
			for (int i = 0; i < goods.Count; i++)
			{
				if (SpecialOrderUtility.Orderable(goods[i].thingDef, f)) return true;
			}
			return false;
		}

		public static int Post(Map map, List<ThingDefCountClass> goods)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || map == null || goods.NullOrEmpty()) return 0;
			var notice = new WantedNotice
			{
				home = map.Parent,
				goods = goods,
				postedTick = Now,
				repliesEndTick = Now + ReplyMaxTicks,
			};
			var who = new List<Faction>();
			foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
			{
				if (!Answers(f) || !CanSupplyAny(f, goods)) continue;
				if (SpecialOrderUtility.KindFor(f, map, null) == null) continue;
				float chance = BaseReplyChance + f.PlayerGoodwill / 250f;
				if (mgr.HoldsGrudge(f)) chance *= GrudgeReplyFactor;
				if (!Rand.Chance(Mathf.Clamp(chance, 0.15f, 0.95f))) continue;
				who.Add(f);
			}
			who.Shuffle();
			for (int i = 0; i < who.Count && i < MaxReplies; i++)
			{
				notice.pending.Add(who[i]);
				notice.pendingTicks.Add(Now + Rand.Range(ReplyMinTicks, ReplyMaxTicks));
			}
			if (notice.pending.Count > 0) mgr.Notice = notice;
			return notice.pending.Count;
		}

		public static void Withdraw()
		{
			if (Mgr == null) return;
			Mgr.Notice = null;
		}

		public static void Tick(MarketManager mgr)
		{
			WantedNotice n = mgr.Notice;
			if (n == null) return;
			Map map = n.home?.Map;
			if (map == null)
			{
				mgr.Notice = null;
				return;
			}
			for (int i = n.pending.Count - 1; i >= 0; i--)
			{
				if (n.pendingTicks[i] > Now) continue;
				Faction f = n.pending[i];
				n.pending.RemoveAt(i);
				n.pendingTicks.RemoveAt(i);
				if (!Answers(f)) continue;
				WantedOffer offer = MakeOffer(n, f, map);
				if (offer == null) continue;
				n.offers.Add(offer);
				Messages.Message("RimMarket.WantedOfferIn".Translate(f.Name, offer.Barter ? "RimMarket.WantedBarterWord".Translate() : "RimMarket.WantedSilverWord".Translate(offer.price)),
					MessageTypeDefOf.PositiveEvent, historical: true);
			}
			n.offers.RemoveAll(o => o.expiresTick <= Now || !Answers(o.faction) || o.settlement == null || !o.settlement.Spawned);
			if (n.pending.Count > 0 || n.offers.Count > 0) return;
			mgr.Notice = null;
			Messages.Message("RimMarket.WantedClosed".Translate(), MessageTypeDefOf.NeutralEvent, historical: true);
		}

		private static WantedOffer MakeOffer(WantedNotice n, Faction f, Map map)
		{
			PlanetTile home = map.Tile;
			Settlement town = null;
			List<int> path = null;
			int travel = 0;
			foreach (Settlement s in Find.WorldObjects.Settlements.Where(s => s.Faction == f && s.Spawned).OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, home)).Take(4))
			{
				if (!TradeRouteUtility.TryPath(s.Tile, home, out List<int> nodes, out int ticks)) continue;
				town = s;
				path = nodes;
				travel = ticks;
				break;
			}
			if (town == null) return null;
			var goods = new List<ThingDefCountClass>();
			bool partial = false;
			for (int i = 0; i < n.goods.Count; i++)
			{
				ThingDefCountClass want = n.goods[i];
				if (!SpecialOrderUtility.Orderable(want.thingDef, f)) continue;
				int count = want.count;
				if (count > 1 && Rand.Chance(PartialChance))
				{
					count = Mathf.Max(1, Mathf.RoundToInt(count * Rand.Range(0.4f, 0.9f)));
					partial = true;
				}
				goods.Add(new ThingDefCountClass(want.thingDef, count));
			}
			if (goods.Count == 0) return null;
			if (goods.Count < n.goods.Count) partial = true;
			int quality = -1;
			for (int i = 0; i < goods.Count; i++)
			{
				if (goods[i].thingDef.HasComp(typeof(CompQuality))) quality = (int)QualityUtility.GenerateQualityTraderItem();
				if (quality >= 0) break;
			}
			float value = ValueOf(goods, quality, f);
			float liking = Mathf.InverseLerp(-100f, 100f, f.PlayerGoodwill);
			float markup = Mathf.Lerp(StrangerMarkup, FriendMarkup, liking) * Rand.Range(0.95f, 1.05f);
			int tiles = path.Count - 1;
			int price = Round10(value * markup + SpecialOrderUtility.TripPrice(tiles) * TripFactor);
			var offer = new WantedOffer
			{
				id = n.nextOfferId++,
				faction = f,
				settlement = town,
				goods = goods,
				quality = quality,
				price = price,
				tiles = tiles,
				travelTicks = travel,
				risk = TradeRouteUtility.TripRisk(path, town.Tile.Layer, f, false, out Faction threat),
				threat = threat,
				partial = partial,
				expiresTick = Now + OfferLifeTicks,
			};
			if (Rand.Chance(BarterChance)) offer.barter = PickBarter(map, Round10(value * BarterValue), n.goods);
			if (offer.Barter) offer.barterFallback = Round10(price * BarterSurcharge);
			return offer;
		}

		public static float ValueOf(List<ThingDefCountClass> goods, int quality, Faction f)
		{
			float total = 0f;
			for (int i = 0; i < goods.Count; i++)
			{
				Thing sample = SpecialOrderUtility.MakeGood(goods[i].thingDef, f, quality, random: false);
				total += sample.MarketValue * goods[i].count;
			}
			return total;
		}

		private static Dictionary<ThingDef, int> StoredStock(Map map)
		{
			var stock = new Dictionary<ThingDef, int>();
			List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
			for (int i = 0; i < things.Count; i++)
			{
				Thing t = things[i];
				if (!Stored(t)) continue;
				stock.TryGetValue(t.def, out int n);
				stock[t.def] = n + t.stackCount;
			}
			return stock;
		}

		private static bool Stored(Thing t)
		{
			return t.Spawned && t.def.category == ThingCategory.Item && t.def != ThingDefOf.Silver && t.IsInAnyStorage()
				&& !t.IsForbidden(Faction.OfPlayer) && t.def.tradeability.PlayerCanSell();
		}

		private static List<ThingDefCountClass> PickBarter(Map map, int price, List<ThingDefCountClass> wanted)
		{
			return price <= 0 ? new List<ThingDefCountClass>() : PickBarterWorth(map, price, wanted);
		}

		private static List<ThingDefCountClass> PickBarterWorth(Map map, int price, List<ThingDefCountClass> wanted)
		{
			var result = new List<ThingDefCountClass>();
			Dictionary<ThingDef, int> stock = StoredStock(map);
			var defs = stock.Keys.Where(d => !wanted.Any(w => w.thingDef == d)).ToList();
			defs.Shuffle();
			for (int i = 0; i < defs.Count; i++)
			{
				ThingDef d = defs[i];
				float unit = SpecialOrderUtility.UnitValue(d);
				if (unit <= 0f || unit > price * (1f + BarterSlack)) continue;
				int count = Mathf.Max(1, Mathf.RoundToInt(price / unit));
				if (count > stock[d]) continue;
				float total = unit * count;
				if (Mathf.Abs(total - price) > price * BarterSlack) continue;
				result.Add(new ThingDefCountClass(d, count));
				return result;
			}
			return result;
		}

		public static string CannotAccept(WantedOffer o, Map map)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || map == null || o == null) return "?";
			if (!Answers(o.faction)) return "RimMarket.RouteHostile".Translate();
			if (SpecialOrderUtility.OrderFrom(mgr, o.faction) != null) return "RimMarket.OrderPending".Translate(o.faction.Name);
			if (mgr.Orders.Count >= SpecialOrderUtility.MaxOrders) return "RimMarket.OrderLimit".Translate(SpecialOrderUtility.MaxOrders);
			if (!o.Barter && AuctionUtility.ColonySilver(map) < o.price) return "RimMarket.RouteNoSilver".Translate(o.price);
			return null;
		}

		public static Pawn BestNegotiator(Map map)
		{
			if (map == null) return null;
			Pawn best = null;
			int bestLevel = -1;
			foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
			{
				if (p.Downed || p.Dead || p.InMentalState || p.skills == null) continue;
				SkillRecord social = p.skills.GetSkill(SkillDefOf.Social);
				if (social == null || social.TotallyDisabled || social.Level <= bestLevel) continue;
				best = p;
				bestLevel = social.Level;
			}
			return best;
		}

		public static float HaggleChance(WantedOffer o, Pawn negotiator, int step)
		{
			int social = negotiator?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			float chance = HaggleBase[step] + HaggleSkillWeight * (social - HaggleSkillPivot) + (o.faction?.PlayerGoodwill ?? 0) * HaggleGoodwillWeight;
			return Mathf.Clamp(chance, HaggleMinChance, HaggleMaxChance);
		}

		public static void Haggle(WantedOffer o, Map map, Pawn negotiator, int step)
		{
			MarketManager mgr = Mgr;
			WantedNotice n = mgr?.Notice;
			if (n == null || o == null || o.haggled || negotiator == null || step < 0 || step >= HaggleCuts.Length) return;
			o.haggled = true;
			negotiator.skills?.Learn(SkillDefOf.Social, HaggleXp, direct: true);
			string town = o.settlement?.LabelCap ?? "?";
			if (Rand.Chance(HaggleChance(o, negotiator, step)))
			{
				float keep = 1f - HaggleCuts[step];
				if (o.Barter)
				{
					for (int i = 0; i < o.barter.Count; i++) o.barter[i].count = Mathf.Max(1, Mathf.RoundToInt(o.barter[i].count * keep));
					o.barterFallback = Round10(o.barterFallback * keep);
					Messages.Message("RimMarket.WantedHaggleWonBarter".Translate(negotiator.LabelShort, town), MessageTypeDefOf.PositiveEvent, historical: false);
				}
				else
				{
					o.price = Round10(o.price * keep);
					Messages.Message("RimMarket.WantedHaggleWon".Translate(negotiator.LabelShort, town, o.price), MessageTypeDefOf.PositiveEvent, historical: false);
				}
				return;
			}
			if (Rand.Chance(HaggleWalkout[step]))
			{
				n.offers.Remove(o);
				Messages.Message("RimMarket.WantedHaggleWalked".Translate(town), MessageTypeDefOf.NegativeEvent, historical: false);
				return;
			}
			Messages.Message("RimMarket.WantedHaggleRefused".Translate(town), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		public static bool HasBarterNow(WantedOffer o, Map map)
		{
			return o.Barter && HasGoods(map, o.barter);
		}

		public static void Accept(WantedOffer o, Map map)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || CannotAccept(o, map) != null) return;
			if (!o.Barter && !AuctionUtility.TakeColonySilver(map, o.price)) return;
			SpecialOrder order = SpecialOrderUtility.Place(o.faction, o.settlement, map, null, false, Copy(o.goods), o.Barter ? 0 : o.price);
			if (order == null) return;
			order.quality = o.quality;
			order.barter = Copy(o.barter);
			order.barterFallback = o.barterFallback;
			order.departTick = Now + AcceptPrepTicks;
			mgr.Notice = null;
			Messages.Message("RimMarket.WantedAccepted".Translate(o.faction.Name, o.settlement.LabelCap), MessageTypeDefOf.PositiveEvent, historical: true);
		}

		private static List<ThingDefCountClass> Copy(List<ThingDefCountClass> list)
		{
			var copy = new List<ThingDefCountClass>();
			for (int i = 0; i < list.Count; i++) copy.Add(new ThingDefCountClass(list[i].thingDef, list[i].count));
			return copy;
		}

		public static bool HasGoods(Map map, List<ThingDefCountClass> goods)
		{
			for (int i = 0; i < goods.Count; i++)
			{
				int have = 0;
				List<Thing> things = map.listerThings.ThingsOfDef(goods[i].thingDef);
				for (int j = 0; j < things.Count; j++)
				{
					if (Stored(things[j])) have += things[j].stackCount;
				}
				if (have < goods[i].count) return false;
			}
			return true;
		}

		private static void TakeGoods(Map map, List<ThingDefCountClass> goods, Pawn taker)
		{
			for (int i = 0; i < goods.Count; i++)
			{
				int left = goods[i].count;
				List<Thing> things = new List<Thing>(map.listerThings.ThingsOfDef(goods[i].thingDef));
				for (int j = 0; j < things.Count && left > 0; j++)
				{
					Thing t = things[j];
					if (!Stored(t)) continue;
					int n = Mathf.Min(left, t.stackCount);
					Thing part = t.SplitOff(n);
					left -= n;
					if (taker?.inventory == null || !taker.inventory.innerContainer.TryAdd(part)) part.Destroy();
				}
			}
		}

		public static bool SettleBarter(SpecialOrder o, Map map, Pawn trader)
		{
			if (o.barter.NullOrEmpty()) return true;
			if (HasGoods(map, o.barter))
			{
				TakeGoods(map, o.barter, trader);
				Messages.Message("RimMarket.WantedBarterTaken".Translate(o.faction.Name, ListOf(o.barter)), new LookTargets(trader), MessageTypeDefOf.NeutralEvent, historical: true);
				return true;
			}
			if (o.barterFallback > 0 && AuctionUtility.TakeColonySilver(map, o.barterFallback))
			{
				Messages.Message("RimMarket.WantedBarterSilver".Translate(o.faction.Name, o.barterFallback), new LookTargets(trader), MessageTypeDefOf.NegativeEvent, historical: true);
				return true;
			}
			o.faction.TryAffectGoodwillWith(Faction.OfPlayer, RefusedGoodwill, canSendMessage: true, canSendHostilityLetter: true);
			Find.LetterStack.ReceiveLetter("RimMarket.WantedBarterFailedLabel".Translate(), "RimMarket.WantedBarterFailedText".Translate(o.faction.Name, ListOf(o.barter), o.barterFallback),
				LetterDefOf.NegativeEvent, new LookTargets(trader));
			return false;
		}

		public static string ListOf(List<ThingDefCountClass> goods)
		{
			var parts = new List<string>();
			for (int i = 0; i < goods.Count; i++) parts.Add(goods[i].count + "x " + goods[i].thingDef.label);
			return parts.ToCommaList(useAnd: true);
		}
	}
}
