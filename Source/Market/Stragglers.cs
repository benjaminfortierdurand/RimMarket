using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public class LeavingGroup : IExposable
	{
		public List<Pawn> pawns = new List<Pawn>();
		public Faction faction;
		public int endTick;

		public void ExposeData()
		{
			Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
			Scribe_References.Look(ref faction, "faction");
			Scribe_Values.Look(ref endTick, "endTick");
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (pawns == null) pawns = new List<Pawn>();
			pawns.RemoveAll(p => p == null);
		}
	}

	public static class Stragglers
	{
		private const int GraceTicks = 2500;
		private const int StuckTicks = 15000;
		private const int ForgetTicks = 180000;

		public static void Remember(StallVisit v)
		{
			if (v?.lord == null) return;
			List<Pawn> owned = v.lord.ownedPawns;
			for (int i = 0; i < owned.Count; i++)
			{
				Pawn p = owned[i];
				if (p != null && !v.members.Contains(p)) v.members.Add(p);
			}
		}

		public static LeavingGroup Watch(StallVisit v)
		{
			if (v?.faction == null) return null;
			Remember(v);
			var group = new LeavingGroup { faction = v.faction, endTick = Find.TickManager.TicksGame };
			for (int i = 0; i < v.members.Count; i++)
			{
				Pawn p = v.members[i];
				if (p != null && !p.Dead && p.Faction == v.faction) group.pawns.Add(p);
			}
			return group.pawns.Count > 0 ? group : null;
		}

		public static void Tick(List<LeavingGroup> groups)
		{
			int now = Find.TickManager.TicksGame;
			for (int i = groups.Count - 1; i >= 0; i--)
			{
				LeavingGroup g = groups[i];
				int age = now - g.endTick;
				if (g.faction == null || age > ForgetTicks)
				{
					groups.RemoveAt(i);
					continue;
				}
				if (age >= GraceTicks) SendHome(g, age);
				if (g.pawns.Count == 0) groups.RemoveAt(i);
			}
		}

		private static void SendHome(LeavingGroup g, int age)
		{
			var byMap = new Dictionary<Map, List<Pawn>>();
			bool hostile = g.faction.HostileTo(Faction.OfPlayer);
			for (int i = g.pawns.Count - 1; i >= 0; i--)
			{
				Pawn p = g.pawns[i];
				if (hostile || p == null || p.Dead || p.Destroyed || !p.Spawned || p.Faction != g.faction || p.IsPrisoner || p.IsSlave)
				{
					g.pawns.RemoveAt(i);
					continue;
				}
				if (p.Downed || p.InMentalState || p.HostFaction != null) continue;
				Lord lord = p.GetLord();
				if (lord != null)
				{
					if (!(lord.LordJob is LordJob_TradeWithColony))
					{
						g.pawns.RemoveAt(i);
						continue;
					}
					if (age < StuckTicks || !Leaving(lord.CurLordToil)) continue;
				}
				if (!byMap.TryGetValue(p.Map, out List<Pawn> list))
				{
					list = new List<Pawn>();
					byMap[p.Map] = list;
				}
				list.Add(p);
				g.pawns.RemoveAt(i);
			}
			foreach (KeyValuePair<Map, List<Pawn>> kv in byMap)
			{
				for (int i = 0; i < kv.Value.Count; i++) kv.Value[i].GetLord()?.RemovePawn(kv.Value[i]);
				LordMaker.MakeNewLord(g.faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, canDig: true), kv.Key, kv.Value);
				Log.Message("[RimMarket] " + kv.Value.Count + " " + g.faction.Name + " visitors stayed behind after their market visit, sending them home.");
			}
		}

		private static bool Leaving(LordToil toil)
		{
			return toil is LordToil_ExitMapAndEscortCarriers || toil is LordToil_ExitMap || toil is LordToil_ExitMapTraderFighting;
		}
	}
}
