using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public static class ColonyStalls
	{
		private const int SocialPivot = 6;
		private const float SocialPerLevel = 0.015f;
		private const float KeeperBuyBonus = 0.1f;
		private const float SaleXp = 30f;
		private const int KeeperFreshTicks = 120;
		private const int CacheTicks = 60;
		private const int InspectOrders = 5;

		public const float DefaultPriceFactor = 1.1f;
		public const float MinPriceFactor = 0.5f;
		public const float MaxPriceFactor = 3f;
		public const float PriceStep = 0.05f;
		public const int ShopHoursStart = 8;
		public const int ShopHoursEnd = 18;
		public const float BargainPull = 1.5f;

		private static readonly List<Building_MarketStall> buffer = new List<Building_MarketStall>();
		private static Map openMap;
		private static int openTick = -99999;
		private static bool openResult;
		private static Map stockedMap;
		private static int stockedTick = -99999;
		private static Building_MarketStall stockedResult;
		private static Map customerMap;
		private static int customerTick = -99999;
		private static bool customerResult;

		public static bool KeepersNeeded => RimMarketMod.Settings.shopkeepers;

		public static void Collect(Map map, List<Building_MarketStall> into)
		{
			into.Clear();
			if (map == null) return;
			List<ThingDef> defs = MarketStallDefs.All;
			for (int d = 0; d < defs.Count; d++)
			{
				List<Thing> things = map.listerThings.ThingsOfDef(defs[d]);
				for (int i = 0; i < things.Count; i++)
				{
					if (things[i] is Building_MarketStall s && s.ColonyStall) into.Add(s);
				}
			}
		}

		public static bool IsColonyStall(Thing t) => t is Building_MarketStall s && s.ColonyStall;

		public static Pawn KeeperAt(Building_MarketStall s)
		{
			Pawn k = s?.keeper;
			if (k == null || !k.Spawned || k.Map != s.Map || k.Downed) return null;
			if (Find.TickManager.TicksGame - s.keeperTick > KeeperFreshTicks) return null;
			return k;
		}

		public static ThingDef StockDef(Thing t) => t is MinifiedThing m && m.InnerThing != null ? m.InnerThing.def : t?.def;

		public static bool IsArt(ThingDef def) => def != null && def.category == ThingCategory.Building && def.Minifiable && def.IsArt;

		public static bool CanSell(ThingDef def)
		{
			if (IsArt(def)) return true;
			if (def == null || def.category != ThingCategory.Item || def == ThingDefOf.Silver) return false;
			if (def.IsCorpse || def.thingClass == null || typeof(MinifiedThing).IsAssignableFrom(def.thingClass)) return false;
			return def.EverHaulable && def.BaseMarketValue > 0f;
		}

		private static bool Sellable(Thing t, Building_MarketStall s)
		{
			return t != null && !t.Destroyed && t.stackCount > 0 && t.def != ThingDefOf.Silver && MarketUtility.Tradeable(t) && s.OrderFor(StockDef(t)) != null;
		}

		public static bool HasStock(Building_MarketStall s)
		{
			IReadOnlyList<Thing> stock = s.Stock;
			for (int i = 0; i < stock.Count; i++)
			{
				if (Sellable(stock[i], s)) return true;
			}
			return false;
		}

		public static bool InMarket(Building_MarketStall s)
		{
			CompMarketStall c = s.Stall;
			return c != null && MarketUtility.InOpenMarket(c);
		}

		public static bool OpenForSale(Building_MarketStall s)
		{
			if (s == null || !s.ColonyStall || !s.Spawned || s.Faction != Faction.OfPlayer) return false;
			if (!InMarket(s)) return false;
			if (KeepersNeeded && KeeperAt(s) == null) return false;
			return HasStock(s);
		}

		public static bool AnyOpen(Map map)
		{
			if (map == null) return false;
			int now = Find.TickManager.TicksGame;
			if (map == openMap && now - openTick < CacheTicks && now >= openTick) return openResult;
			openMap = map;
			openTick = now;
			openResult = false;
			Collect(map, buffer);
			for (int i = 0; i < buffer.Count; i++)
			{
				if (OpenForSale(buffer[i]))
				{
					openResult = true;
					break;
				}
			}
			return openResult;
		}

		public static Building_MarketStall FirstStocked(Map map)
		{
			if (map == null) return null;
			int now = Find.TickManager.TicksGame;
			if (map == stockedMap && now - stockedTick < CacheTicks && now >= stockedTick
				&& (stockedResult == null || (stockedResult.Spawned && stockedResult.Map == map))) return stockedResult;
			stockedMap = map;
			stockedTick = now;
			stockedResult = null;
			Collect(map, buffer);
			for (int i = 0; i < buffer.Count; i++)
			{
				Building_MarketStall s = buffer[i];
				if (s.Faction == Faction.OfPlayer && InMarket(s) && HasStock(s))
				{
					stockedResult = s;
					break;
				}
			}
			return stockedResult;
		}

		public static bool AnyStocked(Map map) => FirstStocked(map) != null;

		public static void OpenStalls(Map map, List<Building_MarketStall> into)
		{
			Collect(map, into);
			into.RemoveAll(s => !OpenForSale(s));
		}

		public static bool IsCustomer(Pawn p)
		{
			if (p == null || !p.Spawned || p.Dead || p.Downed || p.Faction == null || p.Faction.IsPlayer) return false;
			if (!p.RaceProps.Humanlike || p.HostileTo(Faction.OfPlayer)) return false;
			if (MarketUtility.SilverOf(p) < MarketUtility.MinGuestPurchase) return false;
			if (p.GetLord()?.LordJob is LordJob_MarketShoppers) return true;
			return RimMarketMod.Settings.guestsShop && MarketUtility.IsShoppingGuest(p);
		}

		public static bool CustomersAround(Map map)
		{
			if (map == null) return false;
			int now = Find.TickManager.TicksGame;
			if (map == customerMap && now - customerTick < CacheTicks && now >= customerTick) return customerResult;
			customerMap = map;
			customerTick = now;
			customerResult = false;
			IReadOnlyList<Pawn> pawns = map.mapPawns.AllHumanlikeSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				if (IsCustomer(pawns[i]))
				{
					customerResult = true;
					break;
				}
			}
			return customerResult;
		}

		public static float KeeperFactor(Building_MarketStall s)
		{
			SkillRecord social = KeeperAt(s)?.skills?.GetSkill(SkillDefOf.Social);
			if (social == null || social.TotallyDisabled) return 1f;
			return 1f + (social.Level - SocialPivot) * SocialPerLevel;
		}

		public static float ChanceFor(float priceFactor)
		{
			if (priceFactor <= 0.85f) return 0.9f;
			if (priceFactor <= 1.1f) return Mathf.Lerp(0.9f, 0.7f, (priceFactor - 0.85f) / 0.25f);
			if (priceFactor <= 1.4f) return Mathf.Lerp(0.7f, 0.4f, (priceFactor - 1.1f) / 0.3f);
			if (priceFactor <= 2f) return Mathf.Lerp(0.4f, 0.1f, (priceFactor - 1.4f) / 0.6f);
			return 0.1f;
		}

		public static float PullFor(float priceFactor)
		{
			if (priceFactor <= 0.85f) return 2f;
			if (priceFactor <= 1.1f) return Mathf.Lerp(2f, 1f, (priceFactor - 0.85f) / 0.25f);
			if (priceFactor <= 1.4f) return Mathf.Lerp(1f, 0.6f, (priceFactor - 1.1f) / 0.3f);
			if (priceFactor <= 2f) return Mathf.Lerp(0.6f, 0.3f, (priceFactor - 1.4f) / 0.6f);
			return 0.3f;
		}

		public static float Pull(Map map)
		{
			Collect(map, buffer);
			float sum = 0f;
			int lines = 0;
			for (int i = 0; i < buffer.Count; i++)
			{
				Building_MarketStall s = buffer[i];
				if (s.Faction != Faction.OfPlayer || !InMarket(s)) continue;
				IReadOnlyList<StallOrder> orders = s.Orders;
				for (int j = 0; j < orders.Count; j++)
				{
					sum += orders[j].priceFactor;
					lines++;
				}
			}
			if (lines == 0) return 1f;
			float effect = RimMarketMod.Settings.bargainEffect;
			return Mathf.Max(0.1f, 1f + (PullFor(sum / lines) - 1f) * effect);
		}

		private static float OpenShare => (ShopHoursEnd - ShopHoursStart) / 24f;

		public static float ShopperMtbDays(float pull)
		{
			return RimMarketMod.Settings.colonyShopperDays / Mathf.Max(0.05f, pull) * OpenShare;
		}

		public static int ShopperIntervalTicks(float pull)
		{
			return Mathf.RoundToInt(RimMarketMod.Settings.colonyShopperDays / Mathf.Max(0.05f, pull) * GenDate.TicksPerDay);
		}

		public static float BaseValue(Thing t)
		{
			Thing item = t is MinifiedThing m && m.InnerThing != null ? m.InnerThing : t;
			return t.MarketValue * item.GetStatValue(StatDefOf.SellPriceFactor);
		}

		public static float BaseValue(ThingDef def) => def.BaseMarketValue * def.GetStatValueAbstract(StatDefOf.SellPriceFactor);

		private static float UnitPrice(Thing t, float factor) => Mathf.Max(1f, BaseValue(t) * factor);

		public static int ShownPrice(float unitValue, float factor) => Mathf.Max(1, Mathf.RoundToInt(unitValue * factor));

		public static bool Sell(Pawn buyer, Building_MarketStall s, out Thing bought, out int price)
		{
			bought = null;
			price = 0;
			if (buyer?.inventory == null || !OpenForSale(s)) return false;
			int silver = MarketUtility.SilverOf(buyer);
			if (silver < MarketUtility.MinGuestPurchase) return false;
			float keeper = KeeperFactor(s);
			var affordable = new List<Thing>();
			IReadOnlyList<Thing> stock = s.Stock;
			for (int i = 0; i < stock.Count; i++)
			{
				Thing t = stock[i];
				if (!Sellable(t, s)) continue;
				if (UnitPrice(t, s.OrderFor(StockDef(t)).priceFactor * keeper) <= silver) affordable.Add(t);
			}
			if (affordable.Count == 0) return false;
			Thing pick = affordable.RandomElement();
			float baseFactor = s.OrderFor(StockDef(pick)).priceFactor;
			float chance = ChanceFor(baseFactor) + (KeeperAt(s) != null ? KeeperBuyBonus : 0f);
			if (!Rand.Chance(Mathf.Clamp01(chance))) return false;
			float unit = UnitPrice(pick, baseFactor * keeper);
			int max = Mathf.Min(pick.stackCount, Mathf.FloorToInt(silver / unit), MarketUtility.MaxUnitsPerPurchase);
			if (max < 1) return false;
			int count = Rand.RangeInclusive(1, max);
			int cost = Mathf.Max(1, Mathf.CeilToInt(unit * count));
			Thing split = s.StockOwner.Take(pick, count);
			if (split == null) return false;
			s.Notify_Sold(StockDef(split), split.stackCount);
			VteBridge.Notify_PlayerSold(split, split.stackCount);
			price = MarketUtility.TakeAndPlacePayment(new List<Pawn> { buyer }, cost, false, CashCell(s), s.Map);
			buyer.inventory.TryAddItemNotForSale(split);
			bought = split;
			s.takings += price;
			s.sales++;
			MarketManager.Instance?.RecordColonySale(price);
			HegemonyBridge.RecordTrade(buyer.Faction, price);
			KeeperAt(s)?.skills?.Learn(SkillDefOf.Social, SaleXp);
			return true;
		}

		private static IntVec3 CashCell(Building_MarketStall s)
		{
			CompMarketStall c = s.Stall;
			if (c != null && c.MerchantCell.InBounds(s.Map) && c.MerchantCell.Standable(s.Map)) return c.MerchantCell;
			return s.Position;
		}

		public static string InspectText(Building_MarketStall s)
		{
			var lines = new List<string>();
			IReadOnlyList<StallOrder> orders = s.Orders;
			if (orders.Count == 0) lines.Add("RimMarket.ColonyStallEmpty".Translate());
			else lines.Add("RimMarket.ColonyStallForSale".Translate());
			for (int i = 0; i < orders.Count && i < InspectOrders; i++)
			{
				StallOrder o = orders[i];
				lines.Add("RimMarket.ColonyStallOrderLine".Translate(o.def.LabelCap, o.toSell, s.HeldOf(o.def), o.priceFactor.ToStringPercent()));
			}
			if (orders.Count > InspectOrders) lines.Add("RimMarket.ColonyStallMoreOrders".Translate(orders.Count - InspectOrders));
			if (s.Spawned)
			{
				CompMarketStall c = s.Stall;
				Building_MarketMarker marker = c == null ? null : MarketUtility.MarkerFor(c);
				if (marker == null) lines.Add("RimMarket.StallNoMarker".Translate());
				else if (!marker.Open) lines.Add("RimMarket.StallMarketClosed".Translate());
			}
			if (KeepersNeeded)
			{
				Pawn k = KeeperAt(s);
				lines.Add(k != null ? "RimMarket.ColonyStallKeeper".Translate(k.LabelShortCap).ToString() : "RimMarket.ColonyStallNoKeeper".Translate().ToString());
			}
			if (s.sales > 0) lines.Add("RimMarket.ColonyStallTakings".Translate(s.takings, s.sales));
			return string.Join("\n", lines);
		}
	}
}
