using System.Collections.Generic;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public static class GuardWatch
	{
		public const float WornRest = 0.36f;
		public const float FreshRest = 0.42f;
		public const int LongShift = 20000;
		private const int HandoverTimeout = 5000;
		private const int RetryDelay = 1250;
		private const float HandoverRange = 2.5f;

		public static bool IsGuard(Pawn p)
		{
			if (p == null || !p.RaceProps.Humanlike) return false;
			TraderCaravanRole role = p.GetTraderCaravanRole();
			return role != TraderCaravanRole.Trader && role != TraderCaravanRole.Carrier && role != TraderCaravanRole.Chattel;
		}

		private static bool Fit(Pawn p, Lord lord)
		{
			return p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && p.GetLord() == lord && IsGuard(p);
		}

		private static float Rest(Pawn p) => p.needs?.rest?.CurLevel ?? 1f;

		private static bool Resting(Pawn p)
		{
			if (!p.Awake()) return true;
			JobDef job = p.CurJobDef;
			return job == JobDefOf.LayDown || job == RimMarketDefOf.RimMarket_SleepAtInn;
		}

		public static bool OnWatch(StallVisit v, Pawn p) => p != null && (v.posted.Contains(p) || v.reliefIn == p);

		public static int SlotOf(StallVisit v, Pawn p)
		{
			if (p == null) return -1;
			int slot = v.posted.IndexOf(p);
			if (slot < 0 && p == v.reliefIn) slot = v.reliefSlot;
			return slot;
		}

		public static void ClearRelief(StallVisit v)
		{
			v.reliefIn = null;
			v.reliefSlot = -1;
			v.reliefSince = -1;
		}

		public static void Roster(StallVisit v, Lord lord, int wanted)
		{
			int now = Find.TickManager.TicksGame;
			if (v.reliefIn != null && (!Fit(v.reliefIn, lord) || v.reliefSlot < 0 || v.reliefSlot >= wanted || v.reliefSlot >= v.posted.Count)) ClearRelief(v);
			for (int i = 0; i < v.posted.Count; i++)
			{
				if (v.posted[i] != null && !Fit(v.posted[i], lord)) v.posted[i] = null;
			}
			while (v.posted.Count > wanted)
			{
				v.posted.RemoveAt(v.posted.Count - 1);
				v.postedSince.RemoveAt(v.postedSince.Count - 1);
			}
			while (v.posted.Count < wanted)
			{
				v.posted.Add(null);
				v.postedSince.Add(now);
			}
			if (v.reliefIn != null && v.posted[v.reliefSlot] == null)
			{
				v.posted[v.reliefSlot] = v.reliefIn;
				v.postedSince[v.reliefSlot] = now;
				ClearRelief(v);
			}
			for (int i = 0; i < v.posted.Count; i++)
			{
				if (v.posted[i] != null) continue;
				Pawn g = Freshest(v, lord, -1f, true);
				if (g == null) break;
				v.posted[i] = g;
				v.postedSince[i] = now;
			}
		}

		private static Pawn Freshest(StallVisit v, Lord lord, float minRest, bool allowResting)
		{
			Pawn best = null;
			bool bestResting = true;
			float bestRest = -1f;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (!Fit(p, lord) || OnWatch(v, p)) continue;
				float rest = Rest(p);
				if (rest < minRest) continue;
				bool resting = Resting(p);
				if (resting && !allowResting) continue;
				if (best != null && ((resting && !bestResting) || (resting == bestResting && rest <= bestRest))) continue;
				best = p;
				bestRest = rest;
				bestResting = resting;
			}
			return best;
		}

		private static bool Vacant(StallVisit v, Lord lord)
		{
			for (int i = 0; i < v.posted.Count; i++)
			{
				if (!Fit(v.posted[i], lord)) return true;
			}
			return false;
		}

		public static bool Tick(StallVisit v, Lord lord, List<Pawn> nudge)
		{
			int now = Find.TickManager.TicksGame;
			if (v.reliefIn != null)
			{
				if (!Fit(v.reliefIn, lord) || v.reliefSlot < 0 || v.reliefSlot >= v.posted.Count)
				{
					ClearRelief(v);
					return true;
				}
				Pawn outgoing = v.posted[v.reliefSlot];
				PawnDuty duty = v.reliefIn.mindState?.duty;
				bool arrived = duty != null && duty.def == RimMarketDefOf.RimMarket_GuardPost && v.reliefIn.Position.InHorDistOf(duty.focus.Cell, HandoverRange);
				if (outgoing == null || arrived)
				{
					HandOver(v, outgoing);
					if (outgoing != null) nudge.Add(outgoing);
					return true;
				}
				if (now - v.reliefSince > HandoverTimeout)
				{
					ClearRelief(v);
					v.nextReliefTick = now + RetryDelay;
					return true;
				}
				return false;
			}
			if (Vacant(v, lord) && Freshest(v, lord, -1f, true) != null) return true;
			if (now < v.nextReliefTick) return false;
			bool needed = false;
			for (int i = 0; i < v.posted.Count; i++)
			{
				Pawn g = v.posted[i];
				if (g == null) continue;
				float rest = Rest(g);
				bool worn = rest < WornRest;
				if (!worn && now - v.postedSince[i] < LongShift) continue;
				needed = true;
				Pawn fresh = Freshest(v, lord, worn ? FreshRest : Mathf.Max(FreshRest, rest + 0.05f), false);
				if (fresh == null) continue;
				v.reliefIn = fresh;
				v.reliefSlot = i;
				v.reliefSince = now;
				nudge.Add(fresh);
				return true;
			}
			if (needed) v.nextReliefTick = now + RetryDelay;
			return false;
		}

		private static void HandOver(StallVisit v, Pawn outgoing)
		{
			Pawn incoming = v.reliefIn;
			int slot = v.reliefSlot;
			v.posted[slot] = incoming;
			v.postedSince[slot] = Find.TickManager.TicksGame;
			ClearRelief(v);
			if (outgoing == null || !outgoing.Spawned || !incoming.Spawned || outgoing.interactions == null) return;
			InteractionDef talk = RimMarketDefOf.RimMarket_ChangeOfGuard;
			if (outgoing.interactions.CanInteractNowWith(incoming, talk)) outgoing.interactions.TryInteractWith(incoming, talk);
		}

		public static void Nudge(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == null || !p.Spawned || p.jobs?.curJob == null || !p.Awake()) continue;
				if (Idle(p.CurJobDef)) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
			}
			pawns.Clear();
		}

		private static bool Idle(JobDef job)
		{
			return job == RimMarketDefOf.RimMarket_StandAtStall || job == RimMarketDefOf.RimMarket_RestAtSpot
				|| job == RimMarketDefOf.RimMarket_CheckAnimals || job == JobDefOf.Wait || job == JobDefOf.Wait_Wander
				|| job == JobDefOf.GotoWander || job == JobDefOf.Goto || job == JobDefOf.Wait_MaintainPosture;
		}

		public static string InspectLine(StallVisit v)
		{
			var names = new List<string>();
			for (int i = 0; i < v.posted.Count; i++)
			{
				if (v.posted[i] != null && v.posted[i].Spawned) names.Add(v.posted[i].LabelShortCap);
			}
			if (names.Count == 0) return null;
			string line = "RimMarket.StallWatch".Translate(string.Join(", ", names));
			if (v.reliefIn != null && v.reliefSlot >= 0 && v.reliefSlot < v.posted.Count && v.posted[v.reliefSlot] != null)
				line += "\n" + "RimMarket.StallRelief".Translate(v.reliefIn.LabelShortCap, v.posted[v.reliefSlot].LabelShortCap);
			return line;
		}
	}
}
