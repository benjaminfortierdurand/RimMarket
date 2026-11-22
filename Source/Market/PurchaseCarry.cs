using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Market
{
	public static class PurchaseCarry
	{
		private const float HandAltitude = 0.033f;

		private static readonly List<Pawn> pawns = new List<Pawn>();
		private static readonly List<Thing> things = new List<Thing>();

		public static bool Any => pawns.Count > 0;

		public static void Clear()
		{
			pawns.Clear();
			things.Clear();
		}

		public static void Show(Pawn pawn, Thing thing)
		{
			if (pawn == null || thing == null) return;
			int i = pawns.IndexOf(pawn);
			if (i >= 0)
			{
				things[i] = thing;
				return;
			}
			pawns.Add(pawn);
			things.Add(thing);
		}

		private static bool Stale(Pawn p, Thing t)
		{
			if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.inventory == null) return true;
			return t == null || t.Destroyed || t.holdingOwner != p.inventory.innerContainer;
		}

		public static void Draw()
		{
			Map map = Find.CurrentMap;
			bool visible = map != null && !WorldRendererUtility.WorldRendered;
			CellRect view = visible ? Find.CameraDriver.CurrentViewRect.ExpandedBy(1) : CellRect.Empty;
			for (int i = pawns.Count - 1; i >= 0; i--)
			{
				Pawn p = pawns[i];
				Thing t = things[i];
				if (Stale(p, t))
				{
					pawns.RemoveAt(i);
					things.RemoveAt(i);
					continue;
				}
				if (!visible || p.Map != map || !view.Contains(p.Position) || p.Position.Fogged(map)) continue;
				if (p.Downed || p.GetPosture() != PawnPosture.Standing || p.carryTracker?.CarriedThing != null) continue;
				Vector3 pos = p.DrawPos;
				pos.y += HandAltitude;
				PawnRenderUtility.DrawCarriedThing(p, pos, t);
			}
		}
	}
}
