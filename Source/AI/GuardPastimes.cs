using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimMarket.Buildings;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.AI
{
	public static class GuardPastimes
	{
		private const int MinPlay = 1500;
		private const int MaxPlay = 3000;
		private const int TicksPerCell = 14;
		private const int ErrorKey = 0x524D4B42;

		private static readonly MethodInfo CanInteract = AccessTools.Method(typeof(JoyGiver_InteractBuilding), "CanInteractWith");
		private static readonly MethodInfo GivePlay = AccessTools.Method(typeof(JoyGiver_InteractBuilding), "TryGivePlayJob");
		private static readonly HashSet<JoyGiverDef> broken = new HashSet<JoyGiverDef>();
		private static Dictionary<ThingDef, List<JoyGiverDef>> givers;

		private static Dictionary<ThingDef, List<JoyGiverDef>> Givers
		{
			get
			{
				if (givers != null) return givers;
				givers = new Dictionary<ThingDef, List<JoyGiverDef>>();
				foreach (JoyGiverDef g in DefDatabase<JoyGiverDef>.AllDefsListForReading)
				{
					if (g.thingDefs == null || !(g.Worker is JoyGiver_InteractBuilding)) continue;
					foreach (ThingDef t in g.thingDefs)
					{
						if (t == null) continue;
						if (!givers.TryGetValue(t, out List<JoyGiverDef> list)) givers[t] = list = new List<JoyGiverDef>();
						if (!list.Contains(g)) list.Add(g);
					}
				}
				return givers;
			}
		}

		public static bool Playing(Job job) => job != null && job.doUntilGatheringEnded && job.def.joyKind != null;

		public static Job PlayJob(Pawn pawn, Thing spot, float radius)
		{
			if (CanInteract == null || GivePlay == null || pawn?.Map == null || spot == null) return null;
			Map map = pawn.Map;
			CompMarketQuarter q = spot.TryGetComp<CompMarketQuarter>();
			var games = new List<Thing>();
			foreach (ThingDef def in Givers.Keys)
			{
				List<Thing> things = map.listerThings.ThingsOfDef(def);
				for (int i = 0; i < things.Count; i++)
				{
					Thing t = things[i];
					if (t.Faction != Faction.OfPlayer) continue;
					bool inside = q != null ? q.InQuarter(t.Position) : t.Position.DistanceTo(spot.Position) <= radius;
					if (inside) games.Add(t);
				}
			}
			if (games.Count == 0) return null;
			games.Shuffle();
			for (int i = 0; i < games.Count; i++)
			{
				Thing game = games[i];
				if (!pawn.CanReach(game, PathEndMode.Touch, Danger.Some)) continue;
				foreach (JoyGiverDef g in Givers[game.def])
				{
					if (broken.Contains(g)) continue;
					try
					{
						var worker = (JoyGiver_InteractBuilding)g.Worker;
						if (!(bool)CanInteract.Invoke(worker, new object[] { pawn, game, false })) continue;
						if (!(GivePlay.Invoke(worker, new object[] { pawn, game }) is Job job)) continue;
						job.doUntilGatheringEnded = true;
						job.expiryInterval = (int)(pawn.Position.DistanceTo(game.Position) * TicksPerCell) + Rand.Range(MinPlay, MaxPlay);
						return job;
					}
					catch (Exception e)
					{
						broken.Add(g);
						Log.ErrorOnce("[RimMarket] off-duty guards cannot use " + g.defName + ", skipping it: " + e, ErrorKey ^ g.shortHash);
					}
				}
			}
			return null;
		}
	}
}
