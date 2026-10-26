using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RimMarket.Market
{
	public static class MarketUtility
	{
		public static void CollectStalls(Map map, List<CompMarketStall> into)
		{
			into.Clear();
			if (map == null) return;
			List<ThingDef> defs = MarketStallDefs.All;
			for (int d = 0; d < defs.Count; d++)
			{
				List<Thing> things = map.listerThings.ThingsOfDef(defs[d]);
				for (int i = 0; i < things.Count; i++)
				{
					CompMarketStall s = MarketStallDefs.StallOf(things[i]);
					if (s != null) into.Add(s);
				}
			}
		}

		private static readonly List<CompMarketStall> stallBuffer = new List<CompMarketStall>();

		public static Building_MarketMarker MarkerFor(CompMarketStall stall)
		{
			if (stall == null || !stall.Spawned) return null;
			Map map = stall.Map;
			Building_MarketMarker best = null;
			float bestDist = float.MaxValue;
			List<Thing> markers = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker);
			for (int i = 0; i < markers.Count; i++)
			{
				if (!(markers[i] is Building_MarketMarker m) || !m.Spawned) continue;
				if (!InQuarter(m.Quarter, stall)) continue;
				float d = m.Position.DistanceToSquared(stall.Position);
				if (d < bestDist)
				{
					bestDist = d;
					best = m;
				}
			}
			return best;
		}

		public static Building_MarketMarker MarkerAt(Map map, IntVec3 cell)
		{
			if (map == null) return null;
			Building_MarketMarker best = null;
			float bestDist = float.MaxValue;
			List<Thing> markers = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker);
			for (int i = 0; i < markers.Count; i++)
			{
				if (!(markers[i] is Building_MarketMarker m) || !m.Spawned || m.Quarter == null || !m.Quarter.InQuarter(cell)) continue;
				float d = m.Position.DistanceToSquared(cell);
				if (d < bestDist)
				{
					bestDist = d;
					best = m;
				}
			}
			return best;
		}

		private static bool InQuarter(CompMarketQuarter quarter, CompMarketStall stall)
		{
			if (quarter == null) return false;
			return quarter.InQuarter(stall.Position)
				|| quarter.InQuarter(stall.CustomerCell)
				|| quarter.InQuarter(stall.MerchantCell);
		}

		public static bool InOpenMarket(CompMarketStall stall)
		{
			Building_MarketMarker m = MarkerFor(stall);
			return m != null && m.Open;
		}

		private static bool IsFreeOpenStall(CompMarketStall s, Map map, MarketManager mgr, CompMarketStall exclude)
		{
			if (s == null || s == exclude || !s.Spawned || s.Map != map) return false;
			if (s.Faction != Faction.OfPlayer || ColonyStalls.IsColonyStall(s.parent)) return false;
			if (mgr != null && mgr.VisitFor(s) != null) return false;
			if (!s.MerchantCell.InBounds(map) || !s.MerchantCell.Standable(map)) return false;
			return InOpenMarket(s);
		}

		public static bool AnyFreeStall(Map map)
		{
			if (map == null) return false;
			MarketManager mgr = MarketManager.Instance;
			CollectStalls(map, stallBuffer);
			for (int i = 0; i < stallBuffer.Count; i++)
			{
				if (IsFreeOpenStall(stallBuffer[i], map, mgr, null)) return true;
			}
			return false;
		}

		public static CompMarketStall FindFreeStall(Map map, Pawn trader, CompMarketStall exclude = null)
		{
			if (map == null || trader == null || !trader.Spawned || trader.Map != map) return null;
			MarketManager mgr = MarketManager.Instance;
			IntVec3 from = trader.Position;
			CompMarketStall best = null;
			float bestDist = float.MaxValue;
			CollectStalls(map, stallBuffer);
			for (int i = 0; i < stallBuffer.Count; i++)
			{
				CompMarketStall s = stallBuffer[i];
				if (!IsFreeOpenStall(s, map, mgr, exclude)) continue;
				if (!trader.CanReach(s.MerchantCell, PathEndMode.OnCell, Danger.Some)) continue;
				float d = from.DistanceToSquared(s.Position);
				if (d < bestDist)
				{
					bestDist = d;
					best = s;
				}
			}
			return best;
		}

		public static int CountStalls(Building_MarketMarker marker, out int free, out int busy)
		{
			free = 0;
			busy = 0;
			if (marker == null || !marker.Spawned) return 0;
			MarketManager mgr = MarketManager.Instance;
			CollectStalls(marker.Map, stallBuffer);
			int n = 0;
			for (int i = 0; i < stallBuffer.Count; i++)
			{
				CompMarketStall s = stallBuffer[i];
				if (!s.Spawned || ColonyStalls.IsColonyStall(s.parent)) continue;
				if (MarkerFor(s) != marker) continue;
				n++;
				if (mgr != null && mgr.VisitFor(s) != null) busy++;
				else free++;
			}
			return n;
		}

		public static bool Tradeable(Thing t)
		{
			return t != null && t.def != null && t.def.stealable && t.MarketValue > 0f && !t.IsBurning();
		}

		public static float CarriedWealth(Lord lord)
		{
			float wealth = 0f;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				ThingOwner inv = lord.ownedPawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				for (int j = 0; j < inv.Count; j++)
				{
					Thing t = inv[j];
					wealth += t.MarketValue * t.stackCount;
				}
			}
			return wealth;
		}

		public static int FeeFor(Lord lord)
		{
			RimMarketSettings s = RimMarketMod.Settings;
			if (TraderCaravanUtility.FindTrader(lord)?.TraderKind?.tradeCurrency == TradeCurrency.Favor) return 0;
			if (s.waiveFeeAfterToll && RimTollBridge.Available && lord.faction != null)
			{
				int last = RimTollBridge.LastTollTick(lord.faction);
				if (last >= 0 && Find.TickManager.TicksGame - last < 60000) return 0;
			}
			float raw = Mathf.Clamp(CarriedWealth(lord) * s.feePercent, 30f, s.feeCap);
			return 5 * Mathf.Max(1, Mathf.RoundToInt(raw / 5f));
		}

		private const float HumanFeedMargin = 0.12f;

		public static void TopUpFood(Lord lord, bool full)
		{
			if (lord == null) return;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				Need_Food food = p?.needs?.food;
				if (food == null || p.Dead) continue;
				if (!full)
				{
					if (p.RaceProps.Humanlike)
					{
						if (food.CurLevelPercentage > food.PercentageThreshHungry + HumanFeedMargin) continue;
					}
					else if (food.CurCategory < HungerCategory.Hungry) continue;
				}
				food.CurLevel = food.MaxLevel;
			}
		}

		private const int PackedMeals = 2;

		public static bool CarriesFood(Pawn p)
		{
			ThingOwner inv = p?.inventory?.innerContainer;
			if (inv == null) return false;
			for (int i = 0; i < inv.Count; i++)
			{
				ThingDef d = inv[i].def;
				if (d.IsNutritionGivingIngestible && d.ingestible.HumanEdible && !d.IsDrug) return true;
			}
			return false;
		}

		public static void PackMeals(Lord lord)
		{
			if (lord == null) return;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p == null || p.Dead || !p.RaceProps.Humanlike || p.inventory == null || p.needs?.food == null) continue;
				if (p.GetTraderCaravanRole() == TraderCaravanRole.Chattel || CarriesFood(p)) continue;
				Thing meals = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
				meals.stackCount = PackedMeals;
				p.inventory.innerContainer.TryAdd(meals);
			}
		}

		public static PawnKindDef CivilianKind(Faction faction)
		{
			FactionDef def = faction?.def;
			if (def == null) return null;
			if (def.basicMemberKind?.RaceProps?.Humanlike == true) return def.basicMemberKind;
			if (def.pawnGroupMakers == null) return null;
			var peaceful = new List<PawnGenOption>();
			PawnKindDef mildest = null;
			float mildestPower = float.MaxValue;
			foreach (PawnGroupMaker maker in def.pawnGroupMakers)
			{
				if (maker?.options == null) continue;
				foreach (PawnGenOption o in maker.options)
				{
					PawnKindDef k = o?.kind;
					if (k?.RaceProps == null || !k.RaceProps.Humanlike || k.factionLeader) continue;
					if (maker.kindDef == PawnGroupKindDefOf.Peaceful && !k.trader) peaceful.Add(o);
					if (k.combatPower < mildestPower)
					{
						mildestPower = k.combatPower;
						mildest = k;
					}
				}
			}
			if (peaceful.TryRandomElementByWeight(o => o.selectionWeight, out PawnGenOption pick)) return pick.kind;
			return mildest;
		}

		public const int MinGuestPurchase = 5;
		public const int MaxUnitsPerPurchase = 10;

		public static bool IsShoppingGuest(Pawn p) => HospitalityBridge.IsArrivedGuest(p) || HotelsBridge.IsGuest(p);

		public static int SilverOf(Pawn p)
		{
			if (HotelsBridge.IsGuest(p)) return HotelsBridge.BudgetOf(p);
			ThingOwner inv = p?.inventory?.innerContainer;
			if (inv == null) return 0;
			int n = 0;
			for (int i = 0; i < inv.Count; i++)
			{
				if (inv[i].def == ThingDefOf.Silver) n += inv[i].stackCount;
			}
			return n;
		}

		public static bool MerchantAtPost(StallVisit v)
		{
			if (v == null || v.closed || !v.StallOk || !v.TraderOk) return false;
			Pawn t = v.trader;
			if (!t.Awake() || t.InMentalState) return false;
			return t.Position.InHorDistOf(v.stall.MerchantCell, 1.5f);
		}

		private static readonly List<Building_MarketStall> colonyBuffer = new List<Building_MarketStall>();

		public static CompMarketStall PickStallToVisit(Pawn pawn, out Pawn seller)
		{
			seller = null;
			MarketManager mgr = MarketManager.Instance;
			if (pawn?.Map == null) return null;
			var stalls = new List<CompMarketStall>();
			var sellers = new List<Pawn>();
			if (mgr != null)
			{
				IReadOnlyList<StallVisit> visits = mgr.Visits;
				for (int i = 0; i < visits.Count; i++)
				{
					StallVisit v = visits[i];
					if (v.map != pawn.Map || !MerchantAtPost(v)) continue;
					if (!pawn.CanReach(v.stall.CustomerCell, PathEndMode.OnCell, Danger.None)) continue;
					stalls.Add(v.stall);
					sellers.Add(v.trader);
				}
			}
			if (!pawn.IsColonist)
			{
				ColonyStalls.OpenStalls(pawn.Map, colonyBuffer);
				for (int i = 0; i < colonyBuffer.Count; i++)
				{
					CompMarketStall c = colonyBuffer[i].Stall;
					if (c == null || !pawn.CanReach(c.CustomerCell, PathEndMode.OnCell, Danger.None)) continue;
					stalls.Add(c);
					sellers.Add(ColonyStalls.KeeperAt(colonyBuffer[i]));
				}
			}
			if (stalls.Count == 0) return null;
			int pick = Rand.Range(0, stalls.Count);
			seller = sellers[pick];
			return stalls[pick];
		}

		public static IntVec3 FindBrowseCell(CompMarketStall stall, Pawn pawn)
		{
			Map map = stall.Map;
			var cells = new List<IntVec3>();
			int near = stall.CustomerReach;
			for (int d = near; d <= near + 1; d++)
			{
				for (int s = -1; s <= 1; s++)
				{
					IntVec3 c = stall.Position + stall.Front * d + stall.Side * s;
					if (c != stall.CustomerCell) cells.Add(c);
				}
			}
			cells.Shuffle();
			cells.Add(stall.CustomerCell);
			for (int i = 0; i < cells.Count; i++)
			{
				IntVec3 c = cells[i];
				if (!c.InBounds(map) || !c.Standable(map)) continue;
				if (!pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.None)) continue;
				return c;
			}
			return IntVec3.Invalid;
		}

		public static Job MakeVisitJob(Pawn pawn, bool shopper = false)
		{
			CompMarketStall stall = PickStallToVisit(pawn, out Pawn seller);
			if (stall == null) return null;
			IntVec3 cell = FindBrowseCell(stall, pawn);
			if (!cell.IsValid) return null;
			bool buyer = !pawn.IsColonist && (shopper || RimMarketMod.Settings.guestsShop) && SilverOf(pawn) >= MinGuestPurchase;
			JobDef def = buyer ? RimMarketDefOf.RimMarket_BuyAtStall : RimMarketDefOf.RimMarket_BrowseStall;
			return JobMaker.MakeJob(def, stall.parent, cell, seller);
		}

		private static float UnitPrice(Thing t, TraderKindDef kind)
		{
			float mult = kind.PriceTypeFor(t.def, TradeAction.PlayerBuys).PriceMultiplier();
			return Mathf.Max(1f, t.MarketValue * mult);
		}

		public static bool GuestBuy(Pawn buyer, StallVisit v, out Thing bought, out int price)
		{
			bought = null;
			price = 0;
			if (buyer == null || !MerchantAtPost(v)) return false;
			Pawn trader = v.trader;
			TraderKindDef kind = trader.TraderKind;
			if (kind == null || trader.trader == null) return false;
			int silver = SilverOf(buyer);
			if (silver < MinGuestPurchase) return false;
			var affordable = new List<Thing>();
			foreach (Thing t in trader.trader.Goods)
			{
				if (t == null || t is Pawn || t is MinifiedThing || t.def == ThingDefOf.Silver) continue;
				if (t.def.category != ThingCategory.Item || !Tradeable(t) || t.holdingOwner == null) continue;
				if (UnitPrice(t, kind) > silver) continue;
				affordable.Add(t);
			}
			if (affordable.Count == 0) return false;
			Thing pick = affordable.RandomElement();
			float unit = UnitPrice(pick, kind);
			int max = Mathf.Min(pick.stackCount, Mathf.FloorToInt(silver / unit), MaxUnitsPerPurchase);
			if (max < 1) return false;
			int count = Rand.RangeInclusive(1, max);
			int cost = Mathf.Max(1, Mathf.CeilToInt(unit * count));
			ThingOwner owner = pick.holdingOwner;
			Thing split = owner.Take(pick, count);
			if (split == null) return false;
			price = TransferSilver(buyer, owner, cost);
			buyer.inventory.TryAddItemNotForSale(split);
			bought = split;
			return true;
		}

		public static int TransferSilver(Pawn from, ThingOwner to, int amount)
		{
			if (HotelsBridge.IsGuest(from))
			{
				if (to == null) return 0;
				int paid = HotelsBridge.Spend(from, amount);
				if (paid > 0) to.TryAdd(MakeSilver(paid));
				return paid;
			}
			ThingOwner inv = from.inventory?.innerContainer;
			if (inv == null || to == null) return 0;
			int before = SilverOf(from);
			for (int i = inv.Count - 1; i >= 0; i--)
			{
				int left = amount - (before - SilverOf(from));
				if (left <= 0) break;
				if (i >= inv.Count) continue;
				Thing t = inv[i];
				if (t.def != ThingDefOf.Silver) continue;
				inv.TryTransferToContainer(t, to, Mathf.Min(t.stackCount, left));
			}
			return Mathf.Max(0, before - SilverOf(from));
		}

		public static float PayCapacity(List<Pawn> pawns, out int silver)
		{
			silver = 0;
			float goods = 0f;
			for (int i = 0; i < pawns.Count; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				for (int j = 0; j < inv.Count; j++)
				{
					Thing t = inv[j];
					if (t.def == ThingDefOf.Silver) { silver += t.stackCount; continue; }
					if (Tradeable(t)) goods += t.MarketValue * t.stackCount;
				}
			}
			return silver + goods;
		}

		public static List<Thing> TakePayment(List<Pawn> pawns, int amount, bool takeGoods)
		{
			var taken = new List<Thing>();
			float got = 0f;
			for (int i = 0; i < pawns.Count && got < amount; i++)
			{
				if (HotelsBridge.IsGuest(pawns[i]))
				{
					int paid = HotelsBridge.Spend(pawns[i], Mathf.CeilToInt(amount - got));
					if (paid > 0)
					{
						taken.Add(MakeSilver(paid));
						got += paid;
					}
					continue;
				}
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				List<Thing> carried = inv.ToList();
				for (int j = 0; j < carried.Count && got < amount; j++)
				{
					Thing t = carried[j];
					if (t.def != ThingDefOf.Silver) continue;
					int take = Mathf.Min(t.stackCount, Mathf.CeilToInt(amount - got));
					Thing split = inv.Take(t, take);
					if (split == null) continue;
					taken.Add(split);
					got += take;
				}
			}
			if (!takeGoods || got >= amount) return taken;
			var goods = new List<Thing>();
			for (int i = 0; i < pawns.Count; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				for (int j = 0; j < inv.Count; j++)
				{
					Thing t = inv[j];
					if (t.def != ThingDefOf.Silver && Tradeable(t)) goods.Add(t);
				}
			}
			goods.SortByDescending(t => t.MarketValue);
			for (int i = 0; i < goods.Count; i++)
			{
				float left = amount - got;
				if (left < 1f) break;
				Thing t = goods[i];
				float unit = t.MarketValue;
				if (unit <= 0f || unit > left) continue;
				ThingOwner owner = t.holdingOwner;
				if (owner == null) continue;
				int take = Mathf.Min(t.stackCount, Mathf.FloorToInt(left / unit));
				if (take <= 0) continue;
				Thing split = owner.Take(t, take);
				if (split == null) continue;
				taken.Add(split);
				got += split.MarketValue * split.stackCount;
			}
			return taken;
		}

		private static Thing MakeSilver(int count)
		{
			Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
			silver.stackCount = count;
			return silver;
		}

		public static int TakeAndPlacePayment(List<Pawn> pawns, int amount, bool takeGoods, IntVec3 cell, Map map)
		{
			List<Thing> payment = TakePayment(pawns, amount, takeGoods);
			int value = 0;
			for (int i = 0; i < payment.Count; i++)
			{
				Thing t = payment[i];
				value += Mathf.RoundToInt(t.MarketValue * t.stackCount);
				GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
			}
			return value;
		}

		public static void CollectShare(StallVisit v)
		{
			int amount = Mathf.RoundToInt(v.shareOwed);
			v.shareOwed = 0f;
			if (amount <= 0 || v.map == null || !v.StallOk) return;
			int value = TakeAndPlacePayment(v.lord.ownedPawns, amount, RimMarketMod.Settings.feeInGoods, v.stall.CustomerCell, v.map);
			if (value <= 0) return;
			v.feeCollected += value;
			MarketManager.Instance?.AddFees(value);
			RimMarketDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(v.stall.CustomerCell, v.map));
			MoteMaker.ThrowText(v.stall.DrawPos, v.map, "+" + value, 3.9f);
			if (RimMarketMod.Settings.showMessages)
				Messages.Message("RimMarket.MessageSharePaid".Translate(v.faction?.Name ?? "?", value),
					new LookTargets(v.stall.parent), MessageTypeDefOf.PositiveEvent, historical: false);
		}

		public static AcceptanceReport CanNegotiate(Pawn colonist, Pawn trader, CompMarketStall stall)
		{
			if (colonist == null || colonist.Dead || colonist.Downed || colonist.InMentalState) return false;
			if (colonist.skills == null || colonist.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
				return "CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap).ToString();
			if (trader == null || !trader.CanTradeNow) return "RimMarket.TraderBusy".Translate().ToString();
			if (trader.mindState.traderDismissed) return "TraderDismissed".Translate().ToString();
			AcceptanceReport tradeWith = colonist.CanTradeWith(trader.Faction, trader.TraderKind);
			if (!tradeWith.Accepted) return tradeWith;
			if (stall == null || !colonist.CanReach(stall.CustomerCell, PathEndMode.OnCell, Danger.Deadly)) return "NoPath".Translate().ToString();
			return true;
		}

		public static void StartTradeJob(Pawn colonist, CompMarketStall stall, Pawn trader)
		{
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_TradeAtStall, stall.parent, trader);
			job.playerForced = true;
			colonist.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.InteractingWithTraders, KnowledgeAmount.Total);
		}

		public static void OpenTradeMenu(CompMarketStall stall)
		{
			StallVisit v = MarketManager.Instance?.VisitFor(stall);
			if (v == null || v.trader == null) return;
			Pawn trader = v.trader;
			var options = new List<FloatMenuOption>();
			List<Pawn> colonists = stall.Map.mapPawns.FreeColonistsSpawned;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn p = colonists[i];
				AcceptanceReport report = CanNegotiate(p, trader, stall);
				string label = p.LabelShortCap + " (" + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) + ")";
				if (!report.Accepted)
				{
					options.Add(new FloatMenuOption(label + ": " + report.Reason, null));
					continue;
				}
				Pawn negotiator = p;
				options.Add(new FloatMenuOption(label, () => StartTradeJob(negotiator, stall, trader)));
			}
			if (options.Count == 0)
			{
				Messages.Message("RimMarket.MessageNoNegotiator".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			options.SortBy(o => o.Label);
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
