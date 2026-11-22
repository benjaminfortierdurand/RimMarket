using System.Collections.Generic;
using RimMarket.Inn;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public static class MarketBreaks
	{
		private const int MaxRemembered = 300;
		private const int LongestRest = 25000;
		private const int FirstMin = 1500;
		private const int FirstMax = 6000;
		private const int TicksPerCell = 14;

		private static readonly Dictionary<int, int> nextBreak = new Dictionary<int, int>();

		public static void Clear() => nextBreak.Clear();

		public static bool Due(Pawn pawn)
		{
			int now = Find.TickManager.TicksGame;
			if (!nextBreak.TryGetValue(pawn.thingIDNumber, out int at) || at - now > LongestRest)
			{
				Rest(pawn, FirstMin, FirstMax);
				return false;
			}
			return now >= at;
		}

		public static void Rest(Pawn pawn, int min, int max)
		{
			if (nextBreak.Count >= MaxRemembered) nextBreak.Clear();
			nextBreak[pawn.thingIDNumber] = Find.TickManager.TicksGame + Rand.Range(min, max);
		}

		public static bool OnBreak(Pawn pawn)
		{
			JobDef job = pawn?.CurJobDef;
			return job != null && (job == RimMarketDefOf.RimMarket_BrowseStall || job == RimMarketDefOf.RimMarket_EveningGather);
		}

		public static void CallBack(Pawn trader)
		{
			if (trader == null || !trader.Spawned || trader.jobs == null || !OnBreak(trader)) return;
			trader.jobs.EndCurrentJob(JobCondition.InterruptForced);
		}

		public static bool CaravanFolk(Pawn pawn)
		{
			return MarketManager.AnyVisits && MarketManager.Instance.VisitFor(pawn.GetLord()) != null;
		}

		public static bool StallBusy(StallVisit v, Map map)
		{
			Thing stall = v.stall.parent;
			IReadOnlyList<Pawn> pawns = map.mapPawns.AllHumanlikeSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == v.trader) continue;
				Job job = p.CurJob;
				if (job == null) continue;
				Thing target = job.targetA.Thing;
				if (target != null && (target == stall || target == v.trader)) return true;
			}
			return false;
		}

		public static bool OthersAway(StallVisit v)
		{
			IReadOnlyList<StallVisit> visits = MarketManager.Instance.Visits;
			for (int i = 0; i < visits.Count; i++)
			{
				StallVisit o = visits[i];
				if (o != v && o.map == v.map && o.trader != null && o.trader.CurJobDef == RimMarketDefOf.RimMarket_BrowseStall) return true;
			}
			return false;
		}

		public static Job BrowseJob(Pawn pawn, StallVisit own, float maxWalk, int stay)
		{
			Job job = MarketUtility.MakeVisitJob(pawn, browseOnly: true, exclude: own.stall, maxDist: maxWalk);
			if (job == null) return null;
			job.expiryInterval = (int)(pawn.Position.DistanceTo(job.targetB.Cell) * TicksPerCell) + stay;
			return job;
		}
	}

	public class JobGiver_BrowseOnBreak : ThinkNode_JobGiver
	{
		private const int MinRest = 5000;
		private const int MaxRest = 10000;
		private const float MaxWalk = 45f;
		private const int MinStay = 600;
		private const int MaxStay = 1100;

		protected override Job TryGiveJob(Pawn pawn)
		{
			Map map = pawn.Map;
			if (map == null || !pawn.RaceProps.Humanlike || !MarketManager.AnyVisits || InnUtility.Night(map)) return null;
			if (!MarketBreaks.Due(pawn)) return null;
			StallVisit v = MarketManager.Instance.VisitFor(pawn.GetLord());
			if (v == null || v.closed || !v.StallOk || pawn == v.trader) return null;
			if (pawn.GetTraderCaravanRole() == TraderCaravanRole.Chattel) return null;
			MarketBreaks.Rest(pawn, MinRest, MaxRest);
			return MarketBreaks.BrowseJob(pawn, v, MaxWalk, Rand.Range(MinStay, MaxStay));
		}
	}

	public class JobGiver_MerchantBreak : ThinkNode_JobGiver
	{
		private const int MinRest = 12000;
		private const int MaxRest = 20000;
		private const int RetryMin = 600;
		private const int RetryMax = 1200;
		private const float MaxWalk = 25f;
		private const float PostRange = 2f;
		private const int MinStay = 500;
		private const int MaxStay = 800;

		protected override Job TryGiveJob(Pawn pawn)
		{
			Map map = pawn.Map;
			if (map == null || !MarketManager.AnyVisits || InnUtility.Night(map)) return null;
			if (!MarketBreaks.Due(pawn)) return null;
			StallVisit v = MarketManager.Instance.VisitFor(pawn.GetLord());
			if (v == null || v.closed || v.trader != pawn || !v.StallOk || !v.arrivalSignalled || v.feeDue) return null;
			if (!pawn.Position.InHorDistOf(v.stall.MerchantCell, PostRange)) return null;
			if (MarketBreaks.StallBusy(v, map) || MarketBreaks.OthersAway(v))
			{
				MarketBreaks.Rest(pawn, RetryMin, RetryMax);
				return null;
			}
			MarketBreaks.Rest(pawn, MinRest, MaxRest);
			return MarketBreaks.BrowseJob(pawn, v, MaxWalk, Rand.Range(MinStay, MaxStay));
		}
	}
}
