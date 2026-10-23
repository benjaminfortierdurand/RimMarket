using System.Collections.Generic;
using RimMarket.Compat;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Away
{
	public static class AwayStallUtility
	{
		public const int MarketTicks = 60000;
		public const int HourTicks = 2500;
		public const int CooldownTicks = 180000;

		private const float FeeNeutral = 0.10f;
		private const float FeeAlly = 0.05f;
		private const float BaseCustomersPerHour = 1.2f;
		private const float AllyCustomerFactor = 1.3f;
		private const float EventChancePerHour = 0.06f;
		private const int EventHours = 4;
		private const float MinBudget = 40f;
		private const float MaxBudget = 160f;
		private const float RichPremium = 1.2f;
		private const float PickpocketCaughtChance = 0.6f;
		private const float PickpocketLootValue = 60f;

		private static RimMarketSettings S => RimMarketMod.Settings;

		public static bool HostileOrPlayer(Settlement s)
		{
			Faction f = s?.Faction;
			return f == null || f.IsPlayer || f.def.permanentEnemy || f.HostileTo(Faction.OfPlayer);
		}

		public static float FeeRate(AwayStall st) => st.ally ? FeeAlly : FeeNeutral;

		public static bool Sellable(Thing t, Settlement s)
		{
			if (t == null || t is Pawn || t.Destroyed || t.stackCount <= 0) return false;
			if (t.def == ThingDefOf.Silver || t.MarketValue <= 0f) return false;
			if (t.def.IsNutritionGivingIngestible || t.def.IsMedicine) return false;
			TraderKindDef kind = s?.TraderKind;
			return kind != null && kind.WillTrade(t.def);
		}

		public static List<Thing> SellableItems(Caravan caravan, Settlement s)
		{
			var list = new List<Thing>();
			if (caravan == null) return list;
			List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
			for (int i = 0; i < items.Count; i++)
			{
				if (Sellable(items[i], s)) list.Add(items[i]);
			}
			return list;
		}

		public static List<Thing> Stock(AwayStall st)
		{
			List<Thing> list = SellableItems(st.caravan, st.settlement);
			list.RemoveAll(t => st.keep.Contains(t.def));
			return list;
		}

		public static Pawn Seller(Caravan caravan, Settlement s)
		{
			if (caravan == null || s == null) return null;
			return BestCaravanPawnUtility.FindBestNegotiator(caravan, s.Faction, s.TraderKind);
		}

		public static FloatMenuAcceptanceReport CanHoldStall(Caravan caravan, Settlement s)
		{
			if (!S.awayStalls || caravan == null || s == null || !s.Spawned || s.HasMap) return false;
			if (HostileOrPlayer(s) || !s.CanTradeNow) return false;
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return false;
			if (mgr.AwayStallFor(caravan) != null)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.AwayAlreadyHolding".Translate());
			if (mgr.AwayStallAt(s) != null)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.AwayStallTaken".Translate());
			int wait = mgr.AwayCooldownLeft(s);
			if (wait > 0)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.AwayCooldown".Translate(wait.ToStringTicksToPeriod()));
			if (Seller(caravan, s) == null)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.AwayNoSeller".Translate());
			if (SellableItems(caravan, s).Count == 0)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.AwayNothingToSell".Translate());
			return true;
		}

		public static float PriceFactor(Pawn seller)
		{
			int social = seller?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			return 0.85f + 0.0125f * Mathf.Clamp(social, 0, 20);
		}

		private static float BudgetFactor(Faction f)
		{
			switch (f?.def.techLevel ?? TechLevel.Industrial)
			{
				case TechLevel.Animal:
				case TechLevel.Neolithic: return 0.6f;
				case TechLevel.Medieval: return 0.8f;
				case TechLevel.Spacer: return 1.3f;
				case TechLevel.Ultra:
				case TechLevel.Archotech: return 1.5f;
				default: return 1f;
			}
		}

		public static float CustomersPerHour(AwayStall st)
		{
			float rate = BaseCustomersPerHour * (st.ally ? AllyCustomerFactor : 1f);
			MarketReputation opinion = MarketManager.Instance?.OpinionOf(st.Host);
			if (opinion != null && opinion.Established) rate *= Mathf.Clamp(1f + opinion.Total / 200f, 0.6f, 1.5f);
			if (st.EventActive) rate *= st.eventKind == AwayStall.EventFestival ? 2f : 0.5f;
			return rate;
		}

		public static void RunHour(AwayStall st)
		{
			List<Thing> stock = Stock(st);
			if (stock.Count == 0) return;
			Pawn seller = Seller(st.caravan, st.settlement);
			float factor = PriceFactor(seller);
			float rate = CustomersPerHour(st);
			int customers = Mathf.FloorToInt(rate) + (Rand.Chance(rate - Mathf.Floor(rate)) ? 1 : 0);
			for (int i = 0; i < customers && stock.Count > 0; i++)
			{
				SellToCustomer(st, stock, factor);
				stock.RemoveAll(t => t == null || t.Destroyed || t.stackCount <= 0 || t.ParentHolder == null);
			}
			if (!st.EventActive && Rand.Chance(EventChancePerHour)) RollEvent(st, stock, factor);
			PaySilver(st);
		}

		private static void SellToCustomer(AwayStall st, List<Thing> stock, float factor)
		{
			if (!stock.TryRandomElementByWeight(t => Mathf.Sqrt(t.MarketValue * t.stackCount), out Thing item)) return;
			float budget = Rand.Range(MinBudget, MaxBudget) * BudgetFactor(st.Host);
			float unitPrice = item.MarketValue * factor * HegemonyBridge.ScarcityFactor(st.settlement, item.def);
			int count;
			if (unitPrice <= budget) count = Mathf.Min(item.stackCount, Mathf.Max(1, Mathf.FloorToInt(budget / unitPrice)));
			else if (Rand.Chance(budget / unitPrice)) count = 1;
			else return;
			Sell(st, item, count, unitPrice);
		}

		private static void Sell(AwayStall st, Thing item, int count, float unitPrice)
		{
			Thing sold = item.SplitOff(count);
			VteBridge.Notify_PlayerSold(sold, count);
			if (!HegemonyBridge.Credit(st.settlement, sold) && !sold.Destroyed) sold.Destroy();
			float value = unitPrice * count;
			HegemonyBridge.RecordTrade(st.Host, value);
			float fee = value * FeeRate(st);
			st.gross += value;
			st.fees += fee;
			st.pendingSilver += value - fee;
			st.itemsSold += count;
		}

		public static void PaySilver(AwayStall st)
		{
			int whole = Mathf.FloorToInt(st.pendingSilver);
			if (whole <= 0 || st.caravan == null || !st.caravan.Spawned) return;
			st.pendingSilver -= whole;
			while (whole > 0)
			{
				Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
				silver.stackCount = Mathf.Min(whole, silver.def.stackLimit);
				whole -= silver.stackCount;
				CaravanInventoryUtility.GiveThing(st.caravan, silver);
			}
		}

		private static void RollEvent(AwayStall st, List<Thing> stock, float factor)
		{
			float roll = Rand.Value;
			if (roll < 0.35f) Pickpocket(st, stock);
			else if (roll < 0.6f) RichCustomer(st, stock, factor);
			else if (roll < 0.8f) StartEvent(st, AwayStall.EventRival, "RimMarket.AwayEventRival");
			else StartEvent(st, AwayStall.EventFestival, "RimMarket.AwayEventFestival");
		}

		private static void StartEvent(AwayStall st, int kind, string key)
		{
			st.eventKind = kind;
			st.eventUntil = Find.TickManager.TicksGame + EventHours * HourTicks;
			Say(key.Translate(st.settlement.LabelCap), st, MessageTypeDefOf.NeutralEvent);
		}

		private static void RichCustomer(AwayStall st, List<Thing> stock, float factor)
		{
			Thing best = null;
			for (int i = 0; i < stock.Count; i++)
			{
				if (best == null || stock[i].MarketValue > best.MarketValue) best = stock[i];
			}
			if (best == null) return;
			string label = best.LabelNoCount;
			float price = best.MarketValue * factor * RichPremium * HegemonyBridge.ScarcityFactor(st.settlement, best.def);
			Sell(st, best, 1, price);
			Say("RimMarket.AwayEventRich".Translate(label, Mathf.RoundToInt(price)), st, MessageTypeDefOf.PositiveEvent);
		}

		private static void Pickpocket(AwayStall st, List<Thing> stock)
		{
			Pawn guard = Guard(st.caravan);
			if (guard != null && Rand.Chance(PickpocketCaughtChance))
			{
				Say("RimMarket.AwayEventPickpocketCaught".Translate(guard.LabelShortCap), st, MessageTypeDefOf.NeutralEvent);
				return;
			}
			Thing cheapest = null;
			for (int i = 0; i < stock.Count; i++)
			{
				if (cheapest == null || stock[i].MarketValue < cheapest.MarketValue) cheapest = stock[i];
			}
			if (cheapest == null) return;
			int count = Mathf.Clamp(Mathf.FloorToInt(PickpocketLootValue / Mathf.Max(1f, cheapest.MarketValue)), 1, cheapest.stackCount);
			string label = cheapest.LabelNoCount;
			Thing stolen = cheapest.SplitOff(count);
			if (!stolen.Destroyed) stolen.Destroy();
			Say("RimMarket.AwayEventPickpocket".Translate(count, label), st, MessageTypeDefOf.NegativeEvent);
		}

		private static Pawn Guard(Caravan caravan)
		{
			if (caravan == null) return null;
			List<Pawn> pawns = caravan.PawnsListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p.RaceProps.Humanlike && p.IsColonist && !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent)) return p;
			}
			return null;
		}

		public static void Say(string text, AwayStall st, MessageTypeDef type)
		{
			if (!S.showMessages) return;
			LookTargets targets = st?.caravan != null && st.caravan.Spawned ? new LookTargets(st.caravan) : null;
			Messages.Message(text, targets, type, historical: false);
		}
	}
}
