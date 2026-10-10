using System.Collections.Generic;
using RimMarket.AI;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public static class MarketTruce
	{
		public static bool Welcome(Map map, Faction faction)
		{
			return map != null && faction != null && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, faction);
		}

		public static Lord BlockerFor(Map map, Faction faction)
		{
			if (map == null || faction == null) return null;
			Faction home = map.ParentFaction ?? Faction.OfPlayer;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				Lord l = lords[i];
				if (l.faction == null || l.faction == faction || l.faction == home || !l.AnyActivePawn) continue;
				LordJob job = l.LordJob;
				if (job != null && (!job.CanBlockHostileVisitors || job is LordJob_VoluntarilyJoinable)) continue;
				if (l.faction.HostileTo(faction) && !l.faction.HostileTo(home)) return l;
			}
			return null;
		}

		public static Lord RivalOf(Lord lord)
		{
			Map map = lord?.Map;
			Faction faction = lord?.faction;
			if (map == null || faction == null) return null;
			Faction host = map.ParentFaction ?? Faction.OfPlayer;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				Lord other = lords[i];
				if (other == lord || other.faction == null || other.faction == faction || other.faction == host) continue;
				if (other.LordJob is LordJob_MarketThief || !other.AnyActivePawn) continue;
				if (!other.faction.HostileTo(faction) || other.faction.HostileTo(host)) continue;
				return other;
			}
			return null;
		}
	}
}
