using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Auction
{
	public class LordJob_AuctionBuyers : LordJob
	{
		public const string MemoDone = "RimMarketAuctionDone";
		private const int TimeoutTicks = 40000;

		public Building_AuctionBlock block;

		public LordJob_AuctionBuyers()
		{
		}

		public LordJob_AuctionBuyers(Building_AuctionBlock block)
		{
			this.block = block;
		}

		public override bool AddFleeToil => false;

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();
			var attend = new LordToil_AttendAuction(block);
			graph.StartingToil = attend;
			var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: false, interruptCurrentJob: true) { useAvoidGrid = true };
			graph.AddToil(leave);
			var done = new Transition(attend, leave);
			done.AddTrigger(new Trigger_Memo(MemoDone));
			done.AddTrigger(new Trigger_TicksPassed(TimeoutTicks));
			done.AddTrigger(new Trigger_BecamePlayerEnemy());
			done.AddTrigger(new Trigger_PawnHarmed());
			done.AddPreAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(done);
			return graph;
		}

		public override void ExposeData()
		{
			Scribe_References.Look(ref block, "block");
		}
	}

	public class LordToil_AttendAuction : LordToil
	{
		private readonly Building_AuctionBlock block;

		public LordToil_AttendAuction(Building_AuctionBlock block)
		{
			this.block = block;
		}

		public override void UpdateAllDuties()
		{
			Pawn buyer = AuctionBuyers.BuyerOf(lord);
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (AuctionBuyers.IsBought(p) && buyer != null)
					p.mindState.duty = new PawnDuty(DutyDefOf.Escort, buyer, 3f);
				else
					p.mindState.duty = new PawnDuty(RimMarketDefOf.RimMarket_AttendAuctionDuty, block);
			}
		}
	}

	public class JobGiver_AttendAuction : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!(pawn.mindState?.duty?.focus.Thing is Building_AuctionBlock block) || !block.Spawned) return null;
			if (!WatchBuildingUtility.TryFindBestWatchCell(block, pawn, true, out IntVec3 cell, out Building chair)) return null;
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_AttendAuction, block, cell, chair);
			job.expiryInterval = 6000;
			return job;
		}
	}

	public class JobDriver_AttendAuction : JobDriver
	{
		private const int SitTicks = 5000;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.ReserveSittableOrSpot(job.targetB.Cell, job, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.EndOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
			Toil watch = ToilMaker.MakeToil("AttendAuction");
			watch.AddPreTickIntervalAction(delta =>
			{
				pawn.rotationTracker.FaceCell(TargetA.Cell);
				pawn.GainComfortFromCellIfPossible(delta);
			});
			watch.handlingFacing = true;
			watch.defaultCompleteMode = ToilCompleteMode.Delay;
			watch.defaultDuration = SitTicks;
			watch.socialMode = RandomSocialMode.SuperActive;
			yield return watch;
		}
	}

	public static class AuctionBuyers
	{
		private const float MinPurseFactor = 1.1f;
		private const float MaxPurseFactor = 2.4f;

		public static bool IsBought(Pawn p)
		{
			return p?.kindDef == PawnKindDefOf.Slave;
		}

		public static Pawn BuyerOf(Lord lord)
		{
			if (lord == null) return null;
			Pawn best = null;
			int bestSilver = -1;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (IsBought(p) || p.Dead) continue;
				int silver = Market.MarketUtility.SilverOf(p);
				if (silver > bestSilver)
				{
					bestSilver = silver;
					best = p;
				}
			}
			return best;
		}

		public static IEnumerable<Lord> LordsFor(Building_AuctionBlock block)
		{
			if (block?.Map == null) yield break;
			List<Lord> lords = block.Map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].LordJob is LordJob_AuctionBuyers job && job.block == block) yield return lords[i];
			}
		}

		public static List<string> Send(Map map, Building_AuctionBlock block, Faction seller, float lotsValue)
		{
			var sent = new List<string>();
			if (map == null || block == null) return sent;
			List<Faction> factions = Find.FactionManager.AllFactionsVisible
				.Where(f => f != seller && AuctionUtility.FactionBuysSlaves(f) && RimMarket.Market.MarketTruce.Welcome(map, f)).ToList();
			factions.Shuffle();
			int count = Mathf.Min(factions.Count, Rand.RangeInclusive(1, 3));
			for (int i = 0; i < count; i++)
			{
				Faction faction = factions[i];
				if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Friendly, false,
					c => map.reachability.CanReach(c, block, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)))) continue;
				PawnKindDef kind = Market.MarketUtility.CivilianKind(faction) ?? PawnKindDefOf.Villager;
				var group = new List<Pawn>();
				int size = Rand.RangeInclusive(1, 2);
				for (int k = 0; k < size; k++)
				{
					Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
					GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
					group.Add(p);
				}
				Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
				silver.stackCount = Mathf.Max(100, Mathf.RoundToInt(lotsValue * Rand.Range(MinPurseFactor, MaxPurseFactor)));
				group[0].inventory.innerContainer.TryAdd(silver);
				LordMaker.MakeNewLord(faction, new LordJob_AuctionBuyers(block), map, group);
				sent.Add(faction.Name);
			}
			return sent;
		}

		public static void Dismiss(Building_AuctionBlock block)
		{
			foreach (Lord lord in LordsFor(block).ToList()) lord.ReceiveMemo(LordJob_AuctionBuyers.MemoDone);
		}

		public static void HandOver(Pawn slave, Pawn buyer)
		{
			if (slave == null || buyer == null || !slave.Spawned || slave.Dead) return;
			slave.GetLord()?.RemovePawn(slave);
			if (slave.Faction != buyer.Faction) slave.SetFaction(buyer.Faction);
			Lord lord = buyer.GetLord();
			if (lord != null && lord.LordJob is LordJob_AuctionBuyers)
			{
				lord.AddPawn(slave);
				return;
			}
			LordMaker.MakeNewLord(buyer.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk), slave.Map, new List<Pawn> { slave });
		}
	}
}
