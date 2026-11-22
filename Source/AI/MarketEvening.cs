using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Inn;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public static class MarketEvening
	{
		public const int StartHour = 20;
		private const float SeatRadius = 4.9f;
		private const int MinStay = 1500;
		private const int MaxStay = 2500;
		private const int TicksPerCell = 14;

		public static bool On(Map map)
		{
			if (map == null || !RimMarketMod.Settings.eveningGathering) return false;
			return GenLocalDate.HourOfDay(map) >= StartHour && !InnUtility.Night(map);
		}

		public static bool AnySpot(Map map)
		{
			if (map == null) return false;
			List<CompGatherSpot> spots = map.gatherSpotLister.activeSpots;
			for (int i = 0; i < spots.Count; i++)
			{
				Thing t = spots[i].parent;
				if (t != null && t.Spawned && t.Faction == Faction.OfPlayer && MarketUtility.MarkerAt(map, t.Position) != null) return true;
			}
			return false;
		}

		private static readonly List<Thing> candidates = new List<Thing>();

		private static void CollectSpots(Pawn pawn, Building_MarketMarker marker)
		{
			candidates.Clear();
			CompMarketQuarter quarter = marker?.Quarter;
			if (quarter == null || !marker.Spawned) return;
			List<CompGatherSpot> spots = pawn.Map.gatherSpotLister.activeSpots;
			for (int i = 0; i < spots.Count; i++)
			{
				Thing t = spots[i].parent;
				if (t == null || !t.Spawned || t.Faction != Faction.OfPlayer || !quarter.InQuarter(t.Position)) continue;
				CompRefuelable fuel = t.TryGetComp<CompRefuelable>();
				if (fuel != null && !fuel.HasFuel) continue;
				candidates.Add(t);
			}
			IntVec3 center = marker.Position;
			candidates.SortBy(t => t.Position.DistanceToSquared(center));
		}

		private static bool Sittable(Building b) => b != null && b.def.building != null && b.def.building.isSittable;

		private static bool Usable(IntVec3 c, Thing spot, Pawn pawn, bool seat)
		{
			Map map = pawn.Map;
			if (!c.InBounds(map)) return false;
			Building b = c.GetEdifice(map);
			if (seat)
			{
				if (b == spot || !Sittable(b)) return false;
			}
			else if (b != null || !c.Standable(map) || CaravanCamp.OnOtherStall(map, null, c)) return false;
			if (!GenSight.LineOfSight(c, spot.Position, map, skipFirstCell: true)) return false;
			return pawn.CanReserveSittableOrSpot(c) && pawn.CanReach(c, PathEndMode.OnCell, Danger.None);
		}

		public static bool TryFindSeat(Thing spot, Pawn pawn, out IntVec3 cell, out Building chair)
		{
			chair = null;
			IntVec3 here = pawn.Position;
			bool near = here.InHorDistOf(spot.Position, SeatRadius);
			cell = here;
			if (near && Usable(here, spot, pawn, true))
			{
				chair = here.GetEdifice(pawn.Map);
				return true;
			}
			int count = GenRadial.NumCellsInRadius(SeatRadius);
			for (int k = 0; k < count; k++)
			{
				cell = spot.Position + GenRadial.RadialPattern[k];
				if (!Usable(cell, spot, pawn, true)) continue;
				chair = cell.GetEdifice(pawn.Map);
				return true;
			}
			cell = here;
			if (near && Usable(here, spot, pawn, false)) return true;
			int start = Rand.Range(0, count);
			for (int k = 0; k < count; k++)
			{
				cell = spot.Position + GenRadial.RadialPattern[(start + k) % count];
				if (Usable(cell, spot, pawn, false)) return true;
			}
			cell = IntVec3.Invalid;
			return false;
		}

		public static Job GatherJob(Pawn pawn, Building_MarketMarker marker)
		{
			CollectSpots(pawn, marker);
			for (int i = 0; i < candidates.Count; i++)
			{
				Thing spot = candidates[i];
				if (!pawn.CanReach(spot, PathEndMode.Touch, Danger.None)) continue;
				if (!TryFindSeat(spot, pawn, out IntVec3 cell, out Building chair)) continue;
				candidates.Clear();
				Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_EveningGather, spot, cell, chair);
				job.expiryInterval = (int)(pawn.Position.DistanceTo(cell) * TicksPerCell) + Rand.Range(MinStay, MaxStay);
				return job;
			}
			candidates.Clear();
			return null;
		}
	}

	public class JobGiver_MarketEvening : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			Map map = pawn.Map;
			if (!pawn.RaceProps.Humanlike || !MarketEvening.On(map)) return null;
			Lord lord = pawn.GetLord();
			if (lord == null) return null;
			if (lord.LordJob is LordJob_MarketShoppers shoppers)
				return MarketSnacks.EveningDrink(pawn) ?? MarketEvening.GatherJob(pawn, shoppers.marker as Building_MarketMarker);
			if (!MarketManager.AnyVisits) return null;
			StallVisit v = MarketManager.Instance.VisitFor(lord);
			if (v == null || v.closed || !v.StallOk) return null;
			if (pawn.GetTraderCaravanRole() == TraderCaravanRole.Chattel) return null;
			if (pawn == v.trader && (v.feeDue || !v.arrivalSignalled || MarketBreaks.StallBusy(v, map))) return null;
			return MarketEvening.GatherJob(pawn, MarketUtility.MarkerFor(v.stall));
		}
	}
}
