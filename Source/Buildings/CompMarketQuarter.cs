using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class CompProperties_MarketQuarter : CompProperties
	{
		public float defaultRadius = CompMarketQuarter.DefRadius;
		public string areaKey = "Market";

		public CompProperties_MarketQuarter() { compClass = typeof(CompMarketQuarter); }
	}

	public class CompMarketQuarter : ThingComp
	{
		public const float MinRadius = 6f;
		public const float MaxRadius = 30f;
		public const float DefRadius = 15f;

		private float radius = DefRadius;
		private List<IntVec3> regionCells;
		private HashSet<IntVec3> regionSet;

		public float Radius => radius;

		public string AreaKey => (props as CompProperties_MarketQuarter)?.areaKey ?? "Market";

		public bool HasRegion => regionCells != null && regionCells.Count > 0;

		private HashSet<IntVec3> RegionSet
		{
			get
			{
				if (regionSet == null && regionCells != null) regionSet = new HashSet<IntVec3>(regionCells);
				return regionSet;
			}
		}

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			if (props is CompProperties_MarketQuarter p) radius = p.defaultRadius;
		}

		public bool InQuarter(IntVec3 c)
		{
			if (HasRegion) return RegionSet.Contains(c);
			return parent.Spawned && c.DistanceTo(parent.Position) <= radius + 0.5f;
		}

		public bool InReach(IntVec3 c) => parent.Spawned && c.DistanceTo(parent.Position) <= MaxRadius + 0.5f;

		public bool IsMarkerCell(IntVec3 c) => parent.Spawned && parent.OccupiedRect().Contains(c);

		public void DrawArea()
		{
			if (!parent.Spawned) return;
			if (HasRegion) GenDraw.DrawFieldEdges(regionCells);
			else GenDraw.DrawRadiusRing(parent.Position, radius);
		}

		public void Paint(List<IntVec3> cells, bool add)
		{
			if (!parent.Spawned || cells == null) return;
			HashSet<IntVec3> set = HasRegion ? new HashSet<IntVec3>(regionCells) : StartCells(add);
			for (int i = 0; i < cells.Count; i++)
			{
				if (add) set.Add(cells[i]);
				else if (!IsMarkerCell(cells[i])) set.Remove(cells[i]);
			}
			foreach (IntVec3 c in parent.OccupiedRect().Cells) set.Add(c);
			regionCells = new List<IntVec3>(set);
			regionSet = null;
			float maxDist = 0f;
			for (int i = 0; i < regionCells.Count; i++) maxDist = Mathf.Max(maxDist, regionCells[i].DistanceTo(parent.Position));
			radius = Mathf.Clamp(Mathf.Ceil(maxDist), MinRadius, MaxRadius);
		}

		private HashSet<IntVec3> StartCells(bool add)
		{
			var set = new HashSet<IntVec3>();
			if (add) return set;
			foreach (IntVec3 c in CellRect.CenteredOn(parent.Position, Mathf.CeilToInt(radius) + 1).Cells)
			{
				if (c.InBounds(parent.Map) && c.DistanceTo(parent.Position) <= radius + 0.5f) set.Add(c);
			}
			return set;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref radius, "radius", DefRadius);
			Scribe_Collections.Look(ref regionCells, "regionCells", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit) regionSet = null;
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			DrawArea();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Designator_MarketArea(this, add: true);
			yield return new Designator_MarketArea(this, add: false);
		}
	}
}
