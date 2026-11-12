using System.Collections.Generic;
using RimMarket.AI;
using RimMarket.Compat;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public static class MarketTruce
	{
		public static bool Welcome(Map map, Faction faction)
		{
			if (map == null || faction == null) return false;
			if (HotelsBridge.Active) return BlockerFor(map, faction) == null;
			return !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, faction);
		}

		public static Lord BlockerFor(Map map, Faction faction)
		{
			if (map == null || faction == null) return null;
			Faction home = map.ParentFaction ?? Faction.OfPlayer;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (Blocks(lords[i], faction, home)) return lords[i];
			}
			return null;
		}

		public static List<Faction> BlockingFactions(Map map, Faction faction)
		{
			var found = new List<Faction>();
			if (map == null || faction == null) return found;
			Faction home = map.ParentFaction ?? Faction.OfPlayer;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (Blocks(lords[i], faction, home) && !found.Contains(lords[i].faction)) found.Add(lords[i].faction);
			}
			return found;
		}

		private static bool Blocks(Lord l, Faction faction, Faction home)
		{
			if (l.faction == null || l.faction == faction || l.faction == home || !l.AnyActivePawn) return false;
			LordJob job = l.LordJob;
			if (job != null && (!job.CanBlockHostileVisitors || job is LordJob_VoluntarilyJoinable)) return false;
			if (HotelsBridge.PeacefulGuests(l)) return false;
			return l.faction.HostileTo(faction) && !l.faction.HostileTo(home);
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
				if (other.LordJob is LordJob_MarketThief || !other.AnyActivePawn || HotelsBridge.PeacefulGuests(other)) continue;
				if (!other.faction.HostileTo(faction) || other.faction.HostileTo(host)) continue;
				return other;
			}
			return null;
		}
	}
}
