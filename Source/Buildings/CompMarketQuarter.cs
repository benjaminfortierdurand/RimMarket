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
			if (!parent.Spawned) return;
			if (HasRegion) GenDraw.DrawFieldEdges(regionCells);
			else GenDraw.DrawRadiusRing(parent.Position, radius);
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
