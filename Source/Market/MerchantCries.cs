using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Market
{
	public static class MerchantCries
	{
		private const float Chance = 0.1f;
		private const int Gap = 500;
		private const float EarShot = 14f;
		private const float Seconds = 4f;
		private const int TopPicks = 3;

		private const int Phrases = 16;

		private static readonly List<Thing> wares = new List<Thing>();
		private static int lastCry = -Gap;

		public static void Clear() => lastCry = -Gap;

		private static bool Ready(Pawn crier)
		{
			if (!Rand.Chance(Chance)) return false;
			int now = Find.TickManager.TicksGame;
			if (now >= lastCry && now - lastCry < Gap) return false;
			if (!crier.Position.ShouldSpawnMotesAt(crier.Map)) return false;
			return Heard(crier);
		}

		private static bool Heard(Pawn crier)
		{
			bool colonist = crier.Faction == Faction.OfPlayer;
			IReadOnlyList<Pawn> pawns = crier.Map.mapPawns.AllHumanlikeSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == crier || p.Downed || !p.Awake() || !p.Position.InHorDistOf(crier.Position, EarShot)) continue;
				if (colonist && p.Faction == Faction.OfPlayer) continue;
				if (p.HostileTo(Faction.OfPlayer) || RimMarket.AI.MarketBreaks.CaravanFolk(p)) continue;
				return true;
			}
			return false;
		}

		public static void TryCry(StallVisit v)
		{
			Pawn trader = v.trader;
			if (trader?.trader == null || !MarketUtility.MerchantAtPost(v) || !Ready(trader)) return;
			wares.Clear();
			foreach (Thing t in trader.trader.Goods)
			{
				if (t == null || t.def == ThingDefOf.Silver) continue;
				if (t is Pawn p ? !p.RaceProps.Animal : !MarketUtility.Tradeable(t)) continue;
				wares.Add(t);
			}
			Shout(trader);
		}

		public static void TryCry(Building_MarketStall stall, Pawn keeper)
		{
			if (stall == null || keeper == null || !keeper.Spawned || !Ready(keeper)) return;
			wares.Clear();
			IReadOnlyList<Thing> stock = stall.Stock;
			for (int i = 0; i < stock.Count; i++)
			{
				if (stock[i] != null) wares.Add(stock[i]);
			}
			Shout(keeper);
		}

		private static void Shout(Pawn crier)
		{
			if (wares.Count == 0) return;
			wares.SortByDescending(t => t.MarketValue);
			Thing pick = wares[Rand.Range(0, Mathf.Min(TopPicks, wares.Count))];
			wares.Clear();
			string text = ("RimMarket.Cry" + Rand.RangeInclusive(1, Phrases)).Translate(Label(pick)).ToString().CapitalizeFirst();
			lastCry = Find.TickManager.TicksGame;
			MoteMaker.ThrowText(crier.DrawPos, crier.Map, text, Seconds);
		}

		private static string Label(Thing t)
		{
			if (t is Pawn p) return p.KindLabel;
			Thing inner = t is MinifiedThing m && m.InnerThing != null ? m.InnerThing : t;
			return GenLabel.ThingLabel(inner.def, inner.Stuff);
		}
	}
}
