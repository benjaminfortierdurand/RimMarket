using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimMarket.Fair;
using RimMarket.Inn;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Market
{
	public class LordJob_MarketShoppers : LordJob
	{
		private const int CheckInterval = 250;
		private const int FeedInterval = 2500;
		private const int MaxOvernight = 30000;

		public Thing marker;
		public int stayTicks = MarketShoppers.StayTicks;
		public bool innRated;

		public LordJob_MarketShoppers()
		{
		}

		public LordJob_MarketShoppers(Thing marker, int stayTicks)
		{
			this.marker = marker;
			this.stayTicks = stayTicks;
		}

		public override void LordJobTick()
		{
			if (Find.TickManager.TicksGame % FeedInterval == 0) MarketUtility.TopUpFood(lord, full: false);
		}

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();
			var shop = new LordToil_MarketShop(marker);
			graph.StartingToil = shop;
			var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: false, interruptCurrentJob: true) { useAvoidGrid = true };
			graph.AddToil(leave);
			var done = new Transition(shop, leave);
			done.AddTrigger(new Trigger_Custom(s => s.type == TriggerSignalType.Tick
				&& Find.TickManager.TicksGame % CheckInterval == 0
				&& lord != null && lord.ticksInToil >= stayTicks
				&& (lord.ticksInToil >= stayTicks + MaxOvernight || !InnUtility.AnyLodging(lord))));
			done.AddTrigger(new Trigger_Custom(s => s.type == TriggerSignalType.Tick
				&& Find.TickManager.TicksGame % CheckInterval == 0
				&& ((!MarketShoppers.MarketOpen(lord?.Map) && !InnUtility.AnyLodging(lord)) || MarketTruce.RivalOf(lord) != null)));
			done.AddTrigger(new Trigger_BecamePlayerEnemy());
			done.AddTrigger(new Trigger_PawnHarmed());
			done.AddPreAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(done);
			return graph;
		}

		public override void ExposeData()
		{
			Scribe_References.Look(ref marker, "marker");
			Scribe_Values.Look(ref stayTicks, "stayTicks", MarketShoppers.StayTicks);
			Scribe_Values.Look(ref innRated, "innRated");
		}
	}

	public class LordToil_MarketShop : LordToil
	{
		private readonly Thing marker;

		public LordToil_MarketShop(Thing marker)
		{
			this.marker = marker;
		}

		public override void UpdateAllDuties()
		{
			float radius = marker?.TryGetComp<CompMarketQuarter>()?.Radius ?? 8f;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null) continue;
				p.mindState.duty = new PawnDuty(RimMarketDefOf.RimMarket_ShopAtMarket, marker) { wanderRadius = radius, locomotion = LocomotionUrgency.Walk };
			}
		}
	}

	public class JobGiver_ShopAtMarket : ThinkNode_JobGiver
	{
		private const float VisitChance = 0.55f;

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!Rand.Chance(VisitChance)) return null;
			return MarketUtility.MakeVisitJob(pawn, shopper: true);
		}
	}

	public static class MarketShoppers
	{
		public const int StayTicks = 12500;
		private const int MinStay = 11000;
		private const int MaxStay = 14000;
		private const int MaxGroupsOnMap = 3;
		private const int MinSilver = 60;
		private const int MaxSilver = 220;

		public static bool MarketOpen(Map map)
		{
			if (map == null) return false;
			if (MarketManager.Instance != null && MarketManager.Instance.ActiveVisits(map) > 0) return true;
			return ColonyStalls.AnyStocked(map);
		}

		public static int GroupsOn(Map map)
		{
			if (map == null) return 0;
			int n = 0;
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].LordJob is LordJob_MarketShoppers || RimTollBridge.OriginalJob(lords[i]) is LordJob_MarketShoppers) n++;
			}
			return n;
		}

		public static bool RoomForMore(Map map) => GroupsOn(map) < MaxGroupsOnMap;

		public static Faction PickFaction(Faction exclude, Map map)
		{
			MarketManager mgr = MarketManager.Instance;
			bool fair = mgr != null && FairUtility.Running(map);
			return Find.FactionManager.AllFactionsVisible
				.Where(f => !f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && f.def.humanlikeFaction
					&& !f.HostileTo(Faction.OfPlayer) && MarketTruce.Welcome(map, f) && MarketUtility.CivilianKind(f) != null
					&& (!fair || mgr.FairInvited(f)))
				.OrderBy(f => f == exclude ? 1 : 0)
				.ThenBy(f => Rand.Value)
				.FirstOrDefault();
		}

		public static string Send(StallVisit v)
		{
			if (v?.map == null || !v.StallOk) return null;
			return Send(v.map, v.stall, v.faction);
		}

		private static IntVec3 Destination(Map map, CompMarketStall stall, Thing marker)
		{
			IntVec3 c = stall.CustomerCell;
			if (c.InBounds(map) && c.Standable(map)) return c;
			foreach (IntVec3 n in GenAdj.CellsAdjacent8Way(stall.parent))
			{
				if (n.InBounds(map) && n.Standable(map)) return n;
			}
			return marker.Position;
		}

		public static string Send(Map map, CompMarketStall stall, Faction exclude, float purse = 1f)
		{
			if (map == null || stall == null || !stall.Spawned) return null;
			Thing marker = MarketUtility.MarkerFor(stall);
			if (marker == null) return null;
			Faction faction = PickFaction(exclude, map);
			if (faction == null) return null;
			IntVec3 target = Destination(map, stall, marker);
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Friendly, false,
				c => map.reachability.CanReach(c, target, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors))))
				return null;
			PawnKindDef kind = MarketUtility.CivilianKind(faction);
			if (kind == null) return null;
			var group = new List<Pawn>();
			int size = Rand.RangeInclusive(1, 3);
			for (int i = 0; i < size; i++)
			{
				Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
				GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
				Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
				silver.stackCount = Mathf.RoundToInt(Rand.RangeInclusive(MinSilver, MaxSilver) * purse * MarketDifficulty.PurseFactor);
				p.inventory.innerContainer.TryAdd(silver);
				group.Add(p);
			}
			LordMaker.MakeNewLord(faction, new LordJob_MarketShoppers(marker, Rand.RangeInclusive(MinStay, MaxStay)), map, group);
			return faction.Name;
		}
	}
}
