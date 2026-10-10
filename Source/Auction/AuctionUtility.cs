using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Auction
{
	public static class AuctionUtility
	{
		public const int AnnounceTicks = 5000;
		public const int LotTicks = 5000;
		public const int BetweenLotsTicks = 1250;
		public const int SoftCloseTicks = 750;

		private const float MinBidderFactor = 0.7f;
		private const float MaxBidderFactor = 1.4f;
		private const float MinGuestFactor = 0.8f;
		private const float MaxGuestFactor = 1.3f;
		private const float VisitorChance = 0.5f;

		private static readonly FieldInfo SoldPrisonersField = AccessTools.Field(typeof(Pawn_TraderTracker), "soldPrisoners");

		private static readonly string[] PreceptNames = { "Slavery_Honorable", "Slavery_Disapproved", "Slavery_Horrible", "Slavery_Abhorrent" };

		public static bool ForSale(Pawn p, StallVisit v)
		{
			if (p == null || p.Dead || p.Downed || !p.Spawned || !p.RaceProps.Humanlike) return false;
			if (p.GetTraderCaravanRole() != TraderCaravanRole.Chattel) return false;
			if (p.GetLord() != v.lord) return false;
			if (SoldPrisonersField?.GetValue(v.trader?.trader) is List<Pawn> sold && sold.Contains(p)) return false;
			return true;
		}

		public static Pawn NextLot(StallVisit v, SlaveAuction a)
		{
			if (v == null || !v.LordAlive) return null;
			List<Pawn> pawns = v.lord.ownedPawns;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (a != null && a.done.Contains(p)) continue;
				if (ForSale(p, v)) return p;
			}
			return null;
		}

		public static int CountLots(StallVisit v)
		{
			if (v == null || !v.LordAlive) return 0;
			int n = 0;
			for (int i = 0; i < v.lord.ownedPawns.Count; i++)
			{
				if (ForSale(v.lord.ownedPawns[i], v)) n++;
			}
			return n;
		}

		public static Building_AuctionBlock FindBlock(StallVisit v, IEnumerable<SlaveAuction> running)
		{
			if (v == null || !v.StallOk) return null;
			Building_MarketMarker marker = MarketUtility.MarkerFor(v.stall);
			if (marker == null || !marker.Open) return null;
			List<Thing> blocks = v.map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_AuctionBlock);
			for (int i = 0; i < blocks.Count; i++)
			{
				if (!(blocks[i] is Building_AuctionBlock b) || !b.Spawned || b.Faction != Faction.OfPlayer) continue;
				if (MarketUtility.MarkerAt(v.map, b.Position) != marker) continue;
				if (running.Any(r => r.block == b)) continue;
				return b;
			}
			return null;
		}

		public static int StartPrice(Pawn p)
		{
			return Mathf.Max(50, Mathf.RoundToInt(p.MarketValue * 0.5f / 10f) * 10);
		}

		public static int Increment(int price)
		{
			return Mathf.Max(10, Mathf.RoundToInt(price * 0.1f / 5f) * 5);
		}

		public static int NextBid(SlaveAuction a)
		{
			return a.leader == SlaveAuction.LeaderNone ? a.price : a.price + Increment(a.price);
		}

		public static bool IsSlaver(StallVisit v)
		{
			TraderKindDef kind = v?.trader?.TraderKind;
			return kind?.stockGenerators != null && kind.stockGenerators.Any(g => g is StockGenerator_Slaves);
		}

		public static bool FactionShunsSlavery(Faction f)
		{
			if (!ModsConfig.IdeologyActive || f?.ideos == null) return false;
			return f.ideos.AllIdeos.Any(i => !i.IdeoApprovesOfSlavery());
		}

		public static bool PlayerHasBlock(Map map)
		{
			if (map == null) return false;
			List<Thing> blocks = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_AuctionBlock);
			for (int i = 0; i < blocks.Count; i++)
			{
				if (blocks[i].Faction == Faction.OfPlayer) return true;
			}
			return false;
		}

		public static bool FactionBuysSlaves(Faction f)
		{
			if (f == null || f.IsPlayer || f.defeated || f.Hidden || f.HostileTo(Faction.OfPlayer) || !f.def.humanlikeFaction) return false;
			if (!ModsConfig.IdeologyActive || f.ideos?.PrimaryIdeo == null) return true;
			return f.ideos.PrimaryIdeo.IdeoApprovesOfSlavery();
		}

		public static float LotsValue(StallVisit v)
		{
			if (v == null || !v.LordAlive) return 0f;
			float total = 0f;
			int n = 0;
			for (int i = 0; i < v.lord.ownedPawns.Count; i++)
			{
				Pawn p = v.lord.ownedPawns[i];
				if (!ForSale(p, v)) continue;
				total += p.MarketValue;
				n++;
			}
			return n == 0 ? 0f : total / n;
		}

		public static List<AuctionBidder> MakeBidders(Map map, Pawn slave, Building_AuctionBlock block)
		{
			var list = new List<AuctionBidder>();
			float value = slave.MarketValue;
			int start = StartPrice(slave);
			foreach (Lord lord in AuctionBuyers.LordsFor(block))
			{
				Pawn buyer = AuctionBuyers.BuyerOf(lord);
				if (buyer == null || !buyer.Spawned || buyer.Downed) continue;
				int silver = MarketUtility.SilverOf(buyer);
				if (silver < start) continue;
				list.Add(new AuctionBidder
				{
					name = "RimMarket.AuctionBuyerNamed".Translate(buyer.LabelShortCap, lord.faction?.Name ?? "?"),
					pawn = buyer,
					max = Mathf.Min(silver, Mathf.RoundToInt(value * Rand.Range(MinBidderFactor, MaxBidderFactor))),
				});
			}
			if (HospitalityBridge.Active && RimMarketMod.Settings.guestsShop)
			{
				IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
				for (int i = 0; i < pawns.Count; i++)
				{
					Pawn p = pawns[i];
					if (!HospitalityBridge.IsArrivedGuest(p) || !FactionBuysSlaves(p.Faction)) continue;
					int silver = MarketUtility.SilverOf(p);
					if (silver < start) continue;
					list.Add(new AuctionBidder
					{
						name = p.LabelShortCap,
						pawn = p,
						max = Mathf.Min(silver, Mathf.RoundToInt(value * Rand.Range(MinGuestFactor, MaxGuestFactor))),
					});
				}
			}
			return list;
		}

		public static bool BidderStillIn(AuctionBidder b)
		{
			if (b.pawn == null) return true;
			return b.pawn.Spawned && !b.pawn.Dead && !b.pawn.Downed;
		}

		public static IEnumerable<Thing> ColonySilverThings(Map map)
		{
			List<Thing> silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
			for (int i = 0; i < silver.Count; i++)
			{
				Thing t = silver[i];
				if (!t.Spawned || t.Position.Fogged(map)) continue;
				if (!map.areaManager.Home[t.Position] && !t.IsInAnyStorage()) continue;
				yield return t;
			}
		}

		public static int ColonySilver(Map map)
		{
			if (map == null) return 0;
			int n = 0;
			foreach (Thing t in ColonySilverThings(map)) n += t.stackCount;
			return n;
		}

		public static bool TakeColonySilver(Map map, int amount)
		{
			if (ColonySilver(map) < amount) return false;
			List<Thing> stacks = ColonySilverThings(map).ToList();
			for (int i = 0; i < stacks.Count && amount > 0; i++)
			{
				int take = Mathf.Min(amount, stacks[i].stackCount);
				stacks[i].SplitOff(take).Destroy();
				amount -= take;
			}
			return true;
		}

		public static Pawn BestNegotiator(Map map)
		{
			Pawn best = null;
			float bestSkill = -1f;
			List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn p = colonists[i];
				if (p.Downed || p.skills == null) continue;
				float s = p.skills.GetSkill(SkillDefOf.Social).Level;
				if (s > bestSkill)
				{
					bestSkill = s;
					best = p;
				}
			}
			return best;
		}

		public static void GiveMoods(Map map)
		{
			if (!ModsConfig.IdeologyActive || map == null) return;
			var precepts = new PreceptDef[PreceptNames.Length];
			for (int i = 0; i < PreceptNames.Length; i++) precepts[i] = DefDatabase<PreceptDef>.GetNamedSilentFail(PreceptNames[i]);
			List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn p = colonists[i];
				Ideo ideo = p.Ideo;
				if (ideo == null || p.needs?.mood?.thoughts?.memories == null) continue;
				for (int stage = 0; stage < precepts.Length; stage++)
				{
					if (precepts[stage] == null || !ideo.HasPrecept(precepts[stage])) continue;
					p.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(RimMarketDefOf.RimMarket_SlaveAuctionHeld, stage));
					break;
				}
			}
		}

		public static void TryAttractBuyers(Map map)
		{
			if (!HospitalityBridge.Active || !Rand.Chance(VisitorChance)) return;
			Faction faction = ReputationUtility.ArrivalFactions(PawnGroupKindDefOf.Peaceful).Where(FactionBuysSlaves).RandomElementWithFallback();
			if (faction == null) return;
			IncidentDef def = IncidentDefOf.VisitorGroup;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = faction;
			def.Worker.TryExecute(parms);
		}
	}
}
