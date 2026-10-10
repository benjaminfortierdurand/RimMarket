using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public static class CaravanCamp
	{
		public const float SpotRadius = 6f;

		private static readonly FieldInfo ChillSpotField = AccessTools.Field(typeof(LordJob_TradeWithColony), "chillSpot");

		public static Thing CampBuilding(Map map, Pawn from) => NearestPlayerThing(map, RimMarketDefOf.RimMarket_CaravanCamp, from);

		public static Thing RestSpot(Map map, Pawn from) => NearestPlayerThing(map, RimMarketDefOf.RimMarket_GuardRestSpot, from);

		private const float GateSlack = 3f;

		public static bool GateOfCamp(Thing door)
		{
			Map map = door?.Map;
			if (map == null) return false;
			List<Thing> camps = map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_CaravanCamp);
			for (int i = 0; i < camps.Count; i++)
			{
				Thing camp = camps[i];
				if (!camp.Spawned || camp.Faction != Faction.OfPlayer) continue;
				CompMarketQuarter q = camp.TryGetComp<CompMarketQuarter>();
				if (q == null) continue;
				if (!q.HasRegion && door.Position.DistanceTo(camp.Position) <= q.Radius + GateSlack) return true;
				if (q.InQuarter(door.Position)) return true;
				for (int k = 0; k < GenAdj.AdjacentCells.Length; k++)
				{
					if (q.InQuarter(door.Position + GenAdj.AdjacentCells[k])) return true;
				}
			}
			return false;
		}

		public static PawnDuty GuardDuty(Thing rest, Thing camp, IntVec3 spot)
		{
			if (rest != null)
			{
				float radius = rest.TryGetComp<CompMarketQuarter>()?.Radius ?? SpotRadius;
				return new PawnDuty(RimMarketDefOf.RimMarket_RestAtSpotDuty, rest.Position, rest) { wanderRadius = radius, locomotion = LocomotionUrgency.Walk };
			}
			if (camp != null)
			{
				float radius = camp.TryGetComp<CompMarketQuarter>()?.Radius ?? SpotRadius;
				return new PawnDuty(RimMarketDefOf.RimMarket_WaitNearCamp, camp.Position, camp) { wanderRadius = radius, locomotion = LocomotionUrgency.Walk };
			}
			return Duty(null, spot);
		}

		public static Thing SpotOf(PawnDuty d)
		{
			if (d == null) return null;
			return d.focusSecond.Thing ?? d.focus.Thing;
		}

		public static bool GuardDutyStale(PawnDuty d, Thing rest, Thing camp)
		{
			if (d == null) return false;
			if (d.def == RimMarketDefOf.RimMarket_RestAtSpotDuty) return rest == null || d.focus.HasThing || SpotOf(d) != rest;
			if (d.def == RimMarketDefOf.RimMarket_WaitNearCamp) return rest != null || camp == null || d.focus.HasThing || SpotOf(d) != camp;
			return false;
		}

		private static Thing NearestPlayerThing(Map map, ThingDef def, Pawn from)
		{
			if (map == null || def == null) return null;
			List<Thing> camps = map.listerThings.ThingsOfDef(def);
			Thing best = null;
			float bestDist = float.MaxValue;
			for (int i = 0; i < camps.Count; i++)
			{
				Thing c = camps[i];
				if (!c.Spawned || c.Faction != Faction.OfPlayer) continue;
				if (from != null && from.Spawned && !from.CanReach(c, PathEndMode.Touch, Danger.Deadly)) continue;
				float d = from != null ? c.Position.DistanceToSquared(from.Position) : 0f;
				if (d < bestDist)
				{
					best = c;
					bestDist = d;
				}
			}
			return best;
		}

		public static IntVec3 ChillSpot(Lord lord)
		{
			if (lord?.LordJob is LordJob_TradeWithColony job && ChillSpotField?.GetValue(job) is IntVec3 spot && spot.IsValid && spot.InBounds(lord.Map))
				return spot;
			return IntVec3.Invalid;
		}

		private const float OutsideGap = 2f;
		private const float OutsideSearch = 40f;
		private const int OutsideAngles = 12;
		private const float OutsideAngleStep = 15f;

		public static IntVec3 WaitSpot(CompMarketStall stall, Lord lord, Pawn from)
		{
			IntVec3 outside = OutsideMarket(stall, lord, from);
			return outside.IsValid ? outside : FallbackSpot(lord, from);
		}

		public static IntVec3 OutsideMarket(CompMarketStall stall, Lord lord, Pawn from)
		{
			Building_MarketMarker marker = MarketUtility.MarkerFor(stall);
			CompMarketQuarter q = marker?.Quarter;
			Map map = stall?.Map;
			if (q == null || map == null) return IntVec3.Invalid;
			IntVec3 center = marker.Position;
			UnityEngine.Vector3 dir = AwayFrom(center, ChillSpot(lord), map);
			float start = q.HasRegion ? 1f : q.Radius + SpotRadius + OutsideGap;
			for (float dist = start; dist <= q.Radius + OutsideSearch; dist += 1f)
			{
				for (int k = 0; k <= OutsideAngles; k++)
				{
					float angle = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * OutsideAngleStep;
					UnityEngine.Vector3 v = UnityEngine.Quaternion.AngleAxis(angle, UnityEngine.Vector3.up) * dir * dist;
					IntVec3 c = center + new IntVec3(UnityEngine.Mathf.RoundToInt(v.x), 0, UnityEngine.Mathf.RoundToInt(v.z));
					if (FitsOutside(c, map, q, from)) return c;
				}
			}
			return IntVec3.Invalid;
		}

		private static UnityEngine.Vector3 AwayFrom(IntVec3 center, IntVec3 toward, Map map)
		{
			if (toward.IsValid && toward != center) return (toward - center).ToVector3().normalized;
			int left = center.x, right = map.Size.x - 1 - center.x, down = center.z, up = map.Size.z - 1 - center.z;
			int best = UnityEngine.Mathf.Min(UnityEngine.Mathf.Min(left, right), UnityEngine.Mathf.Min(down, up));
			if (best == left) return new UnityEngine.Vector3(-1f, 0f, 0f);
			if (best == right) return new UnityEngine.Vector3(1f, 0f, 0f);
			if (best == down) return new UnityEngine.Vector3(0f, 0f, -1f);
			return new UnityEngine.Vector3(0f, 0f, 1f);
		}

		private static bool FitsOutside(IntVec3 c, Map map, CompMarketQuarter q, Pawn from)
		{
			if (!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || q.InQuarter(c)) return false;
			if (q.HasRegion)
			{
				int count = GenRadial.NumCellsInRadius(SpotRadius);
				for (int i = 0; i < count; i++)
				{
					IntVec3 n = c + GenRadial.RadialPattern[i];
					if (n.InBounds(map) && q.InQuarter(n)) return false;
				}
			}
			return from == null || !from.Spawned || from.CanReach(c, PathEndMode.OnCell, Danger.Some);
		}

		public static IntVec3 FallbackSpot(Lord lord, Pawn from)
		{
			IntVec3 spot = ChillSpot(lord);
			if (spot.IsValid) return spot;
			if (from != null && from.Spawned && RCellFinder.TryFindRandomSpotJustOutsideColony(from, out IntVec3 cell)) return cell;
			return from != null && from.Spawned ? from.Position : IntVec3.Invalid;
		}

		public static PawnDuty Duty(Thing camp, IntVec3 spot)
		{
			if (camp != null)
			{
				float radius = camp.TryGetComp<CompMarketQuarter>()?.Radius ?? SpotRadius;
				return new PawnDuty(RimMarketDefOf.RimMarket_WaitAtCamp, camp.Position, camp) { wanderRadius = radius, locomotion = LocomotionUrgency.Walk };
			}
			return new PawnDuty(RimMarketDefOf.RimMarket_WaitAtCamp, spot) { wanderRadius = SpotRadius, locomotion = LocomotionUrgency.Walk };
		}

		public static bool DutyStale(PawnDuty d, Thing camp, CompMarketQuarter market, IntVec3 chill)
		{
			if (d == null || d.def != RimMarketDefOf.RimMarket_WaitAtCamp) return false;
			if (d.focus.HasThing) return true;
			if (camp != null) return SpotOf(d) != camp || d.focus.Cell != camp.Position;
			if (d.focusSecond.HasThing || !d.focus.IsValid) return true;
			return market != null && market.InQuarter(d.focus.Cell) && d.focus.Cell != chill;
		}

		public static List<IntVec3> PostCells(CompMarketStall stall)
		{
			var cells = new List<IntVec3>();
			Map map = stall.Map;
			if (map == null) return cells;
			IntVec3 right = stall.Side;
			IntVec3 front = stall.Front;
			IntVec3 merchant = stall.MerchantCell;
			int reach = stall.parent.def.size.x / 2 + 1;
			IntVec3[] candidates;
			if (!stall.Props.merchantAtSide)
			{
				candidates = new[]
				{
					stall.CustomerCell + right * reach,
					stall.CustomerCell - right * reach,
					stall.Position + right * reach,
					stall.Position - right * reach,
				};
			}
			else if (stall.parent.Rotation.IsHorizontal)
			{
				candidates = new[] { merchant - front, merchant + front, merchant + right };
			}
			else
			{
				IntVec3 far = stall.Position + (stall.Position - merchant);
				candidates = new[] { far, far + front, merchant + front };
			}
			for (int i = 0; i < candidates.Length; i++)
			{
				IntVec3 c = candidates[i];
				if (c == merchant || !c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null) continue;
				if (OnOtherStall(map, stall, c)) continue;
				cells.Add(c);
			}
			return cells;
		}

		private static bool OnOtherStall(Map map, CompMarketStall self, IntVec3 c)
		{
			List<ThingDef> defs = MarketStallDefs.All;
			for (int i = 0; i < defs.Count; i++)
			{
				List<Thing> things = map.listerThings.ThingsOfDef(defs[i]);
				for (int j = 0; j < things.Count; j++)
				{
					CompMarketStall other = MarketStallDefs.StallOf(things[j]);
					if (other == null || other == self || !other.Spawned) continue;
					if (c == other.MerchantCell || c == other.CustomerCell || other.CanvasCovers(c)) return true;
				}
			}
			return false;
		}
	}

	public class JobGiver_WanderInCamp : ThinkNode_JobGiver
	{
		public bool outsideZone;

		private const float RingWidth = 4f;

		public override ThinkNode DeepCopy(bool resolve = true)
		{
			var copy = (JobGiver_WanderInCamp)base.DeepCopy(resolve);
			copy.outsideZone = outsideZone;
			return copy;
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			PawnDuty duty = pawn.mindState?.duty;
			if (duty == null || !duty.focus.IsValid) return null;
			if (pawn.mindState.nextMoveOrderIsWait && InCamp(pawn, duty))
			{
				pawn.mindState.nextMoveOrderIsWait = false;
				Job wait = JobMaker.MakeJob(JobDefOf.Wait_Wander);
				wait.expiryInterval = Rand.Range(400, 1100);
				return wait;
			}
			IntVec3 dest = PickCell(pawn, duty);
			if (!dest.IsValid) return null;
			pawn.mindState.nextMoveOrderIsWait = true;
			Job job = JobMaker.MakeJob(JobDefOf.GotoWander, dest);
			job.locomotionUrgency = LocomotionUrgency.Walk;
			return job;
		}

		private bool InCamp(Pawn pawn, PawnDuty duty)
		{
			CompMarketQuarter q = CaravanCamp.SpotOf(duty)?.TryGetComp<CompMarketQuarter>();
			float r = duty.wanderRadius ?? CaravanCamp.SpotRadius;
			float d2 = pawn.Position.DistanceToSquared(duty.focus.Cell);
			if (outsideZone)
			{
				bool inZone = q != null && q.HasRegion ? q.InQuarter(pawn.Position) : d2 <= r * r;
				return !inZone && d2 <= (r + RingWidth + 1f) * (r + RingWidth + 1f);
			}
			if (q != null && q.HasRegion) return q.InQuarter(pawn.Position);
			return d2 <= (r + 1f) * (r + 1f);
		}

		private IntVec3 PickCell(Pawn pawn, PawnDuty duty)
		{
			Map map = pawn.Map;
			CompMarketQuarter q = CaravanCamp.SpotOf(duty)?.TryGetComp<CompMarketQuarter>();
			IntVec3 center = duty.focus.Cell;
			float radius = duty.wanderRadius ?? CaravanCamp.SpotRadius;
			float outer = outsideZone ? radius + RingWidth : radius;
			int count = GenRadial.NumCellsInRadius(UnityEngine.Mathf.Min(outer, GenRadial.MaxRadialPatternRadius - 1f));
			for (int i = 0; i < 30; i++)
			{
				IntVec3 c = center + GenRadial.RadialPattern[Rand.Range(0, count)];
				if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null) continue;
				bool inZone = q != null && q.HasRegion ? q.InQuarter(c) : c.DistanceToSquared(center) <= radius * radius;
				if (outsideZone == inZone) continue;
				if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
				return c;
			}
			return IntVec3.Invalid;
		}
	}

	public class JobGiver_RestAtSpot : ThinkNode_JobGiver
	{
		private const float SitWeight = 0.5f;
		private const float StrollWeight = 0.3f;
		private const int MinSit = 1200;
		private const int MaxSit = 3000;
		private const int MinLinger = 500;
		private const int MaxLinger = 1200;
		private const int TicksPerCell = 14;
		private const float LingerRadius = 3.9f;

		protected override Job TryGiveJob(Pawn pawn)
		{
			PawnDuty duty = pawn.mindState?.duty;
			if (!(CaravanCamp.SpotOf(duty) is Thing spot) || !spot.Spawned) return null;
			float roll = Rand.Value;
			if (roll >= SitWeight)
			{
				roll -= SitWeight;
				if (roll < StrollWeight) return null;
				if (TraderCaravanUtility.GetTraderCaravanRole(pawn) != TraderCaravanRole.Chattel)
				{
					Job check = AnimalCheck(pawn);
					if (check != null) return check;
				}
			}
			return SitJob(pawn, spot, duty);
		}

		private static Job AnimalCheck(Pawn pawn)
		{
			Lord lord = pawn.GetLord();
			if (lord == null) return null;
			var animals = new List<Pawn>();
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p != pawn && p.Spawned && p.Map == pawn.Map && !p.Dead && p.RaceProps.Animal) animals.Add(p);
			}
			if (!animals.TryRandomElement(out Pawn animal)) return null;
			IntVec3 cell = LingerCell(pawn, animal.Position);
			if (!cell.IsValid) return null;
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_CheckAnimals, animal, cell);
			job.expiryInterval = WalkTicks(pawn, cell) + Rand.Range(MinLinger, MaxLinger);
			return job;
		}

		private static IntVec3 LingerCell(Pawn pawn, IntVec3 center)
		{
			Map map = pawn.Map;
			int count = GenRadial.NumCellsInRadius(LingerRadius);
			int start = Rand.Range(0, count);
			for (int k = 0; k < count; k++)
			{
				IntVec3 c = center + GenRadial.RadialPattern[(start + k) % count];
				if (c == center || !c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null) continue;
				if (!pawn.CanReserveSittableOrSpot(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
				return c;
			}
			return IntVec3.Invalid;
		}

		private static int WalkTicks(Pawn pawn, IntVec3 cell)
		{
			return (int)(pawn.Position.DistanceTo(cell) * TicksPerCell);
		}

		private static Job SitJob(Pawn pawn, Thing spot, PawnDuty duty)
		{
			Map map = pawn.Map;
			CompMarketQuarter q = spot.TryGetComp<CompMarketQuarter>();
			float radius = duty.wanderRadius ?? CaravanCamp.SpotRadius;
			int count = GenRadial.NumCellsInRadius(UnityEngine.Mathf.Min(radius, GenRadial.MaxRadialPatternRadius - 1f));
			IntVec3 seat = IntVec3.Invalid;
			Building chair = null;
			IntVec3 stand = IntVec3.Invalid;
			int start = Rand.Range(0, count);
			for (int k = 0; k < count; k++)
			{
				IntVec3 c = spot.Position + GenRadial.RadialPattern[(start + k) % count];
				if (!c.InBounds(map)) continue;
				if (q != null && !q.InQuarter(c)) continue;
				Building b = c.GetEdifice(map);
				bool sittable = b != null && b.def.building != null && b.def.building.isSittable;
				if (!sittable && (!c.Standable(map) || b != null)) continue;
				if (!pawn.CanReserveSittableOrSpot(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
				if (sittable)
				{
					seat = c;
					chair = b;
					break;
				}
				if (!stand.IsValid) stand = c;
			}
			IntVec3 cell = seat.IsValid ? seat : stand;
			if (!cell.IsValid) return null;
			Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_RestAtSpot, spot, cell, chair);
			job.expiryInterval = WalkTicks(pawn, cell) + Rand.Range(MinSit, MaxSit);
			return job;
		}
	}

	public class JobDriver_RestAtSpot : RimMarket.Auction.JobDriver_AttendAuction
	{
	}
}
