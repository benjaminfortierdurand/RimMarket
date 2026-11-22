using System.Collections.Generic;
using HarmonyLib;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TryFindChairOrSpot))]
	public static class Patch_MarketSeats
	{
		public static void Postfix(Pawn pawn, ref IntVec3 cell, ref bool __result)
		{
			if (pawn?.Map == null || pawn.Faction == null || pawn.Faction.IsPlayer || !pawn.RaceProps.Humanlike) return;
			if (!MarketSeats.TryFind(pawn, cell, out IntVec3 seat)) return;
			cell = seat;
			__result = true;
		}
	}

	public static class MarketSeats
	{
		public static bool TryFind(Pawn pawn, IntVec3 current, out IntVec3 seat)
		{
			seat = IntVec3.Invalid;
			Map map = pawn.Map;
			List<Thing> markers = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker);
			if (markers.Count == 0) return false;
			bool folk = MarketFolk(pawn);
			if (!folk && !MarketUtility.IsShoppingGuest(pawn)) return false;
			var quarters = new List<CompMarketQuarter>();
			for (int i = 0; i < markers.Count; i++)
			{
				CompMarketQuarter q = (markers[i] as Building_MarketMarker)?.Quarter;
				if (q != null) quarters.Add(q);
			}
			if (quarters.Count == 0) return false;
			if (!folk && !Inside(quarters, pawn.Position)) return false;
			if (current.IsValid && Inside(quarters, current) && current.GetEdifice(map)?.def.building?.isSittable == true) return false;
			float best = float.MaxValue;
			List<Building> all = map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < all.Count; i++)
			{
				Building chair = all[i];
				if (chair.def.building == null || !chair.def.building.isSittable || !Inside(quarters, chair.Position)) continue;
				if (chair.IsBurning() || !pawn.CanReserve(chair)) continue;
				if (!Toils_Ingest.TryFindFreeSittingSpotOnThing(chair, pawn, out IntVec3 spot) || !AtTable(spot, map)) continue;
				float dist = spot.DistanceToSquared(pawn.Position);
				if (dist >= best || !pawn.CanReach(spot, PathEndMode.OnCell, Danger.None)) continue;
				best = dist;
				seat = spot;
			}
			return seat.IsValid;
		}

		private static bool MarketFolk(Pawn pawn)
		{
			Lord lord = pawn.GetLord();
			if (lord == null) return false;
			if (lord.LordJob is LordJob_MarketShoppers) return true;
			return MarketManager.Instance?.VisitFor(lord) != null;
		}

		private static bool Inside(List<CompMarketQuarter> quarters, IntVec3 c)
		{
			for (int i = 0; i < quarters.Count; i++)
			{
				if (quarters[i].InQuarter(c)) return true;
			}
			return false;
		}

		private static bool AtTable(IntVec3 c, Map map)
		{
			for (int i = 0; i < 4; i++)
			{
				IntVec3 n = c + GenAdj.CardinalDirections[i];
				if (!n.InBounds(map)) continue;
				Building b = n.GetEdifice(map);
				if (b != null && b.def.surfaceType == SurfaceType.Eat) return true;
			}
			return false;
		}
	}
}
