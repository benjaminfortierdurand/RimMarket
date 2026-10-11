using System.Collections.Generic;
using RimMarket.Auction;
using RimMarket.Buildings;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Fair
{
	public static class FairUtility
	{
		public const int LeadDays = 3;
		public const int OpenHour = 6;
		public const int CloseHour = 22;
		public const int JudgeHour = 18;
		private const int PrizeGoodwill = 5;
		private const float PrizeAppeal = 2f;
		private const float ColonyPrizeAppeal = 3f;
		public const int MaxCaravans = 6;
		public const int MerchantCap = 8;
		public const int MaxShopperGroups = 5;
		public const int MinStalls = 3;
		public const int WaveTicks = 5000;
		public const int LastWaveMargin = 10000;
		public const int BuntingLinger = 2500;
		public const float ShopperPurse = 1.5f;
		public const float JoyFactor = 3f;
		public const float ThiefFactor = 3f;
		private const float AppealPerFaction = 1.5f;
		private const float SafetyPerFaction = 1f;
		private const float EmptyFairAppeal = -2f;

		private static readonly List<CompMarketStall> stalls = new List<CompMarketStall>();

		private static int Now => Find.TickManager.TicksGame;

		public static GreatFair On(Map map) => MarketManager.Instance?.FairOn(map);

		public static bool Running(Map map)
		{
			GreatFair f = On(map);
			return f != null && f.state == GreatFair.Running;
		}

		public static bool Festive(Map map)
		{
			GreatFair f = On(map);
			if (f == null) return false;
			if (f.state == GreatFair.Running) return true;
			return f.state == GreatFair.Done && Now < f.endTick + BuntingLinger;
		}

		public static bool Modern(List<CompMarketStall> stalls)
		{
			int lit = 0;
			for (int i = 0; i < stalls.Count; i++)
			{
				if (stalls[i].Modern) lit++;
			}
			return stalls.Count > 0 && lit * 2 >= stalls.Count;
		}

		public static Building_MarketMarker MastOn(Map map)
		{
			if (!Festive(map)) return null;
			Building_MarketMarker marker = On(map)?.marker;
			return marker != null && marker.Spawned && marker.Map == map ? marker : null;
		}

		public static int StartTickFor(Map map)
		{
			int sinceMidnight = Mathf.FloorToInt(GenLocalDate.DayPercent(map) * GenDate.TicksPerDay);
			return Now - sinceMidnight + LeadDays * GenDate.TicksPerDay + OpenHour * GenDate.TicksPerHour;
		}

		public static string DateOf(int tick, Map map)
		{
			long abs = Find.TickManager.TicksAbs + (tick - Now);
			return GenDate.DateFullStringAt(abs, Find.WorldGrid.LongLatOf(map.Tile));
		}

		public static AcceptanceReport CanAnnounce(Building_MarketMarker marker)
		{
			MarketManager mgr = MarketManager.Instance;
			RimMarketSettings s = RimMarketMod.Settings;
			if (mgr == null || marker == null || !marker.Spawned) return false;
			if (mgr.AnyFair) return "RimMarket.FairAlreadyPlanned".Translate();
			int ready = mgr.LastFairTick + s.fairCooldownDays * GenDate.TicksPerDay;
			if (mgr.LastFairTick > 0 && Now < ready) return "RimMarket.FairTooSoon".Translate((ready - Now).ToStringTicksToPeriod());
			if (MarketUtility.CountStalls(marker, out _, out _) < MinStalls) return "RimMarket.FairNeedStalls".Translate(MinStalls);
			if (s.fairCost > 0 && AuctionUtility.ColonySilver(marker.Map) < s.fairCost) return "RimMarket.FairNoSilver".Translate(s.fairCost);
			return true;
		}

		public static void Announce(Building_MarketMarker marker)
		{
			MarketManager mgr = MarketManager.Instance;
			RimMarketSettings s = RimMarketMod.Settings;
			if (mgr == null || !CanAnnounce(marker).Accepted) return;
			Map map = marker.Map;
			if (s.fairCost > 0 && !AuctionUtility.TakeColonySilver(map, s.fairCost)) return;
			var fair = new GreatFair
			{
				map = map,
				marker = marker,
				state = GreatFair.Announced,
				announcedTick = Now,
				startTick = StartTickFor(map),
			};
			fair.endTick = fair.startTick + (CloseHour - OpenHour) * GenDate.TicksPerHour;
			mgr.AddFair(fair);
			Find.LetterStack.ReceiveLetter("RimMarket.FairAnnouncedLabel".Translate(),
				"RimMarket.FairAnnouncedText".Translate(DateOf(fair.startTick, map), OpenHour, CloseHour),
				LetterDefOf.PositiveEvent, new LookTargets(marker));
		}

		public static void Cancel(GreatFair fair, string key)
		{
			MarketManager.Instance?.RemoveFair(fair);
			if (!key.NullOrEmpty())
				Messages.Message(key.Translate(), new LookTargets(fair.marker), MessageTypeDefOf.NegativeEvent, historical: false);
		}

		public static void Tick(MarketManager mgr, GreatFair fair)
		{
			if (fair.map == null || !Find.Maps.Contains(fair.map))
			{
				mgr.RemoveFair(fair);
				return;
			}
			switch (fair.state)
			{
				case GreatFair.Announced:
					if (fair.marker == null || !fair.marker.Spawned)
					{
						Cancel(fair, "RimMarket.FairCancelledMarker");
						return;
					}
					if (Now >= fair.startTick) Open(mgr, fair);
					break;
				case GreatFair.Running:
					if (Now >= fair.endTick)
					{
						Close(mgr, fair);
						return;
					}
					SendCaravans(fair);
					SendWave(fair);
					if (!fair.judged && Now >= fair.startTick + (JudgeHour - OpenHour) * GenDate.TicksPerHour) Judge(mgr, fair);
					break;
				default:
					if (Now >= fair.endTick + BuntingLinger) mgr.RemoveFair(fair);
					break;
			}
		}

		public static void Open(MarketManager mgr, GreatFair fair)
		{
			fair.state = GreatFair.Running;
			fair.feesAtStart = mgr.TotalFees;
			fair.guestSalesAtStart = mgr.TotalGuestSales;
			fair.colonySalesAtStart = mgr.TotalColonySales;
			fair.caughtAtStart = mgr.ThievesCaught;
			fair.escapedAtStart = mgr.ThievesEscaped;
			if (fair.marker != null && fair.marker.Spawned && !fair.marker.Open) fair.marker.SetOpen(true);
			RecordBaselines(fair);
			MarketUtility.CountStalls(fair.marker, out int free, out _);
			fair.caravansDue = Mathf.Min(MaxCaravans, free);
			fair.nextCaravanTick = Now;
			fair.nextWaveTick = Now + WaveTicks;
			if (RimMarketMod.Settings.fairPictures)
				Window_GreatFair.ShowOpening(fair, "RimMarket.FairOpenStory".Translate(fair.caravansDue, JudgeHour, CloseHour));
			else
				Messages.Message("RimMarket.FairOpens".Translate(), new LookTargets(fair.marker), MessageTypeDefOf.PositiveEvent);
			MapComponent_MarketShow.On(fair.map)?.Opening(fair);
		}

		private static void SendCaravans(GreatFair fair)
		{
			if (fair.caravansSent >= fair.caravansDue || Now < fair.nextCaravanTick) return;
			Faction faction = ReputationUtility.TrySendFairCaravan(fair.map, fair.factions);
			if (faction == null)
			{
				fair.caravansDue = fair.caravansSent;
				return;
			}
			fair.factions.Add(faction);
			fair.caravansSent++;
			fair.nextCaravanTick = Now + Rand.RangeInclusive(900, 2000);
		}

		private static void SendWave(GreatFair fair)
		{
			if (Now < fair.nextWaveTick) return;
			fair.nextWaveTick = Now + WaveTicks;
			if (!RimMarketMod.Settings.shoppersVisit || Now > fair.endTick - LastWaveMargin) return;
			if (MarketShoppers.GroupsOn(fair.map) >= MaxShopperGroups) return;
			CompMarketStall target = TargetStall(fair);
			if (target == null) return;
			string from = MarketShoppers.Send(fair.map, target, null, ShopperPurse);
			if (from == null) return;
			fair.waves++;
			Messages.Message("RimMarket.FairCrowd".Translate(from), new LookTargets(target.parent), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static int MerchantTakings(StallVisit v) => Mathf.RoundToInt(v.silverEarned + v.colonyBought);

		private static void RecordBaselines(GreatFair fair)
		{
			fair.visitBaselines.Clear();
			fair.colonyBaselines.Clear();
			List<CompMarketStall> all = StallsOf(fair);
			for (int i = 0; i < all.Count; i++)
			{
				StallVisit v = all[i].Visit;
				if (v != null) fair.visitBaselines[v.id] = MerchantTakings(v);
				if (all[i].parent is Building_MarketStall b && b.ColonyStall) fair.colonyBaselines[b.thingIDNumber] = b.takings;
			}
		}

		public static void Judge(MarketManager mgr, GreatFair fair)
		{
			fair.judged = true;
			List<CompMarketStall> all = StallsOf(fair);
			CompMarketStall best = null;
			Faction bestFaction = null;
			string bestName = null;
			int bestScore = 0;
			for (int i = 0; i < all.Count; i++)
			{
				CompMarketStall stall = all[i];
				StallVisit v = stall.Visit;
				int score;
				Faction faction;
				string name;
				if (v != null)
				{
					fair.visitBaselines.TryGetValue(v.id, out int baseline);
					score = MerchantTakings(v) - baseline;
					faction = v.faction;
					name = v.trader?.LabelShortCap ?? v.faction?.Name ?? "?";
				}
				else if (stall.parent is Building_MarketStall b && b.ColonyStall)
				{
					fair.colonyBaselines.TryGetValue(b.thingIDNumber, out int baseline);
					score = b.takings - baseline;
					faction = Faction.OfPlayer;
					name = null;
				}
				else continue;
				if (score > bestScore)
				{
					bestScore = score;
					best = stall;
					bestFaction = faction;
					bestName = name;
				}
			}
			if (best == null) return;
			fair.winnerStall = best.parent;
			fair.winnerFaction = bestFaction;
			fair.winnerName = bestName;
			fair.winnerScore = bestScore;
			MoteMaker.ThrowText(best.parent.DrawPos + new Vector3(0f, 0f, 1.2f), fair.map, "RimMarket.FairPrizeMote".Translate(), new Color(1f, 0.85f, 0.35f), 8f);
			if (bestFaction == null || bestFaction.IsPlayer)
			{
				mgr.Reputation.Add(MarketReputation.Appeal, ColonyPrizeAppeal);
				List<Pawn> colonists = fair.map.mapPawns.FreeColonistsSpawned;
				for (int i = 0; i < colonists.Count; i++)
					colonists[i].needs?.mood?.thoughts?.memories?.TryGainMemory(RimMarketDefOf.RimMarket_FairStallWon);
				Messages.Message("RimMarket.FairPrizeColony".Translate(bestScore), new LookTargets(best.parent), MessageTypeDefOf.PositiveEvent);
				return;
			}
			mgr.Record(null, bestFaction, MarketReputation.Appeal, PrizeAppeal, null);
			if (!bestFaction.defeated && !bestFaction.HostileTo(Faction.OfPlayer))
				bestFaction.TryAffectGoodwillWith(Faction.OfPlayer, PrizeGoodwill, canSendMessage: false, canSendHostilityLetter: false,
					RimMarketDefOf.RimMarket_FairPrize);
			Messages.Message("RimMarket.FairPrizeMerchant".Translate(bestName, bestFaction.Name, bestScore, PrizeGoodwill),
				new LookTargets(best.parent), MessageTypeDefOf.PositiveEvent);
		}

		public static List<CompMarketStall> StallsOf(GreatFair fair)
		{
			var list = new List<CompMarketStall>();
			if (fair?.marker == null || !fair.marker.Spawned) return list;
			MarketUtility.CollectStalls(fair.map, stalls);
			for (int i = 0; i < stalls.Count; i++)
			{
				if (stalls[i].Spawned && MarketUtility.MarkerFor(stalls[i]) == fair.marker) list.Add(stalls[i]);
			}
			return list;
		}

		private static CompMarketStall TargetStall(GreatFair fair)
		{
			List<CompMarketStall> all = StallsOf(fair);
			if (all.Count == 0) return null;
			var busy = new List<CompMarketStall>();
			for (int i = 0; i < all.Count; i++)
			{
				if (all[i].Visit != null || ColonyStalls.IsColonyStall(all[i].parent)) busy.Add(all[i]);
			}
			return (busy.Count > 0 ? busy : all).RandomElement();
		}

		public static void Close(MarketManager mgr, GreatFair fair)
		{
			fair.state = GreatFair.Done;
			mgr.LastFairTick = fair.startTick;
			MapComponent_MarketShow.On(fair.map)?.Closing(fair);
			int fees = Mathf.Max(0, mgr.TotalFees - fair.feesAtStart);
			int colony = Mathf.Max(0, mgr.TotalColonySales - fair.colonySalesAtStart);
			int spent = Mathf.Max(0, mgr.TotalGuestSales - fair.guestSalesAtStart);
			int caught = Mathf.Max(0, mgr.ThievesCaught - fair.caughtAtStart);
			int escaped = Mathf.Max(0, mgr.ThievesEscaped - fair.escapedAtStart);
			for (int i = 0; i < fair.factions.Count; i++)
			{
				Faction f = fair.factions[i];
				mgr.Record(null, f, MarketReputation.Appeal, AppealPerFaction, null);
				if (escaped == 0) mgr.Record(null, f, MarketReputation.Safety, SafetyPerFaction, null);
			}
			if (fair.factions.Count == 0) mgr.Reputation.Add(MarketReputation.Appeal, EmptyFairAppeal);
			var names = new List<string>();
			for (int i = 0; i < fair.factions.Count; i++) names.Add(fair.factions[i].Name);
			string text = fair.factions.Count == 0
				? "RimMarket.FairOverEmpty".Translate().ToString()
				: "RimMarket.FairOverText".Translate(fair.factions.Count, names.ToCommaList(useAnd: true), fair.waves, fees, colony, spent, caught, escaped).ToString();
			if (fair.winnerStall != null && fair.winnerScore > 0)
			{
				text += "\n\n" + (fair.winnerFaction == null || fair.winnerFaction.IsPlayer
					? "RimMarket.FairOverWinnerColony".Translate(fair.winnerScore).ToString()
					: "RimMarket.FairOverWinner".Translate(fair.winnerName ?? "?", fair.winnerFaction.Name, fair.winnerScore).ToString());
			}
			LetterDef def = fair.factions.Count == 0 ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent;
			if (RimMarketMod.Settings.fairPictures)
				Window_GreatFair.ShowClosing(fair, "RimMarket.FairOverLabel".Translate(), text, def);
			else
				Find.LetterStack.ReceiveLetter("RimMarket.FairOverLabel".Translate(), text, def, new LookTargets(fair.marker));
		}

		public static string InspectLine(GreatFair fair)
		{
			if (fair == null) return null;
			switch (fair.state)
			{
				case GreatFair.Announced:
					return "RimMarket.FairInspectAnnounced".Translate(DateOf(fair.startTick, fair.map), OpenHour);
				case GreatFair.Running:
					return "RimMarket.FairInspectRunning".Translate(fair.caravansSent, fair.waves, CloseHour);
				default:
					return null;
			}
		}
	}
}
