using System.Collections.Generic;
using System.Linq;
using RimMarket.AI;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Thieves
{
	public static class ThiefUtility
	{
		public const float DeterRadius = 14f;
		public const float NeighborRadius = 15f;
		private const float BaseSnatchChance = 0.9f;
		private const float CaravanGuardFactor = 0.8f;
		private const float ColonistFactor = 0.6f;
		private const float LootValueTarget = 250f;
		private const float MaxLootMass = 10f;
		private const float ChaseRange = 60f;

		public static Faction PickThiefFaction(Map map)
		{
			return Find.FactionManager.AllFactionsVisible.Where(f =>
				!f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && !f.HostileTo(Faction.OfPlayer)
				&& MarketTruce.Welcome(map, f)
				&& f.def.humanlikeFaction
				&& f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Peaceful))
				.RandomElementWithFallback();
		}

		private static PawnKindDef PickThiefKind(Faction faction)
		{
			var options = new List<PawnGenOption>();
			foreach (PawnGroupMaker maker in faction.def.pawnGroupMakers)
			{
				if (maker.kindDef != PawnGroupKindDefOf.Peaceful || maker.options == null) continue;
				foreach (PawnGenOption o in maker.options)
				{
					if (o?.kind != null && !o.kind.trader && o.kind.RaceProps.Humanlike) options.Add(o);
				}
			}
			if (options.TryRandomElementByWeight(o => o.selectionWeight, out PawnGenOption pick)) return pick.kind;
			return MarketUtility.CivilianKind(faction);
		}

		public static bool TrySendThief(Map map, int loiterTicks, bool bold = false)
		{
			if (map == null) return false;
			Faction faction = PickThiefFaction(map);
			if (faction == null) return false;
			PawnKindDef kind = PickThiefKind(faction);
			if (kind == null) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 cell, map, CellFinder.EdgeRoadChance_Neutral)) return false;
			Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
			pawn.equipment?.DestroyAllEquipment();
			GenSpawn.Spawn(pawn, cell, map);
			LordMaker.MakeNewLord(faction, new LordJob_MarketThief(loiterTicks, bold), map, new List<Pawn> { pawn });
			return true;
		}

		public static bool AnyThiefOnMap(Map map)
		{
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].LordJob is LordJob_MarketThief) return true;
			}
			return false;
		}

		public static bool IsCaravanGuard(Pawn p)
		{
			return p != null && p.RaceProps.Humanlike && p.GetLord()?.LordJob is LordJob_TradeWithColony
				&& p.GetTraderCaravanRole() == TraderCaravanRole.Guard;
		}

		public static bool IsCulprit(Pawn p)
		{
			return p != null && p.guilt != null && p.guilt.IsGuilty && p.GetLord()?.LordJob is LordJob_MarketThief;
		}

		public static bool CaughtThief(Pawn p)
		{
			if (IsCulprit(p)) return true;
			if (MarketManager.Instance?.WasThief(p) != true) return false;
			return p.IsPrisonerOfColony || p.Downed || (p.guilt != null && p.guilt.IsGuilty);
		}

		public static void OrderCapture(Pawn capturer, Pawn thief)
		{
			Building_Bed bed = RestUtility.FindBedFor(thief, capturer, false, false, GuestStatus.Prisoner)
				?? RestUtility.FindBedFor(thief, capturer, false, true, GuestStatus.Prisoner);
			if (bed == null)
			{
				Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), thief, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Job job = JobMaker.MakeJob(JobDefOf.Capture, thief, bed);
			job.count = 1;
			capturer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
		}

		private static bool Watching(Pawn p)
		{
			return p.Spawned && !p.Dead && !p.Downed && p.Awake() && !p.InMentalState;
		}

		public static void CountWatchers(CompMarketStall stall, float radius, out int caravanGuards, out int colonists)
		{
			caravanGuards = 0;
			colonists = 0;
			if (stall == null || !stall.Spawned) return;
			Map map = stall.Map;
			foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
			{
				if (p == null || !p.RaceProps.Humanlike || !Watching(p)) continue;
				bool colonist = p.IsColonist;
				bool guard = !colonist && IsCaravanGuard(p);
				if (!colonist && !guard) continue;
				if (!p.Position.InHorDistOf(stall.Position, radius)) continue;
				if (!GenSight.LineOfSight(p.Position, stall.Position, map, skipFirstCell: true)) continue;
				if (colonist) colonists++;
				else caravanGuards++;
			}
		}

		public static float SnatchChance(CompMarketStall stall)
		{
			CountWatchers(stall, DeterRadius, out int guards, out int colonists);
			return BaseSnatchChance * Mathf.Pow(CaravanGuardFactor, guards) * Mathf.Pow(ColonistFactor, colonists);
		}

		public static StallVisit PickTarget(Pawn thief)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || thief?.Map == null) return null;
			StallVisit best = null;
			float bestChance = -1f;
			IReadOnlyList<StallVisit> visits = mgr.Visits;
			foreach (StallVisit v in visits.InRandomOrder())
			{
				if (v.map != thief.Map || !MarketUtility.MerchantAtPost(v)) continue;
				if (!thief.CanReach(v.stall.CustomerCell, PathEndMode.OnCell, Danger.Some)) continue;
				float chance = SnatchChance(v.stall);
				if (chance > bestChance)
				{
					bestChance = chance;
					best = v;
				}
			}
			return best;
		}

		public static bool AnyTargetOnMap(Map map)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || map == null) return false;
			IReadOnlyList<StallVisit> visits = mgr.Visits;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].map == map && visits[i].StallOk && !visits[i].closed) return true;
			}
			return false;
		}

		public static IntVec3 MarketFocus(Map map)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || map == null) return IntVec3.Invalid;
			var cells = new List<IntVec3>();
			IReadOnlyList<StallVisit> visits = mgr.Visits;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].map == map && visits[i].StallOk) cells.Add(visits[i].stall.CustomerCell);
			}
			return cells.Count == 0 ? IntVec3.Invalid : cells.RandomElement();
		}

		private static Thing PickLoot(StallVisit v, Pawn thief)
		{
			Pawn_TraderTracker tracker = v.trader?.trader;
			if (tracker == null) return null;
			var candidates = new List<Thing>();
			foreach (Thing t in tracker.Goods)
			{
				if (t == null || t is Pawn || t is MinifiedThing || t.holdingOwner == null) continue;
				if (t.def.category != ThingCategory.Item || !t.def.EverHaulable) continue;
				if (!MarketUtility.Tradeable(t) || t.GetStatValue(StatDefOf.Mass) > MaxLootMass) continue;
				if (thief.carryTracker.AvailableStackSpace(t.def) < 1) continue;
				candidates.Add(t);
			}
			if (candidates.Count == 0) return null;
			return candidates.RandomElementByWeight(t => Mathf.Min(t.MarketValue * t.stackCount, 1000f) + 1f);
		}

		public static bool TrySnatch(Pawn thief, CompMarketStall stall)
		{
			MarketManager mgr = MarketManager.Instance;
			StallVisit v = mgr?.VisitFor(stall);
			if (v == null || !MarketUtility.MerchantAtPost(v)) return false;
			bool bold = thief.GetLord()?.LordJob is LordJob_MarketThief lj && lj.Bold;
			if (!bold && !Rand.Chance(SnatchChance(stall)))
			{
				mgr.Notify_Deterred(thief, v);
				return false;
			}
			Thing loot = PickLoot(v, thief);
			if (loot == null)
			{
				thief.GetLord()?.ReceiveMemo(LordJob_MarketThief.MemoLeave);
				return false;
			}
			float unit = Mathf.Max(1f, loot.MarketValue);
			int count = Mathf.Clamp(Mathf.FloorToInt(LootValueTarget / unit), 1, loot.stackCount);
			count = Mathf.Min(count, thief.carryTracker.AvailableStackSpace(loot.def));
			if (count < 1) return false;
			ThingOwner owner = loot.holdingOwner;
			Thing taken = owner.Take(loot, count);
			if (taken == null) return false;
			if (!thief.carryTracker.TryStartCarry(taken))
			{
				owner.TryAdd(taken);
				return false;
			}
			mgr.Notify_Snatched(thief, taken, v);
			return true;
		}

		public static bool IsFleeingThief(Pawn p)
		{
			MarketManager mgr = MarketManager.Instance;
			return mgr != null && p != null && p.Spawned && !p.Dead && !p.Downed && mgr.TheftBy(p) != null;
		}

		public static Pawn ThiefToChase(Pawn guard, int ownVisitId)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || !MarketManager.AnyThefts || guard?.Map == null) return null;
			Pawn best = null;
			float bestDist = float.MaxValue;
			IReadOnlyList<MarketTheft> thefts = mgr.Thefts;
			for (int i = 0; i < thefts.Count; i++)
			{
				MarketTheft theft = thefts[i];
				Pawn t = theft.thief;
				if (t == null || t.Map != guard.Map || !IsFleeingThief(t)) continue;
				float d = t.Position.DistanceToSquared(guard.Position);
				bool ownStall = theft.visitId == ownVisitId;
				if (!ownStall && d > NeighborRadius * NeighborRadius) continue;
				if (d > ChaseRange * ChaseRange || d >= bestDist) continue;
				if (!guard.CanReach(t, PathEndMode.Touch, Danger.Some)) continue;
				bestDist = d;
				best = t;
			}
			return best;
		}

		public static void TryTackle(Pawn guard, Pawn thief)
		{
			if (guard == null || thief == null || !IsFleeingThief(thief)) return;
			int guardMelee = guard.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
			int thiefMelee = thief.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
			float chance = Mathf.Clamp(0.55f + 0.05f * (guardMelee - thiefMelee), 0.2f, 0.95f);
			guard.skills?.Learn(SkillDefOf.Melee, 150f);
			Map map = thief.Map;
			if (Rand.Chance(chance))
			{
				MoteMaker.ThrowText(thief.DrawPos, map, "RimMarket.MoteGotYou".Translate(), 3.5f);
				thief.health.AddHediff(RimMarketDefOf.RimMarket_Subdued);
				MarketManager.Instance?.Notify_ThiefCaught(thief, guard);
				return;
			}
			MoteMaker.ThrowText(thief.DrawPos, map, "RimMarket.MoteSlipped".Translate(), 3.5f);
			guard.stances?.stunner?.StunFor(120, thief, addBattleLog: false, showMote: false);
		}

		public static Pawn CarrierOf(StallVisit v)
		{
			if (v == null || !v.LordAlive) return null;
			Pawn fallback = null;
			List<Pawn> pawns = v.lord.ownedPawns;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == null || p.Dead || p.inventory == null) continue;
				TraderCaravanRole role = p.GetTraderCaravanRole();
				if (role == TraderCaravanRole.Carrier) return p;
				if (role == TraderCaravanRole.Trader) fallback = p;
			}
			return fallback;
		}

		public static bool ReturnLoot(MarketTheft t, StallVisit v)
		{
			Thing loot = t?.loot;
			if (loot == null || loot.Destroyed) return false;
			Pawn carrier = CarrierOf(v);
			Map map = t.map;
			Thing anchor = t.thief?.SpawnedParentOrMe;
			Vector3 at = anchor != null && anchor.Spawned ? anchor.DrawPos : (loot.Spawned ? loot.DrawPos : Vector3.zero);
			if (carrier == null)
			{
				if (!loot.Spawned && loot.holdingOwner != null && anchor != null && anchor.Spawned)
					loot.holdingOwner.TryDrop(loot, anchor.Position, anchor.Map, ThingPlaceMode.Near, out Thing _);
				return false;
			}
			if (loot.Spawned)
			{
				at = loot.DrawPos;
				loot.DeSpawn();
				if (!carrier.inventory.innerContainer.TryAdd(loot) && map != null)
					GenPlace.TryPlaceThing(loot, carrier.Position, map, ThingPlaceMode.Near);
			}
			else if (loot.holdingOwner != null)
			{
				loot.holdingOwner.TryTransferToContainer(loot, carrier.inventory.innerContainer, loot.stackCount);
			}
			else
			{
				return false;
			}
			if (map != null && at != Vector3.zero) MoteMaker.ThrowText(at, map, "RimMarket.MoteReturned".Translate(), 3.5f);
			v?.stall?.MarkDisplayDirty();
			return true;
		}
	}
}
