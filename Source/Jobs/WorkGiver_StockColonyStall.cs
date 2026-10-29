using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Jobs
{
	public class WorkGiver_StockColonyStall : WorkGiver_Scanner
	{
		private static readonly List<Building_MarketStall> buffer = new List<Building_MarketStall>();

		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (pawn.Map == null) return true;
			ColonyStalls.Collect(pawn.Map, buffer);
			for (int i = 0; i < buffer.Count; i++)
			{
				if (buffer[i].Orders.Count > 0) return false;
			}
			return true;
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			ColonyStalls.Collect(pawn.Map, buffer);
			return buffer.Where(s => s.Orders.Count > 0).Cast<Thing>().ToList();
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			return Goods(pawn, t as Building_MarketStall, forced) != null;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			Building_MarketStall stall = t as Building_MarketStall;
			Thing goods = Goods(pawn, stall, forced);
			return goods == null ? null : HaulAIUtility.HaulToContainerJob(pawn, goods, stall);
		}

		private static Thing Goods(Pawn pawn, Building_MarketStall stall, bool forced)
		{
			if (stall == null || !stall.Spawned || !stall.ColonyStall || stall.IsForbidden(pawn)) return null;
			if (!pawn.CanReach(stall, PathEndMode.Touch, pawn.NormalMaxDanger())) return null;
			IReadOnlyList<StallOrder> orders = stall.Orders;
			for (int i = 0; i < orders.Count; i++)
			{
				ThingDef def = orders[i].def;
				if (def == null) continue;
				bool art = ColonyStalls.IsArt(def);
				ThingDef carried = art ? def.minifiedDef : def;
				if (stall.GetSpaceRemainingWithEnroute(carried, pawn) <= 0 || (art && stall.RoomFor(def) <= 0) || stall.SpareFor(def, null) <= 0) continue;
				Thing found = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(carried), PathEndMode.ClosestTouch,
					TraverseParms.For(pawn), 9999f, x => !x.IsForbidden(pawn) && ColonyStalls.StockDef(x) == def && stall.Accepts(x) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, x, forced));
				if (found != null) return found;
			}
			return null;
		}
	}
}
