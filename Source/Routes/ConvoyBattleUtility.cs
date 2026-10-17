using System;
using System.Collections.Generic;
using System.Linq;
using RimMarket.AI;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Routes
{
	public static class ConvoyBattleUtility
	{
		public const int MapSize = 120;
		private const int SpotTries = 80;
		private const float SpotRadius = 22f;
		private const float RingRadius = 8f;
		private const float GapAngle = 14f;
		private const float InnerRadius = 3f;
		private const float EscortPoints = 1.5f;
		private const float AttackFactor = 1.4f;
		private const float FarFromEnemies = 0.6f;
		private const float ArriveMin = 18f;
		private const int HoldOutMin = 60000;
		private const int HoldOutMax = 180000;
		private const int HoldOutSlack = 20000;
		private const float HoldOutTravelFactor = 1.3f;
		private const float ArriveMax = 26f;
		private const int SavedGoodwill = 10;
		private const float SavedSafety = 3f;
		private const float ArcherShare = 0.6f;
		private const float MinBowRange = 18f;

		private static List<Pawn> replay;
		private static bool replayUsed;
		private static readonly List<Pawn> replayed = new List<Pawn>();

		public static TraderKindDef KindOf(TradeConvoy convoy)
		{
			return TradeRouteUtility.RouteOf(convoy)?.kind ?? Orders.SpecialOrderUtility.OrderOf(convoy)?.kind;
		}

		public static FloatMenuAcceptanceReport CanHelp(TradeConvoy convoy)
		{
			if (convoy == null || convoy.Destroyed || !convoy.UnderAttack || convoy.Faction == null) return false;
			if (convoy.battle != null) return convoy.battle.HasMap && !convoy.battle.resolved;
			if (convoy.Faction.HostileTo(Faction.OfPlayer) || KindOf(convoy) == null) return false;
			Faction enemy = convoy.attacker;
			if (enemy == null || enemy.defeated || !CanFight(enemy)) return false;
			if (!enemy.HostileTo(Faction.OfPlayer)) return FloatMenuAcceptanceReport.WithFailReason("RimMarket.HelpNotAtWar".Translate(enemy.Name));
			if (Find.WorldObjects.AnyMapParentAt(convoy.Tile) || Current.Game.FindMap(convoy.Tile) != null)
				return FloatMenuAcceptanceReport.WithFailReason("RimMarket.HelpNoRoom".Translate());
			return true;
		}

		public static int HoldOutTicks(TradeConvoy convoy)
		{
			MapParent home = TradeRouteUtility.HomeOf(convoy);
			int travel = 0;
			if (home != null && TradeRouteUtility.TryPath(home.Tile, convoy.Tile, out _, out int ticks)) travel = ticks;
			int hold = Mathf.RoundToInt(travel * HoldOutTravelFactor) + HoldOutSlack;
			return Mathf.Clamp(Mathf.RoundToInt(hold * Rand.Range(0.9f, 1.1f)), HoldOutMin, HoldOutMax);
		}

		private static bool CanFight(Faction f)
		{
			List<PawnGroupMaker> makers = f.def.pawnGroupMakers;
			if (makers == null) return false;
			for (int i = 0; i < makers.Count; i++)
			{
				if (makers[i].kindDef == PawnGroupKindDefOf.Combat) return true;
			}
			return false;
		}

		public static void Arrive(TradeConvoy convoy, Caravan caravan)
		{
			if (convoy == null || caravan == null) return;
			ConvoyBattle battle = convoy.battle;
			if (battle != null && battle.HasMap)
			{
				EnterNearCamp(caravan, battle.Map, battle.enemyEntry, battle.campCenter);
				return;
			}
			LongEventHandler.QueueLongEvent(() => Start(convoy, caravan), "GeneratingMapForNewEncounter", false, null);
		}

		private static void Start(TradeConvoy convoy, Caravan caravan)
		{
			if (caravan == null || caravan.Destroyed || !CanHelp(convoy).Accepted) return;
			Faction faction = convoy.Faction;
			Faction enemy = convoy.attacker;
			float points = TraderCaravanUtility.GenerateGuardPoints() * (convoy.escort ? EscortPoints : 1f);
			List<Pawn> traders = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Trader,
				faction = faction,
				traderKind = KindOf(convoy),
				tile = convoy.Tile,
				points = Mathf.Max(points, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Trader)),
			}, warnOnZeroResults: false).ToList();
			List<Pawn> raiders = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Combat,
				faction = enemy,
				tile = convoy.Tile,
				points = Mathf.Max(points * AttackFactor, enemy.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)),
			}, warnOnZeroResults: false).ToList();
			ArmGuards(traders, faction.def.techLevel);
			Pawn trader = traders.FirstOrDefault(p => p.TraderKind != null);
			if (trader == null || raiders.Count == 0)
			{
				Discard(traders);
				Discard(raiders);
				Messages.Message("RimMarket.HelpFailed".Translate(), caravan, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Map map = GetOrGenerateMapUtility.GetOrGenerateMap(convoy.Tile, new IntVec3(MapSize, 1, MapSize), RimMarketDefOf.RimMarket_ConvoyBattle);
			if (!(map?.Parent is ConvoyBattle battle))
			{
				Discard(traders);
				Discard(raiders);
				return;
			}
			map.retainedCaravanData.Notify_GeneratedTempIncidentMapFor(caravan);
			IntVec3 center = BuildCamp(map, faction, out float radius);
			battle.convoy = convoy;
			battle.trader = trader;
			battle.convoyPawns.AddRange(traders);
			battle.campCenter = center;
			convoy.battle = battle;
			for (int i = 0; i < traders.Count; i++)
			{
				Pawn p = traders[i];
				bool guard = p.GetTraderCaravanRole() == TraderCaravanRole.Guard;
				GenSpawn.Spawn(p, guard ? RingCell(map, center, radius) : CellNear(map, center, InnerRadius), map, Rot4.Random);
				if (p.needs?.food != null) p.needs.food.CurLevel = p.needs.food.MaxLevel;
			}
			LordMaker.MakeNewLord(faction, new LordJob_ConvoyCamp(center, radius - 1.5f), map, traders);
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Hostile)) entry = CellFinder.RandomEdgeCell(map);
			battle.enemyEntry = entry;
			for (int i = 0; i < raiders.Count; i++)
				GenSpawn.Spawn(raiders[i], CellFinder.RandomClosewalkCellNear(entry, map, 8), map, Rot4.Random);
			LordMaker.MakeNewLord(enemy, new LordJob_AssaultColony(enemy, canKidnap: true, canTimeoutOrFlee: true, sappers: false, useAvoidGridSmart: false, canSteal: false), map, raiders);
			string label = caravan.Label;
			EnterNearCamp(caravan, map, entry, center);
			Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
			CameraJumper.TryJump(center, map);
			Find.LetterStack.ReceiveLetter("RimMarket.ConvoyBattleLabel".Translate(),
				"RimMarket.ConvoyBattleText".Translate(label, TradeRouteUtility.FromName(convoy), enemy.Name, trader.LabelShort),
				LetterDefOf.ThreatBig, new LookTargets(trader));
		}

		private static void ArmGuards(List<Pawn> pawns, TechLevel tech)
		{
			var guards = pawns.Where(p => p.GetTraderCaravanRole() == TraderCaravanRole.Guard && p.equipment != null && p.RaceProps.Humanlike).ToList();
			int want = Mathf.CeilToInt(guards.Count * ArcherShare);
			int have = guards.Count(p => p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon);
			if (have >= want) return;
			List<ThingDef> bows = RangedFor(tech);
			if (bows.Count == 0) return;
			guards.Shuffle();
			for (int i = 0; i < guards.Count && have < want; i++)
			{
				Pawn p = guards[i];
				if (p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) continue;
				if (p.WorkTagIsDisabled(WorkTags.Violent) || (p.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false)) continue;
				ThingDef def = bows.RandomElementByWeight(d => d.generateCommonality);
				var bow = (ThingWithComps)ThingMaker.MakeThing(def, GenStuff.RandomStuffFor(def));
				bow.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(), ArtGenerationContext.Outsider);
				p.equipment.DestroyAllEquipment();
				p.equipment.AddEquipment(bow);
				have++;
			}
		}

		private static List<ThingDef> RangedFor(TechLevel tech)
		{
			TechLevel min = tech == TechLevel.Medieval ? TechLevel.Neolithic : tech == TechLevel.Spacer ? TechLevel.Industrial : tech;
			return DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsRangedWeapon && d.equipmentType == EquipmentType.Primary
				&& d.techLevel >= min && d.techLevel <= tech && !d.destroyOnDrop && d.generateCommonality > 0f
				&& !d.weaponTags.NullOrEmpty() && !d.weaponTags.Any(t => t.Contains("Grenade"))
				&& !d.Verbs.NullOrEmpty() && d.Verbs[0].range >= MinBowRange && !d.Verbs[0].ai_IsBuildingDestroyer).ToList();
		}

		private static void Discard(List<Pawn> pawns)
		{
			for (int i = 0; i < pawns.Count; i++)
			{
				if (!pawns[i].Spawned && !pawns[i].Discarded) Find.WorldPawns.PassToWorld(pawns[i], PawnDiscardDecideMode.Discard);
			}
		}

		private static void EnterNearCamp(Caravan caravan, Map map, IntVec3 enemy, IntVec3 center)
		{
			Vector3 away = (center - enemy).ToVector3();
			away.y = 0f;
			if (away.sqrMagnitude < 1f) away = Vector3.forward;
			away.Normalize();
			for (int i = 0; i < 30; i++)
			{
				float angle = Rand.Range(-50f, 50f);
				IntVec3 c = center + (Quaternion.AngleAxis(angle, Vector3.up) * away * Rand.Range(ArriveMin, ArriveMax)).ToIntVec3();
				if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) continue;
				if (!map.reachability.CanReach(c, center, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))) continue;
				CaravanEnterMapUtility.Enter(caravan, map, p => CellFinder.RandomSpawnCellForPawnNear(c, map), CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
				return;
			}
			Enter(caravan, map, enemy, center);
		}

		private static void Enter(Caravan caravan, Map map, IntVec3 enemy, IntVec3 center)
		{
			float far = MapSize * FarFromEnemies;
			if (TryEdge(map, center, c => !enemy.IsValid || c.DistanceTo(enemy) >= far, out IntVec3 spot) || TryEdge(map, center, null, out spot))
				CaravanEnterMapUtility.Enter(caravan, map, p => CellFinder.RandomSpawnCellForPawnNear(spot, map), CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
			else
				CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
		}

		private static bool TryEdge(Map map, IntVec3 center, Predicate<IntVec3> extra, out IntVec3 spot)
		{
			return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && (extra == null || extra(c))
				&& (!center.IsValid || map.reachability.CanReach(c, center, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))),
				map, CellFinder.EdgeRoadChance_Ignore, out spot);
		}

		private static IntVec3 CellNear(Map map, IntVec3 center, float radius)
		{
			int r = Mathf.CeilToInt(radius);
			if (CellFinder.TryFindRandomCellNear(center, map, r, c => c.Standable(map) && c.DistanceTo(center) <= radius, out IntVec3 cell)) return cell;
			return CellFinder.RandomClosewalkCellNear(center, map, r);
		}

		private static IntVec3 RingCell(Map map, IntVec3 center, float radius)
		{
			int r = Mathf.CeilToInt(radius);
			if (CellFinder.TryFindRandomCellNear(center, map, r, c => c.Standable(map) && c.DistanceTo(center) >= radius - 4f && c.DistanceTo(center) <= radius - 1.5f, out IntVec3 cell)) return cell;
			return CellNear(map, center, radius - 1.5f);
		}

		private static IntVec3 BuildCamp(Map map, Faction faction, out float radius)
		{
			PrefabDef prefab = PickPrefab(faction);
			int half = prefab != null ? Mathf.Max(prefab.size.x, prefab.size.z) / 2 + 1 : Mathf.CeilToInt(RingRadius) + 1;
			IntVec3 center = FindSpot(map, half);
			Clear(map, center, half);
			if (prefab != null)
			{
				radius = Mathf.Min(prefab.size.x, prefab.size.z) / 2f;
				PrefabUtility.SpawnPrefab(prefab, map, center, Rot4.Random, faction, null, null, Prepare);
			}
			else
			{
				radius = RingRadius;
				BuildRing(map, center, faction);
			}
			FloodFillerFog.FloodUnfog(center, map);
			return center;
		}

		private static PrefabDef PickPrefab(Faction faction)
		{
			TechLevel tech = faction.def.techLevel;
			var fits = new List<PrefabDef>();
			int best = int.MinValue;
			List<PrefabDef> all = DefDatabase<PrefabDef>.AllDefsListForReading;
			for (int i = 0; i < all.Count; i++)
			{
				ConvoyCampExtension ext = all[i].GetModExtension<ConvoyCampExtension>();
				if (ext == null || !ext.Fits(tech) || ext.priority < best) continue;
				if (ext.priority > best)
				{
					best = ext.priority;
					fits.Clear();
				}
				fits.Add(all[i]);
			}
			return fits.RandomElementWithFallback();
		}

		private static void Prepare(Thing t)
		{
			if (t.TryGetComp(out CompRefuelable fuel)) fuel.Refuel(fuel.Props.fuelCapacity);
		}

		private static IntVec3 FindSpot(Map map, int half)
		{
			IntVec3 best = map.Center;
			int bestScore = int.MaxValue;
			for (int i = 0; i < SpotTries; i++)
			{
				IntVec3 c = i == 0 ? map.Center : map.Center + (Rand.InsideUnitCircleVec3 * SpotRadius).ToIntVec3();
				int score = SpotScore(map, c, half);
				if (score >= bestScore) continue;
				best = c;
				bestScore = score;
				if (score == 0) break;
			}
			return best;
		}

		private static int SpotScore(Map map, IntVec3 center, int half)
		{
			CellRect rect = CellRect.CenteredOn(center, half);
			if (!rect.InBounds(map)) return int.MaxValue;
			int score = 0;
			foreach (IntVec3 c in rect.Cells)
			{
				TerrainDef t = c.GetTerrain(map);
				if (t.passability == Traversability.Impassable || t.IsWater) score += 4;
				if (c.GetEdifice(map) != null) score += 2;
				if (c.Roofed(map)) score++;
			}
			return score;
		}

		private static void Clear(Map map, IntVec3 center, int half)
		{
			foreach (IntVec3 c in CellRect.CenteredOn(center, half).ClipInsideMap(map).Cells)
			{
				List<Thing> things = c.GetThingList(map);
				for (int i = things.Count - 1; i >= 0; i--)
				{
					Thing t = things[i];
					if (t is Pawn || t.Destroyed) continue;
					ThingCategory cat = t.def.category;
					if (cat == ThingCategory.Plant || cat == ThingCategory.Building || cat == ThingCategory.Item || cat == ThingCategory.Filth) t.Destroy();
				}
				if (c.Roofed(map)) map.roofGrid.SetRoof(c, null);
			}
		}

		private static void BuildRing(Map map, IntVec3 center, Faction faction)
		{
			bool bags = faction.def.techLevel >= TechLevel.Industrial;
			ThingDef def = bags ? ThingDefOf.Sandbags : DefDatabase<ThingDef>.GetNamedSilentFail("Barricade") ?? ThingDefOf.Sandbags;
			ThingDef stuff = def == ThingDefOf.Sandbags ? ThingDefOf.Cloth : ThingDefOf.WoodLog;
			float gap = Rand.Range(0f, 360f);
			foreach (IntVec3 c in GenRadial.RadialCellsAround(center, RingRadius + 0.5f, true))
			{
				if (!c.InBounds(map) || c.DistanceTo(center) < RingRadius - 0.5f) continue;
				float angle = (c - center).AngleFlat;
				if (Mathf.Abs(Mathf.DeltaAngle(angle, gap)) < GapAngle || Mathf.Abs(Mathf.DeltaAngle(angle, gap + 180f)) < GapAngle) continue;
				if (!GenSpawn.CanSpawnAt(def, c, map, Rot4.North)) continue;
				Thing t = ThingMaker.MakeThing(def, def.MadeFromStuff ? stuff : null);
				t.SetFaction(faction);
				GenSpawn.Spawn(t, c, map);
			}
		}

		public static void Check(ConvoyBattle battle)
		{
			Map map = battle.Map;
			TradeConvoy convoy = battle.convoy;
			if (convoy == null || convoy.Destroyed)
			{
				battle.resolved = true;
				battle.convoy = null;
				SendHome(map);
				return;
			}
			Pawn trader = battle.trader;
			if (trader == null || trader.Dead || trader.Discarded || trader.MapHeld != map)
			{
				Lose(battle);
				return;
			}
			if (trader.CarriedBy != null && trader.CarriedBy.HostileTo(convoy.Faction)) return;
			if (!GenHostility.AnyHostileActiveThreatTo(map, convoy.Faction)) Win(battle);
		}

		private static void SendHome(Map map)
		{
			List<Lord> lords = map.lordManager.lords;
			for (int i = 0; i < lords.Count; i++)
			{
				if (lords[i].LordJob is LordJob_ConvoyCamp) lords[i].ReceiveMemo(LordJob_ConvoyCamp.MemoLeave);
			}
		}

		private static void Win(ConvoyBattle battle)
		{
			battle.resolved = true;
			battle.won = true;
			TradeConvoy convoy = battle.convoy;
			Faction faction = convoy.Faction;
			convoy.EndAmbush(0f);
			var wounded = battle.convoyPawns.Where(p => p.Spawned && !p.Dead && p.Downed).ToList();
			for (int i = 0; i < wounded.Count; i++) wounded[i].ExitMap(false, Rot4.Invalid);
			SendHome(battle.Map);
			faction.TryAffectGoodwillWith(Faction.OfPlayer, SavedGoodwill, canSendMessage: false, canSendHostilityLetter: false, RimMarketDefOf.RimMarket_ConvoySaved);
			MarketManager.Instance?.Record(null, faction, MarketReputation.Safety, SavedSafety, null);
			TradeRoute r = TradeRouteUtility.RouteOf(convoy);
			if (r != null) r.freeVisit = true;
			string text = "RimMarket.ConvoySavedText".Translate(TradeRouteUtility.FromName(convoy), faction.Name, SavedGoodwill);
			if (r != null) text += "\n\n" + "RimMarket.ConvoySavedFee".Translate();
			Find.LetterStack.ReceiveLetter("RimMarket.ConvoySavedLabel".Translate(), text, LetterDefOf.PositiveEvent, new LookTargets(convoy));
		}

		private static void Lose(ConvoyBattle battle)
		{
			battle.resolved = true;
			TradeConvoy convoy = battle.convoy;
			string from = TradeRouteUtility.FromName(convoy);
			battle.convoy = null;
			TradeRouteUtility.LoseConvoy(convoy);
			SendHome(battle.Map);
			Find.LetterStack.ReceiveLetter("RimMarket.ConvoyFellLabel".Translate(), "RimMarket.ConvoyFellText".Translate(from),
				LetterDefOf.NegativeEvent, new LookTargets(battle.campCenter, battle.Map));
		}

		public static void Notify_MapRemoved(ConvoyBattle battle)
		{
			TradeConvoy convoy = battle.convoy;
			if (convoy == null || convoy.Destroyed) return;
			convoy.battle = null;
			if (!battle.resolved)
			{
				convoy.ambushUntil = Find.TickManager.TicksGame;
				return;
			}
			if (battle.won) SendOffStragglers(battle);
		}

		private static void SendOffStragglers(ConvoyBattle battle)
		{
			var left = battle.convoyPawns.Where(p => p.Spawned && !p.Dead).ToList();
			for (int i = 0; i < left.Count; i++) left[i].ExitMap(false, Rot4.Invalid);
		}

		public static bool ArriveWith(TradeConvoy convoy, Func<bool> execute)
		{
			ConvoyBattle battle = convoy.battle;
			if (battle != null && battle.won && battle.HasMap) SendOffStragglers(battle);
			replay = convoy.HasSurvivors ? convoy.Survivors : null;
			replayUsed = false;
			try
			{
				return execute();
			}
			finally
			{
				if (replayUsed)
				{
					convoy.ClearSurvivors();
					SendOffOrphans();
				}
				replay = null;
				replayUsed = false;
				replayed.Clear();
			}
		}

		private static void SendOffOrphans()
		{
			var orphans = replayed.Where(p => p.Spawned && !p.Dead && p.GetLord() == null).ToList();
			if (orphans.Count == 0) return;
			LordMaker.MakeNewLord(orphans[0].Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk), orphans[0].Map, orphans);
		}

		public static List<Pawn> TakeReplay(IncidentParms parms)
		{
			if (replay == null || replayUsed || !(parms.target is Map map)) return null;
			var list = new List<Pawn>();
			for (int i = 0; i < replay.Count; i++)
			{
				Pawn p = replay[i];
				if (p == null || p.Dead || p.Discarded || p.Spawned || p.Faction != parms.faction) continue;
				list.Add(p);
			}
			if (!list.Any(p => p.TraderKind != null)) return null;
			replayUsed = true;
			for (int i = 0; i < list.Count; i++)
			{
				Pawn p = list[i];
				TraderKindDef kind = p.TraderKind;
				if (kind != null && p.mindState != null) p.mindState.wantsToTradeWithColony = true;
				if (p.Downed) HealthUtility.HealNonPermanentInjuriesAndRestoreLegs(p);
				GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(parms.spawnCenter, map, 5), map);
				if (kind != null && p.TraderKind == null)
				{
					p.trader = new Pawn_TraderTracker(p);
					p.trader.traderKind = kind;
				}
				parms.storeGeneratedNeutralPawns?.Add(p);
				replayed.Add(p);
			}
			return list;
		}
	}
}
