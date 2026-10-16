using System.Collections.Generic;
using RimMarket.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class CompProperties_MarketQuarter : CompProperties
	{
		public float defaultRadius = CompMarketQuarter.DefRadius;

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
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.RadiusPlus".Translate(radius.ToString("F0")),
				defaultDesc = "RimMarket.RadiusDesc".Translate(),
				icon = RimMarketIcons.Plus,
				action = () => { radius = Mathf.Min(MaxRadius, radius + 1f); ClearRegion(); },
			};
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.RadiusMinus".Translate(radius.ToString("F0")),
				defaultDesc = "RimMarket.RadiusDesc".Translate(),
				icon = RimMarketIcons.Minus,
				action = () => { radius = Mathf.Max(MinRadius, radius - 1f); ClearRegion(); },
			};
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.RadiusAuto".Translate(),
				defaultDesc = "RimMarket.RadiusAutoDesc".Translate(),
				icon = RimMarketIcons.Auto,
				action = AutoFit,
			};
			yield return new Designator_MarketArea(this, add: true);
			yield return new Designator_MarketArea(this, add: false);
		}

		private void ClearRegion()
		{
			regionCells = null;
			regionSet = null;
		}

		private void AutoFit()
		{
			Map map = parent.Map;
			if (map == null || !parent.Spawned) return;
			IntVec3 origin = parent.Position;
			float limit = MaxRadius + 2f;
			var visited = new HashSet<IntVec3> { origin };
			var reached = new List<IntVec3> { origin };
			var queue = new Queue<IntVec3>();
			queue.Enqueue(origin);
			float maxDist = 0f;
			bool leaked = false;
			IntVec3[] dirs = { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West };
			while (queue.Count > 0)
			{
				IntVec3 c = queue.Dequeue();
				for (int i = 0; i < dirs.Length; i++)
				{
					IntVec3 n = c + dirs[i];
					if (!visited.Add(n)) continue;
					if (!n.InBounds(map)) { leaked = true; continue; }
					if (!n.Walkable(map)) continue;
					Building edifice = n.GetEdifice(map);
					if (edifice is Building_Door) continue;
					if (edifice != null && edifice.def.IsFence) continue;
					float dist = n.DistanceTo(origin);
					if (dist > limit) { leaked = true; continue; }
					if (dist > maxDist) maxDist = dist;
					reached.Add(n);
					queue.Enqueue(n);
				}
			}
			if (leaked)
			{
				Messages.Message("RimMarket.QuarterNotEnclosed".Translate(Mathf.RoundToInt(MaxRadius)),
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			regionCells = reached;
			regionSet = null;
			radius = Mathf.Clamp(Mathf.Ceil(maxDist), MinRadius, MaxRadius);
			Messages.Message("RimMarket.QuarterAutoFit".Translate(reached.Count),
				MessageTypeDefOf.NeutralEvent, historical: false);
		}
	}
}
