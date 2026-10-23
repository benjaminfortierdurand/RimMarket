using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class PlaceWorker_MarketStall : PlaceWorker
	{
		private const float OverlapTolerance = 0.7f;
		private const float NeighbourRange = 12f;

		private static readonly List<IntVec3> BackCells = new List<IntVec3>();
		private static readonly List<IntVec3> FrontCell = new List<IntVec3> { IntVec3.Invalid };
		private static readonly List<IntVec3> NeighbourMerchants = new List<IntVec3>();
		private static readonly List<IntVec3> NeighbourCustomers = new List<IntVec3>();
		private static readonly List<Spot> Others = new List<Spot>();
		private static readonly Color NeighbourMerchantColor = new Color(1f, 1f, 1f, 0.4f);
		private static readonly Color NeighbourCustomerColor = new Color(0f, 1f, 1f, 0.4f);

		private struct Spot
		{
			public Rect canopy;
			public CellRect body;
			public IntVec3 merchant;
			public IntVec3 customer;

			public Spot(ThingDef def, IntVec3 pos, Rot4 rot, bool flipped)
			{
				canopy = CanopyRect(def, pos, rot);
				body = GenAdj.OccupiedRect(pos, rot, def.size);
				merchant = CompMarketStall.MerchantCellFor(def, pos, rot, flipped);
				customer = CompMarketStall.CustomerCellFor(def, pos, rot);
			}
		}

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			IntVec3 back = CompMarketStall.BackFor(rot);
			IntVec3 side = CompMarketStall.SideFor(rot);
			IntVec3 merchant = CompMarketStall.MerchantCellFor(def, center, rot, CompMarketStall.FlippedOf(thing));
			BackCells.Clear();
			BackCells.Add(merchant);
			if (merchant == center + back)
			{
				BackCells.Add(merchant + side);
				BackCells.Add(merchant - side);
			}
			GenDraw.DrawFieldEdges(BackCells, Color.white);
			FrontCell[0] = CompMarketStall.CustomerCellFor(def, center, rot);
			GenDraw.DrawFieldEdges(FrontCell, Color.cyan);
			DrawNeighbours(center, thing);
		}

		private static void DrawNeighbours(IntVec3 center, Thing self)
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			CollectOthers(map, self, null);
			NeighbourMerchants.Clear();
			NeighbourCustomers.Clear();
			for (int i = 0; i < Others.Count; i++)
			{
				Spot s = Others[i];
				if (!s.merchant.InHorDistOf(center, NeighbourRange)) continue;
				NeighbourMerchants.Add(s.merchant);
				NeighbourCustomers.Add(s.customer);
			}
			if (NeighbourMerchants.Count == 0) return;
			GenDraw.DrawFieldEdges(NeighbourMerchants, NeighbourMerchantColor);
			GenDraw.DrawFieldEdges(NeighbourCustomers, NeighbourCustomerColor);
		}

		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			ThingDef def = checkingDef as ThingDef;
			bool flipped = CompMarketStall.FlippedOf(thing);
			IntVec3 merchant = CompMarketStall.MerchantCellFor(def, loc, rot, flipped);
			if (!merchant.InBounds(map) || !merchant.Walkable(map))
				return "RimMarket.PlaceMerchantCellBlocked".Translate().ToString();
			if (def == null) return true;
			IntVec3 customer = CompMarketStall.CustomerCellFor(def, loc, rot);
			if (!customer.InBounds(map) || !customer.Walkable(map))
				return "RimMarket.PlaceCustomerCellBlocked".Translate().ToString();
			string why = Conflict(new Spot(def, loc, rot, flipped), map, thingToIgnore, thing);
			if (why != null) return why.Translate().ToString();
			return true;
		}

		public static string SwapConflict(CompMarketStall stall)
		{
			Thing t = stall.parent;
			Map map = t.Map;
			if (map == null) return null;
			var spot = new Spot(t.def, t.Position, t.Rotation, !stall.flipped);
			if (!spot.merchant.InBounds(map) || !spot.merchant.Walkable(map))
				return "RimMarket.PlaceMerchantCellBlocked".Translate().ToString();
			string why = Conflict(spot, map, t, null);
			return why?.Translate().ToString();
		}

		public static Rect CanopyRect(ThingDef def, IntVec3 pos, Rot4 rot)
		{
			CompProperties_MarketStall p = def.GetCompProperties<CompProperties_MarketStall>();
			Vector3 c = GenThing.TrueCenter(pos, rot, def.size, 0f);
			IntVec2 size = rot.IsHorizontal ? new IntVec2(def.size.z, def.size.x) : def.size;
			FloatRange xr = FloatRange.Zero;
			FloatRange zr = FloatRange.Zero;
			if (p != null)
			{
				xr = rot.IsHorizontal ? p.canvasXVertical : p.canvasX;
				zr = rot.IsHorizontal ? p.canvasZVertical : p.canvasZ;
			}
			if (xr.max <= xr.min || zr.max <= zr.min)
			{
				Vector2 draw = def.graphicData?.drawSize ?? Vector2.one;
				Vector3 offset = def.graphicData?.DrawOffsetForRot(rot) ?? Vector3.zero;
				xr = new FloatRange(offset.x - draw.x / 2f, offset.x + draw.x / 2f);
				zr = new FloatRange(offset.z - draw.y / 2f, offset.z + draw.y / 2f);
			}
			else if (rot == Rot4.West)
			{
				xr = new FloatRange(-xr.max, -xr.min);
			}
			float minX = Mathf.Min(xr.min, -size.x / 2f);
			float maxX = Mathf.Max(xr.max, size.x / 2f);
			float minZ = Mathf.Min(zr.min, -size.z / 2f);
			float maxZ = Mathf.Max(zr.max, size.z / 2f);
			return Rect.MinMaxRect(c.x + minX, c.z + minZ, c.x + maxX, c.z + maxZ);
		}

		private static bool Clash(Rect a, Rect b)
		{
			float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
			float z = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
			return x > OverlapTolerance && z > OverlapTolerance;
		}

		private static string Conflict(Spot mine, Map map, Thing ignoreA, Thing ignoreB)
		{
			CollectOthers(map, ignoreA, ignoreB);
			for (int i = 0; i < Others.Count; i++)
			{
				Spot o = Others[i];
				if (mine.body.Contains(o.merchant) || mine.body.Contains(o.customer)
					|| mine.merchant == o.merchant || mine.merchant == o.customer || mine.customer == o.merchant)
					return "RimMarket.PlaceTakesNeighbourSpot";
				if (o.body.Contains(mine.merchant) || o.body.Contains(mine.customer))
					return "RimMarket.PlaceSpotUnderStall";
				if (Clash(mine.canopy, o.canopy)) return "RimMarket.PlaceCanopyOverlap";
			}
			return null;
		}

		private static void CollectOthers(Map map, Thing ignoreA, Thing ignoreB)
		{
			Others.Clear();
			List<ThingDef> defs = MarketStallDefs.All;
			for (int d = 0; d < defs.Count; d++)
			{
				List<Thing> things = map.listerThings.ThingsOfDef(defs[d]);
				for (int i = 0; i < things.Count; i++)
				{
					Thing t = things[i];
					if (t == ignoreA || t == ignoreB) continue;
					Others.Add(new Spot(defs[d], t.Position, t.Rotation, MarketStallDefs.StallOf(t)?.flipped == true));
				}
			}
			CollectPlanned(map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint), ignoreA, ignoreB);
			CollectPlanned(map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame), ignoreA, ignoreB);
		}

		private static void CollectPlanned(List<Thing> planned, Thing ignoreA, Thing ignoreB)
		{
			for (int i = 0; i < planned.Count; i++)
			{
				Thing t = planned[i];
				if (t == ignoreA || t == ignoreB) continue;
				if (!(t.def.entityDefToBuild is ThingDef built) || !MarketStallDefs.All.Contains(built)) continue;
				Others.Add(new Spot(built, t.Position, t.Rotation, CompMarketStall.FlippedOf((t as Blueprint_Install)?.ThingToInstall)));
			}
		}
	}
}
