using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class Trigger_MarketTrapped : Trigger
	{
		private const int Interval = 197;
		private const int StrikesNeeded = 3;

		private int strikes;

		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (signal.type != TriggerSignalType.Tick || Find.TickManager.TicksGame % Interval != 0) return false;
			Pawn stuck = TrappedPawn(lord);
			if (stuck == null)
			{
				strikes = 0;
				return false;
			}
			strikes++;
			if (strikes < StrikesNeeded) return false;
			strikes = 0;
			if (Prefs.DevMode) Report(lord, stuck);
			return true;
		}

		private static readonly Dictionary<(int, int, int), bool> districtReach = new Dictionary<(int, int, int), bool>();

		public static Pawn TrappedPawn(Lord lord)
		{
			districtReach.Clear();
			bool atStall = lord.CurLordToil is LordToil_MarketStall;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (!p.Spawned || p.Dead || p.Downed) continue;
				if (atStall && !p.RaceProps.Humanlike) continue;
				District district = p.Position.GetDistrict(p.Map);
				if (district == null)
				{
					if (!p.CanReachMapEdge()) return p;
					continue;
				}
				int flags = (p.ShouldAvoidFences ? 1 : 0) + (p.CanOpenDoors ? 2 : 0) + (p.CanOpenAnyDoor ? 4 : 0);
				var key = (district.ID, flags, p.Faction?.loadID ?? -1);
				if (!districtReach.TryGetValue(key, out bool reaches))
				{
					reaches = p.CanReachMapEdge();
					districtReach[key] = reaches;
				}
				if (!reaches) return p;
			}
			return null;
		}

		private static void Report(Lord lord, Pawn p)
		{
			Map map = p.Map;
			IntVec3 c = p.Position;
			Room room = p.GetRoom();
			Log.Warning("[RimMarket] a " + (lord.faction?.Name ?? "?") + " caravan thinks it is trapped and will dig out. "
				+ p.LabelShort + " (" + p.kindDef?.defName + ", " + p.GetTraderCaravanRole() + ", faction " + (p.Faction?.Name ?? "none") + ")"
				+ " at " + c + ", toil " + (lord.CurLordToil?.GetType().Name ?? "?")
				+ ", job " + (p.CurJobDef?.defName ?? "none")
				+ ", region " + (c.GetRegion(map) != null ? "ok" : "none")
				+ ", edifice " + (c.GetEdifice(map)?.def.defName ?? "none")
				+ ", opens doors " + p.CanOpenDoors + "/" + p.CanOpenAnyDoor
				+ ", fence blocked " + p.FenceBlocked
				+ ", room " + (room == null ? "none" : room.CellCount + " cells, touches edge " + room.TouchesMapEdge));
		}
	}
}
