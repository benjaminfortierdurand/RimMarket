using System.Collections.Generic;
using RimMarket.Auction;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class LordToil_MarketStall : LordToil
	{
		private const int CheckInterval = 250;
		private const int FeedInterval = 2500;
		private const int ArrivalFallbackTicks = 5000;
		private const string ArrivedMemo = "TravelArrived";

		private static readonly List<Pawn> nudged = new List<Pawn>();

		private StallVisit Visit => MarketManager.Instance?.VisitFor(lord);

		public override bool AllowSatisfyLongNeeds => true;

		public override float? CustomWakeThreshold => 0.5f;

		public override IntVec3 FlagLoc
		{
			get
			{
				StallVisit v = Visit;
				if (v != null && v.StallOk) return v.stall.Position;
				return lord.ownedPawns.Count > 0 ? lord.ownedPawns[0].Position : IntVec3.Invalid;
			}
		}

		public override void Init()
		{
			base.Init();
			MarketManager.Instance?.TryBeginVisit(lord);
		}

		public override void UpdateAllDuties()
		{
			StallVisit v = Visit;
			if (v == null || !v.StallOk)
			{
				IdleDuties();
				return;
			}
			v.dutiesDirty = false;
			CompMarketStall stall = v.stall;
			Pawn trader = v.trader;
			Thing camp = CaravanCamp.CampBuilding(stall.Map, trader);
			Thing rest = CaravanCamp.RestSpot(stall.Map, trader);
			IntVec3 spot = camp == null || rest == null ? CaravanCamp.WaitSpot(stall, lord, trader) : IntVec3.Invalid;
			List<IntVec3> posts = CaravanCamp.PostCells(stall);
			GuardWatch.Roster(v, lord, System.Math.Min(RimMarketMod.Settings.guardsAtStall, posts.Count));
			IntVec3 lookOut = stall.CustomerCell + stall.Front * 4;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null) continue;
				switch (p.GetTraderCaravanRole())
				{
					case TraderCaravanRole.Trader:
						p.mindState.duty = TraderDuty(v);
						break;
					case TraderCaravanRole.Carrier:
					case TraderCaravanRole.Chattel:
						p.mindState.duty = p.RaceProps.Humanlike ? CaravanCamp.GuardDuty(rest, camp, spot) : CaravanCamp.Duty(camp, spot);
						break;
					default:
						int slot = GuardWatch.SlotOf(v, p);
						if (slot >= 0 && slot < posts.Count)
						{
							p.mindState.duty = new PawnDuty(RimMarketDefOf.RimMarket_GuardPost, posts[slot], lookOut)
							{
								locomotion = LocomotionUrgency.Walk,
							};
						}
						else
						{
							p.mindState.duty = CaravanCamp.GuardDuty(rest, camp, spot);
						}
						break;
				}
			}
		}

		private static PawnDuty TraderDuty(StallVisit v)
		{
			if (AuctionUtility.AuctioneerSpot(v, out IntVec3 cell, out Thing block))
				return new PawnDuty(RimMarketDefOf.RimMarket_CallAuctionDuty, cell, block) { locomotion = LocomotionUrgency.Walk };
			return new PawnDuty(RimMarketDefOf.RimMarket_TendStall, v.stall.MerchantCell, v.stall.MerchantLook) { locomotion = LocomotionUrgency.Walk };
		}

		private bool DutiesDrifted(StallVisit v)
		{
			if (!v.StallOk) return false;
			Thing camp = null, rest = null;
			IntVec3 chill = IntVec3.Invalid;
			CompMarketQuarter market = null;
			bool looked = false;
			List<IntVec3> posts = null;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null || !p.Spawned) continue;
				PawnDuty d = p.mindState.duty;
				if (d == null) return true;
				if (d.def == RimMarketDefOf.RimMarket_TendStall || d.def == RimMarketDefOf.RimMarket_CallAuctionDuty)
				{
					PawnDuty want = TraderDuty(v);
					if (d.def != want.def || d.focus.Cell != want.focus.Cell || d.focusSecond.Cell != want.focusSecond.Cell) return true;
					continue;
				}
				bool watch = GuardWatch.OnWatch(v, p);
				if (d.def == RimMarketDefOf.RimMarket_GuardPost)
				{
					if (!watch) return true;
					if (posts == null) posts = CaravanCamp.PostCells(v.stall);
					int slot = GuardWatch.SlotOf(v, p);
					if (slot < 0 || slot >= posts.Count || d.focus.Cell != posts[slot]) return true;
					continue;
				}
				if (watch && !p.Downed) return true;
				if (d.def == DutyDefOf.Escort || d.def == RimMarketDefOf.RimMarket_ParkAtStall || d.def == RimMarketDefOf.RimMarket_GuardStall) return true;
				if (d.def != RimMarketDefOf.RimMarket_WaitAtCamp && d.def != RimMarketDefOf.RimMarket_WaitNearCamp && d.def != RimMarketDefOf.RimMarket_RestAtSpotDuty) continue;
				if (!looked)
				{
					camp = CaravanCamp.CampBuilding(v.stall.Map, v.trader);
					rest = CaravanCamp.RestSpot(v.stall.Map, v.trader);
					chill = CaravanCamp.ChillSpot(lord);
					market = MarketUtility.MarkerFor(v.stall)?.Quarter;
					looked = true;
				}
				if (CaravanCamp.GuardDutyStale(d, rest, camp)) return true;
				if (d.def == RimMarketDefOf.RimMarket_WaitAtCamp)
				{
					if (!p.RaceProps.Animal && (rest != null || camp != null)) return true;
					if (CaravanCamp.DutyStale(d, p.RaceProps.Animal ? camp : null, market, chill)) return true;
				}
			}
			return false;
		}

		private void IdleDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null) continue;
				p.mindState.duty = new PawnDuty(DutyDefOf.Defend, p.Position, 8f) { wanderRadius = 6f };
			}
		}

		public override void LordToilTick()
		{
			int now = Find.TickManager.TicksGame;
			if (now % CheckInterval != 0) return;
			StallVisit v = Visit;
			if (v == null) return;
			bool relieved = v.StallOk && GuardWatch.Tick(v, lord, nudged);
			if (relieved || v.dutiesDirty || DutiesDrifted(v)) UpdateAllDuties();
			GuardWatch.Nudge(nudged);
			if (now % FeedInterval == 0) MarketUtility.TopUpFood(lord, full: false);
			if (!v.arrivalSignalled && (MarketUtility.MerchantAtPost(v) || now - v.startTick > ArrivalFallbackTicks))
			{
				v.arrivalSignalled = true;
				lord.ReceiveMemo(ArrivedMemo);
			}
		}
	}
}
