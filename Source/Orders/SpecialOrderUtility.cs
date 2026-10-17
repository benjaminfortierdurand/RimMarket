using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Orders
{
	public static class SpecialOrderUtility
	{
		public const int MaxOrders = 3;
		public const int MaxLines = 10;
		public const float MaxGoodsValue = 25000f;
		private const int TicksPerDay = 60000;
		private const int FamilyPrepTicks = 60000;
		private const int SpecificPrepTicks = 120000;
		private const int GiveUpTicks = 60000;
		private const int MaxPathFailures = 3;
		private const float SmallFactor = 2f;
		private const float LargeFactor = 3f;
		private const float SpecificFactor = 3f;
		private const float FinderFee = 0.5f;
		private const float SmallBudget = 1500f;
		private const float LargeBudget = 3500f;
		private const int FamilyTries = 60;
		private const float LostRefund = 0.5f;
		private const int HandOverTimeout = 6000;

		public sealed class OrderFamily
		{
			public string key;
			public string[] categories;
			public string[] exclude = new string[0];
			public string[] extraDefs = new string[0];
			public string kindHint;

			public string Label => ("RimMarket.OrderFamily_" + key).Translate();
		}

		public static readonly OrderFamily[] Families =
		{
			new OrderFamily { key = "Provisions", categories = new[] { "Foods", "Medicine" }, kindHint = "BulkGoods" },
			new OrderFamily { key = "Materials", categories = new[] { "Textiles", "StoneBlocks", "ResourcesRaw", "Manufactured" }, exclude = new[] { "Drugs", "Medicine" }, kindHint = "BulkGoods" },
			new OrderFamily { key = "Arms", categories = new[] { "Weapons", "ApparelArmor" }, kindHint = "CombatSupplier" },
			new OrderFamily { key = "Luxuries", categories = new[] { "Drugs", "Apparel" }, exclude = new[] { "ApparelArmor" }, extraDefs = new[] { "Gold", "Jade" }, kindHint = "Exotic" },
		};

		private static MarketManager Mgr => MarketManager.Instance;

		private static bool ShowMessages => RimMarketMod.Settings.showMessages;

		private static int Round10(float v) => 10 * Mathf.RoundToInt(v / 10f);

		public static int TripPrice(int tiles) => TradeRouteUtility.Price(tiles, TradeRouteUtility.Fortnightly, false);

		public static int FamilyPrice(int tiles, bool large) => Round10(TripPrice(tiles) * (large ? LargeFactor : SmallFactor));

		public static int SpecificPrice(int tiles, float goodsValue) => Round10(TripPrice(tiles) * SpecificFactor + goodsValue * FinderFee);

		public static OrderFamily FamilyByKey(string key)
		{
			for (int i = 0; i < Families.Length; i++)
			{
				if (Families[i].key == key) return Families[i];
			}
			return null;
		}

		public static bool Orderable(ThingDef d, Faction faction)
		{
			if (d == null || d.category != ThingCategory.Item || d == ThingDefOf.Silver) return false;
			if (!d.tradeability.TraderCanSell() || d.BaseMarketValue <= 0.01f || d.destroyOnDrop || d.isUnfinishedThing) return false;
			if (d.thingCategories.NullOrEmpty() || d.IsCorpse || d.thingClass == typeof(MinifiedThing)) return false;
			if (typeof(Pawn).IsAssignableFrom(d.thingClass)) return false;
			if (d.MadeFromStuff && GenStuff.DefaultStuffFor(d) == null) return false;
			if (d.ingestible?.sourceDef?.race?.Humanlike == true) return false;
			if (faction != null && d.techLevel != TechLevel.Undefined && d.techLevel > faction.def.techLevel) return false;
			return true;
		}

		private static bool InFamily(ThingDef d, OrderFamily fam)
		{
			for (int i = 0; i < fam.extraDefs.Length; i++)
			{
				if (d.defName == fam.extraDefs[i]) return true;
			}
			for (int i = 0; i < fam.exclude.Length; i++)
			{
				ThingCategoryDef ex = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(fam.exclude[i]);
				if (ex != null && d.IsWithinCategory(ex)) return false;
			}
			for (int i = 0; i < fam.categories.Length; i++)
			{
				ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(fam.categories[i]);
				if (cat != null && d.IsWithinCategory(cat)) return true;
			}
			return false;
		}

		public static List<ThingDef> Candidates(OrderFamily fam, Faction faction)
		{
			var list = new List<ThingDef>();
			List<ThingDef> all = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < all.Count; i++)
			{
				ThingDef d = all[i];
				if (!Orderable(d, faction)) continue;
				if (fam != null && !InFamily(d, fam)) continue;
				list.Add(d);
			}
			return list;
		}

		public static float UnitValue(ThingDef d)
		{
			return d.GetStatValueAbstract(StatDefOf.MarketValue, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null);
		}

		public static float GoodsValue(List<ThingDefCountClass> goods)
		{
			float total = 0f;
			for (int i = 0; i < goods.Count; i++) total += UnitValue(goods[i].thingDef) * goods[i].count;
			return total;
		}

		public static TraderKindDef KindFor(Faction faction, Map map, string hint)
		{
			List<TraderKindDef> kinds = TradeRouteUtility.KindsFor(faction, map, null);
			if (kinds.Count == 0) return null;
			if (!hint.NullOrEmpty())
			{
				for (int i = 0; i < kinds.Count; i++)
				{
					if (kinds[i].defName.Contains(hint)) return kinds[i];
				}
			}
			return kinds[0];
		}

		public static SpecialOrder OrderFrom(MarketManager mgr, Faction faction)
		{
			if (mgr == null || faction == null) return null;
			IReadOnlyList<SpecialOrder> orders = mgr.Orders;
			for (int i = 0; i < orders.Count; i++)
			{
				if (orders[i].faction == faction) return orders[i];
			}
			return null;
		}

		public static string CannotOrder(MarketManager mgr, Faction faction, Map map)
		{
			if (mgr == null || faction == null || map == null) return "?";
			if (faction.HostileTo(Faction.OfPlayer)) return "RimMarket.RouteHostile".Translate();
			if (!mgr.HasOpenMarket(map)) return "RimMarket.RouteNeedsMarket".Translate();
			if (mgr.LedgerFor(faction) == null) return "RimMarket.RouteUnknown".Translate(faction.Name);
			if (mgr.HoldsGrudge(faction)) return "RimMarket.RouteGrudge".Translate(faction.Name);
			if (OrderFrom(mgr, faction) != null) return "RimMarket.OrderPending".Translate(faction.Name);
			if (mgr.Orders.Count >= MaxOrders) return "RimMarket.OrderLimit".Translate(MaxOrders);
			if (KindFor(faction, map, null) == null) return "RimMarket.RouteNoKinds".Translate();
			return null;
		}

		public static SpecialOrder OrderOf(TradeConvoy convoy)
		{
			return convoy == null || convoy.orderId <= 0 ? null : Mgr?.OrderById(convoy.orderId);
		}

		public static SpecialOrder Place(Faction faction, Settlement settlement, Map home, OrderFamily fam, bool large, List<ThingDefCountClass> goods, int paid)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || faction == null || settlement == null || home == null) return null;
			int now = Find.TickManager.TicksGame;
			var order = new SpecialOrder
			{
				id = mgr.NextOrderId(),
				faction = faction,
				settlement = settlement,
				home = home.Parent,
				kind = KindFor(faction, home, fam?.kindHint),
				family = fam?.key,
				large = large,
				goods = goods ?? new List<ThingDefCountClass>(),
				paid = paid,
				placedTick = now,
				departTick = now + (fam == null ? SpecificPrepTicks : FamilyPrepTicks),
			};
			mgr.AddOrder(order);
			return order;
		}

		private static string BrokenReason(SpecialOrder o)
		{
			if (o.faction == null || o.faction.defeated) return "RimMarket.OrderCancelledGone";
			if (o.settlement == null || !o.settlement.Spawned || o.settlement.Faction != o.faction) return "RimMarket.OrderCancelledGone";
			if (o.faction.HostileTo(Faction.OfPlayer)) return "RimMarket.OrderCancelledHostile";
			if (o.home == null || !o.home.Spawned || !o.home.HasMap) return "RimMarket.OrderCancelledHome";
			if (o.kind == null) return "RimMarket.OrderCancelledGone";
			return null;
		}

		public static void OrdersTick(MarketManager mgr)
		{
			var copy = new List<SpecialOrder>(mgr.Orders);
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < copy.Count; i++)
			{
				SpecialOrder o = copy[i];
				if (o.delivered)
				{
					if (!o.handedOver && o.courier != null && o.courier.Spawned && now - o.arrivedTick >= HandOverTimeout)
						HandOver(o, o.courier.Position, o.courier.Map);
					if (!LordAlive(o)) mgr.RemoveOrder(o);
					continue;
				}
				string broken = BrokenReason(o);
				if (broken != null)
				{
					Cancel(o, broken, o.departed ? LostRefund : 1f);
					continue;
				}
				if (o.convoy != null && (o.convoy.Destroyed || !o.convoy.Spawned)) o.convoy = null;
				if (o.departed && o.convoy == null)
				{
					Cancel(o, "RimMarket.OrderCancelledLost", LostRefund);
					continue;
				}
				if (!o.departed && now >= o.departTick) Depart(o);
			}
		}

		private static bool LordAlive(SpecialOrder o)
		{
			Map map = o.home?.Map;
			if (map == null || o.arrivedLordId < 0) return false;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].loadID == o.arrivedLordId) return true;
			}
			return false;
		}

		private static void Depart(SpecialOrder o)
		{
			int now = Find.TickManager.TicksGame;
			if (!TradeRouteUtility.TryPath(o.settlement.Tile, o.home.Tile, out List<int> nodes, out int ticks))
			{
				o.pathFailures++;
				o.departTick = now + TicksPerDay;
				if (o.pathFailures >= MaxPathFailures) Cancel(o, "RimMarket.OrderCancelledNoRoad", 1f);
				return;
			}
			var convoy = (TradeConvoy)WorldObjectMaker.MakeWorldObject(RimMarketDefOf.RimMarket_TradeConvoy);
			convoy.Tile = o.settlement.Tile;
			convoy.SetFaction(o.faction);
			convoy.Setup(0, nodes);
			convoy.orderId = o.id;
			Find.WorldObjects.Add(convoy);
			o.convoy = convoy;
			o.departed = true;
			if (ShowMessages)
				Messages.Message("RimMarket.OrderLeft".Translate(o.SettlementName, ticks.ToStringTicksToPeriod()),
					new LookTargets(convoy), MessageTypeDefOf.PositiveEvent, historical: false);
		}

		public static bool TryArrive(TradeConvoy convoy)
		{
			SpecialOrder o = OrderOf(convoy);
			Map map = o?.home?.Map;
			if (o == null || map == null || o.faction == null || o.faction.HostileTo(Faction.OfPlayer))
			{
				if (o != null) o.convoy = null;
				if (!convoy.Destroyed) convoy.Destroy();
				return false;
			}
			int now = Find.TickManager.TicksGame;
			bool welcome = MarketTruce.Welcome(map, o.faction);
			if (welcome)
			{
				var parms = new IncidentParms
				{
					target = map,
					faction = o.faction,
					traderKind = o.kind,
					forced = true,
				};
				if (ConvoyBattleUtility.ArriveWith(convoy, () => Compat.RimTollBridge.WithoutBooth(() => IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms))))
				{
					Lord lord = TradeRouteUtility.NewestTraderLord(map, o.faction);
					o.arrivedLordId = lord?.loadID ?? -1;
					o.arrivedTick = now;
					o.delivered = true;
					o.convoy = null;
					Pawn trader = lord == null ? null : TraderCaravanUtility.FindTrader(lord);
					if (trader != null && WantedUtility.SettleBarter(o, map, trader)) Deliver(o, trader);
					if (convoy.plunder > 0f) TradeRouteUtility.Plunder(lord, convoy.plunder);
					if (!convoy.Destroyed) convoy.Destroy();
					Find.LetterStack.ReceiveLetter("RimMarket.OrderArrivedLabel".Translate(),
						"RimMarket.OrderArrivedText".Translate(o.faction.Name, o.SettlementName, What(o)),
						LetterDefOf.PositiveEvent, new LookTargets(trader));
					if (lord == null) Mgr?.RemoveOrder(o);
					return true;
				}
			}
			if (now - convoy.waitingSince >= GiveUpTicks)
			{
				o.convoy = null;
				if (!convoy.Destroyed) convoy.Destroy();
				Cancel(o, "RimMarket.OrderTurnedBack", LostRefund);
				return false;
			}
			if (now == convoy.waitingSince && ShowMessages)
				Messages.Message((welcome ? "RimMarket.RouteConvoyDelayed" : "RimMarket.RouteConvoyWaiting").Translate(o.SettlementName),
					new LookTargets(convoy), MessageTypeDefOf.NeutralEvent, historical: false);
			return false;
		}

		public static string What(SpecialOrder o)
		{
			if (!o.Specific)
			{
				OrderFamily fam = FamilyByKey(o.family);
				string label = fam?.Label ?? o.family;
				return (o.large ? "RimMarket.OrderWhatLarge" : "RimMarket.OrderWhatSmall").Translate(label);
			}
			var parts = new List<string>();
			for (int i = 0; i < o.goods.Count; i++) parts.Add(o.goods[i].count + "x " + o.goods[i].thingDef.label);
			return parts.ToCommaList(useAnd: true);
		}

		private static void Deliver(SpecialOrder o, Pawn trader)
		{
			if (o.Specific)
			{
				ThingOwner bag = trader.inventory?.innerContainer;
				if (bag == null) return;
				List<Thing> parcel = MakeSpecific(o);
				o.courier = trader;
				o.parcel.Clear();
				for (int i = 0; i < parcel.Count; i++)
				{
					if (bag.TryAdd(parcel[i], canMergeWithExistingStacks: false)) o.parcel.Add(parcel[i]);
					else parcel[i].Destroy();
				}
				return;
			}
			var carriers = new List<Pawn>();
			List<Pawn> pawns = trader.GetLord()?.ownedPawns;
			if (pawns != null)
			{
				for (int i = 0; i < pawns.Count; i++)
				{
					if (pawns[i].inventory != null && pawns[i].GetTraderCaravanRole() == TraderCaravanRole.Carrier) carriers.Add(pawns[i]);
				}
			}
			if (carriers.Count == 0 && trader.inventory != null)
			{
				Log.Warning("[RimMarket] the caravan with special order " + o.id + " has no pack animals, the goods ride with the trader and cannot be sold.");
				carriers.Add(trader);
			}
			if (carriers.Count == 0) return;
			List<Thing> things = o.Specific ? MakeSpecific(o) : MakeFamily(FamilyByKey(o.family), o.faction, o.large);
			for (int i = 0; i < things.Count; i++)
			{
				if (!carriers[i % carriers.Count].inventory.innerContainer.TryAdd(things[i])) things[i].Destroy();
			}
		}

		public static Thing MakeGood(ThingDef def, Faction faction, int quality, bool random)
		{
			ThingDef stuff = null;
			if (def.MadeFromStuff)
				stuff = (random ? GenStuff.RandomStuffByCommonalityFor(def, faction.def.techLevel) : null) ?? GenStuff.DefaultStuffFor(def);
			Thing t = ThingMaker.MakeThing(def, stuff);
			QualityCategory q = quality >= 0 ? (QualityCategory)quality : QualityUtility.GenerateQualityTraderItem();
			t.TryGetComp<CompQuality>()?.SetQuality(q, ArtGenerationContext.Outsider);
			return t;
		}

		private static List<Thing> MakeSpecific(SpecialOrder o)
		{
			var made = new List<Thing>();
			for (int i = 0; i < o.goods.Count; i++)
			{
				ThingDef def = o.goods[i].thingDef;
				int left = o.goods[i].count;
				while (left > 0)
				{
					Thing t = MakeGood(def, o.faction, o.quality, random: false);
					int n = Mathf.Min(left, Mathf.Max(1, def.stackLimit));
					t.stackCount = n;
					left -= n;
					made.Add(t);
				}
			}
			return made;
		}

		private static List<Thing> MakeFamily(OrderFamily fam, Faction faction, bool large)
		{
			var made = new List<Thing>();
			if (fam == null) return made;
			List<ThingDef> pool = Candidates(fam, faction);
			if (pool.Count == 0) return made;
			float budget = large ? LargeBudget : SmallBudget;
			float spent = 0f;
			for (int tries = 0; tries < FamilyTries && spent < budget * 0.95f; tries++)
			{
				ThingDef def = pool.RandomElementByWeight(d => 1f / Mathf.Sqrt(Mathf.Max(1f, d.BaseMarketValue)));
				Thing t = MakeGood(def, faction, -1, random: true);
				float unit = t.MarketValue;
				if (unit <= 0f || unit > budget - spent)
				{
					t.Destroy();
					continue;
				}
				int want = Mathf.Clamp(Mathf.FloorToInt(Rand.Range(0.15f, 0.4f) * budget / unit), 1, Mathf.Max(1, def.stackLimit));
				want = Mathf.Min(want, Mathf.FloorToInt((budget - spent) / unit));
				if (want < 1)
				{
					t.Destroy();
					continue;
				}
				t.stackCount = want;
				spent += unit * want;
				made.Add(t);
			}
			return made;
		}

		public static void HandOver(SpecialOrder o, IntVec3 cell, Map map)
		{
			if (o == null || o.handedOver || map == null) return;
			o.handedOver = true;
			ThingOwner bag = o.courier?.inventory?.innerContainer;
			if (bag == null) return;
			var given = new List<ThingDefCountClass>();
			for (int i = 0; i < o.parcel.Count; i++)
			{
				Thing t = o.parcel[i];
				if (t == null || t.Destroyed || !bag.Contains(t)) continue;
				given.Add(new ThingDefCountClass(t.def, t.stackCount));
				bag.TryDrop(t, cell, map, ThingPlaceMode.Near, out _);
			}
			o.parcel.Clear();
			if (given.Count > 0)
				Messages.Message("RimMarket.WantedHandedOver".Translate(o.faction?.Name ?? "?", WantedUtility.ListOf(given)),
					new LookTargets(cell, map), MessageTypeDefOf.PositiveEvent, historical: true);
		}

		public static void Notify_ConvoyLost(TradeConvoy convoy)
		{
			SpecialOrder o = OrderOf(convoy);
			if (o == null) return;
			o.convoy = null;
			Cancel(o, "RimMarket.OrderCancelledLost", LostRefund);
		}

		public static void Cancel(SpecialOrder o, string key, float refundFraction)
		{
			MarketManager mgr = Mgr;
			if (mgr == null || o == null) return;
			mgr.RemoveOrder(o);
			if (o.convoy != null && !o.convoy.Destroyed) o.convoy.Destroy();
			o.convoy = null;
			int refund = Mathf.RoundToInt(o.paid * refundFraction);
			Map map = o.home?.Map;
			if (refund > 0 && map != null) Refund(map, refund);
			Messages.Message(key.Translate(o.faction?.Name ?? "?", o.SettlementName, refund), MessageTypeDefOf.NegativeEvent, historical: true);
		}

		private static void Refund(Map map, int amount)
		{
			IntVec3 spot = IntVec3.Invalid;
			List<Thing> markers = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker);
			for (int i = 0; i < markers.Count; i++)
			{
				if (markers[i].Spawned)
				{
					spot = markers[i].Position;
					break;
				}
			}
			if (!spot.IsValid) spot = DropCellFinder.TradeDropSpot(map);
			while (amount > 0)
			{
				Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
				silver.stackCount = Mathf.Min(amount, ThingDefOf.Silver.stackLimit);
				amount -= silver.stackCount;
				GenPlace.TryPlaceThing(silver, spot, map, ThingPlaceMode.Near);
			}
		}
	}
}
