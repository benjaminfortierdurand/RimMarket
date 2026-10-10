using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimMarket.AI;
using RimMarket.Auction;
using RimMarket.Buildings;
using RimMarket.Inn;
using RimMarket.Market;
using RimMarket.Reputation;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Debug
{
	public static class DebugActions_RimMarket
	{
		[DebugAction("RimMarket", "Send trade caravan (random)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendCaravanRandom()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			Faction faction = TraderFactions().RandomElementWithFallback();
			if (faction == null)
			{
				Log.Warning("[RimMarket] no visible non-hostile faction with caravan trader kinds.");
				return;
			}
			FireCaravan(map, faction, null);
		}

		[DebugAction("RimMarket", "Send trade caravan (pick faction and kind)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendCaravanPick()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			var options = new List<DebugMenuOption>();
			foreach (Faction f in TraderFactions())
			{
				Faction faction = f;
				foreach (TraderKindDef k in faction.def.caravanTraderKinds)
				{
					TraderKindDef kind = k;
					options.Add(new DebugMenuOption(faction.Name + " | " + kind.label, DebugMenuOptionMode.Action,
						() => FireCaravan(map, faction, kind)));
				}
			}
			if (options.Count == 0)
			{
				Log.Warning("[RimMarket] no visible non-hostile faction with caravan trader kinds.");
				return;
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}

		[DebugAction("RimMarket", "Send every caravan on map to a stall",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ForceToStall()
		{
			Map map = Find.CurrentMap;
			MarketManager mgr = MarketManager.Instance;
			if (map == null || mgr == null) return;
			int n = 0;
			foreach (Lord lord in map.lordManager.lords)
			{
				if (!(lord.LordJob is LordJob_TradeWithColony)) continue;
				if (mgr.VisitFor(lord) != null) continue;
				StallVisit v = mgr.TryBeginVisit(lord);
				if (v == null) continue;
				n++;
				lord.CurLordToil?.UpdateAllDuties();
			}
			Messages.Message("RimMarket: " + n + " caravan(s) assigned a stall, they switch on their next check.",
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Send visitors (Hospitality guests if loaded)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendVisitors()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			IncidentDef def = IncidentDefOf.VisitorGroup;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = ArrivalFactions(PawnGroupKindDefOf.Peaceful).RandomElementWithFallback();
			if (parms.faction == null)
			{
				Log.Warning("[RimMarket] no visible non-hostile faction can send a peaceful group.");
				return;
			}
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimMarket] the visitor group refused to fire.");
			}
		}

		[DebugAction("RimMarket", "Visit a stall now (click a pawn)",
			actionType = DebugActionType.ToolMapForPawns,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void VisitStallNow(Pawn p)
		{
			if (p == null || p.jobs == null) return;
			Job job = MarketUtility.MakeVisitJob(p);
			if (job == null)
			{
				Messages.Message("RimMarket: no open stall with its merchant at the counter that " + p.LabelShort + " can reach.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			p.jobs.StartJob(job, JobCondition.InterruptForced);
		}

		[DebugAction("RimMarket", "Give 100 silver (click a pawn)",
			actionType = DebugActionType.ToolMapForPawns,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void GiveSilver(Pawn p)
		{
			if (p?.inventory == null) return;
			Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
			silver.stackCount = 100;
			p.inventory.innerContainer.TryAdd(silver);
		}

		[DebugAction("RimMarket", "Send a market thief now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendThief()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			if (MarketManager.Instance == null || MarketManager.Instance.ActiveVisits(map) == 0)
			{
				Messages.Message("RimMarket: a thief needs a merchant at a stall on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (!RimMarket.Thieves.ThiefUtility.TrySendThief(map, 300))
				Log.Warning("[RimMarket] no thief could be sent (no suitable faction or entry cell).");
		}

		[DebugAction("RimMarket", "Send a bold thief (never backs down)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendBoldThief()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			if (MarketManager.Instance == null || MarketManager.Instance.ActiveVisits(map) == 0)
			{
				Messages.Message("RimMarket: a thief needs a merchant at a stall on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (!RimMarket.Thieves.ThiefUtility.TrySendThief(map, 300, bold: true))
				Log.Warning("[RimMarket] no thief could be sent (no suitable faction or entry cell).");
		}

		[DebugAction("RimMarket", "End all market visits",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void EndAll()
		{
			MarketManager.Instance?.DebugEndAll();
		}

		[DebugAction("RimMarket", "Reset haggle on all visits",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ResetHaggle()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			foreach (StallVisit v in mgr.Visits)
			{
				v.haggleUsed = false;
				v.haggleWon = false;
				v.haggleBuyFactor = 1f;
				v.haggleSellFactor = 1f;
			}
		}

		[DebugAction("RimMarket", "Make all visits end in 1 hour",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ShortenVisits()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			foreach (StallVisit v in mgr.Visits) v.endTick = Find.TickManager.TicksGame + 2500;
		}

		[DebugAction("RimMarket", "Log market state",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void LogState()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			var sb = new StringBuilder("[RimMarket] market state, fees collected " + mgr.TotalFees
				+ ", guest sales " + mgr.TotalGuestSales
				+ ", thefts in progress " + mgr.Thefts.Count
				+ ", thieves caught " + mgr.ThievesCaught + " escaped " + mgr.ThievesEscaped + " deterred " + mgr.ThievesDeterred
				+ ", reputation " + mgr.Reputation.HeadlineLabel + " total " + mgr.Reputation.Total.ToString("F1")
				+ " [safety " + mgr.Reputation.Score(0).ToString("F1") + " fairness " + mgr.Reputation.Score(1).ToString("F1")
				+ " fees " + mgr.Reputation.Score(2).ToString("F1") + " appeal " + mgr.Reputation.Score(3).ToString("F1") + "]"
				+ ", Hospitality " + (RimMarket.Compat.HospitalityBridge.Active ? (RimMarket.Compat.HospitalityBridge.Available ? "bridged" : "loaded, bridge failed") : "absent")
				+ ", Hotels and Recreation " + (RimMarket.Compat.HotelsBridge.Active ? (RimMarket.Compat.HotelsBridge.Available ? "bridged" : "loaded, bridge failed") : "absent") + ":");
			foreach (FactionLedger l in mgr.Ledgers)
			{
				if (l?.faction == null || l.rep == null) continue;
				MarketReputation opinion = mgr.OpinionOf(l.faction);
				sb.Append("\n  memory of " + l.faction.Name + ": own " + l.rep.Total.ToString("F1")
					+ " (weight " + (mgr.OwnWeight(l.faction) * 100f).ToString("F0") + "%)"
					+ ", opinion " + opinion.Total.ToString("F1") + " " + opinion.HeadlineLabel
					+ ", refusal " + (mgr.RefusalChance(l.faction) * 100f).ToString("F0") + "%"
					+ ", prices " + (opinion.PriceBonus * 100f).ToString("F1") + "%");
			}
			if (mgr.Visits.Count == 0) sb.Append("\n  (no visit)");
			foreach (StallVisit v in mgr.Visits)
			{
				sb.Append("\n  #" + v.id + " " + (v.faction?.Name ?? "?")
					+ " | trader " + (v.trader?.LabelShort ?? "?")
					+ " | stall " + (v.stall?.Position.ToString() ?? "none")
					+ " | toil " + (v.lord?.CurLordToil?.GetType().Name ?? "?")
					+ " | fee mode " + v.feeMode + " amount " + v.feeAmount + " due " + v.feeDue + " paid " + v.feePaid
					+ " | share owed " + v.shareOwed.ToString("F0")
					+ " | haggle " + (v.haggleUsed ? (v.haggleWon ? "won" : "lost") : "open")
					+ " | hours left " + (v.TicksLeft / 2500f).ToString("F1")
					+ " | closed " + v.closed);
			}
			Log.Message(sb.ToString());
		}

		private static IEnumerable<Faction> ArrivalFactions(PawnGroupKindDef groupKind) => ReputationUtility.ArrivalFactions(groupKind);

		private static IEnumerable<Faction> TraderFactions() => ReputationUtility.TraderFactions();

		[DebugAction("RimMarket", "Reputation: make it renowned",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepRenowned()
		{
			MarketManager.Instance?.Reputation.DebugSet(30f);
		}

		[DebugAction("RimMarket", "Reputation: make it notorious",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepNotorious()
		{
			MarketManager.Instance?.Reputation.DebugSet(-30f);
		}

		[DebugAction("RimMarket", "Reputation: reset",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepReset()
		{
			MarketManager.Instance?.Reputation.DebugSet(0f);
			MarketManager.Instance?.DebugClearLedgers();
		}

		[DebugAction("RimMarket", "Send a caravan drawn by the reputation",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepCaravan()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || Find.CurrentMap == null) return;
			if (!ReputationUtility.TrySendMarketCaravan(Find.CurrentMap, announce: true))
				Log.Warning("[RimMarket] no caravan could be sent.");
		}

		[DebugAction("RimMarket", "Reputation: a faction loves the market (pick)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepFactionLoves()
		{
			PickFaction(f => MarketManager.Instance?.DebugSetLedger(f, 30f));
		}

		[DebugAction("RimMarket", "Reputation: a faction hates the market (pick)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void RepFactionHates()
		{
			PickFaction(f => MarketManager.Instance?.DebugSetLedger(f, -30f));
		}

		private static Building_MarketMarker DebugMarker(Map map)
		{
			if (map == null) return null;
			return map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker).OfType<Building_MarketMarker>().FirstOrDefault(m => m.Spawned);
		}

		[DebugAction("RimMarket", "Fair: announce now (free, opens in an hour)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugAnnounceFair()
		{
			Map map = Find.CurrentMap;
			MarketManager mgr = MarketManager.Instance;
			Building_MarketMarker marker = DebugMarker(map);
			if (mgr == null || marker == null)
			{
				Messages.Message("RimMarket: no market marker on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (mgr.FairOn(map) != null) mgr.RemoveFair(mgr.FairOn(map));
			int now = Find.TickManager.TicksGame;
			var fair = new Fair.GreatFair
			{
				map = map,
				marker = marker,
				state = Fair.GreatFair.Announced,
				announcedTick = now,
				startTick = now + GenDate.TicksPerHour,
			};
			fair.endTick = fair.startTick + (Fair.FairUtility.CloseHour - Fair.FairUtility.OpenHour) * GenDate.TicksPerHour;
			mgr.AddFair(fair);
			Messages.Message("RimMarket: Great Fair announced, it opens in an hour.", MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Fair: open now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugOpenFair()
		{
			MarketManager mgr = MarketManager.Instance;
			Fair.GreatFair fair = mgr?.FairOn(Find.CurrentMap);
			if (fair == null || fair.state != Fair.GreatFair.Announced)
			{
				Messages.Message("RimMarket: no announced fair on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			int now = Find.TickManager.TicksGame;
			fair.startTick = now;
			fair.endTick = now + (Fair.FairUtility.CloseHour - Fair.FairUtility.OpenHour) * GenDate.TicksPerHour;
			Fair.FairUtility.Open(mgr, fair);
		}

		[DebugAction("RimMarket", "Fair: judge the best stall now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugJudgeFair()
		{
			MarketManager mgr = MarketManager.Instance;
			Fair.GreatFair fair = mgr?.FairOn(Find.CurrentMap);
			if (fair == null || fair.state != Fair.GreatFair.Running)
			{
				Messages.Message("RimMarket: no fair running on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Fair.FairUtility.Judge(mgr, fair);
			if (fair.winnerStall == null) Messages.Message("RimMarket: nobody has sold anything yet, no prize.", MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Fair: close now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugCloseFair()
		{
			MarketManager mgr = MarketManager.Instance;
			Fair.GreatFair fair = mgr?.FairOn(Find.CurrentMap);
			if (fair == null || fair.state != Fair.GreatFair.Running)
			{
				Messages.Message("RimMarket: no fair running on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			fair.endTick = Find.TickManager.TicksGame;
			Fair.FairUtility.Close(mgr, fair);
		}

		[DebugAction("RimMarket", "Special orders: hurry them along",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugHurryOrders()
		{
			MarketManager mgr = MarketManager.Instance;
			int now = Find.TickManager.TicksGame;
			Orders.WantedNotice notice = mgr?.Notice;
			if (notice != null)
			{
				for (int i = 0; i < notice.pendingTicks.Count; i++) notice.pendingTicks[i] = now;
				Orders.WantedUtility.Tick(mgr);
			}
			if (mgr == null || mgr.Orders.Count == 0)
			{
				Messages.Message(notice != null ? "RimMarket: every town answered the wanted notice." : "RimMarket: no special order or notice pending.",
					MessageTypeDefOf.NeutralEvent, historical: false);
				return;
			}
			for (int i = 0; i < mgr.Orders.Count; i++)
			{
				Orders.SpecialOrder o = mgr.Orders[i];
				if (!o.departed) o.departTick = now;
				else if (o.convoy != null && !o.convoy.Destroyed) o.convoy.JumpToEnd();
			}
			Messages.Message("RimMarket: orders leave now, convoys on the road jump to your border. Run again after they leave.", MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Fair: petals now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugFairPetals()
		{
			Fair.GreatFair fair = MarketManager.Instance?.FairOn(Find.CurrentMap);
			if (fair == null)
			{
				Messages.Message("RimMarket: no fair on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Fair.MapComponent_MarketShow.On(Find.CurrentMap)?.Opening(fair);
		}

		[DebugAction("RimMarket", "Fair: closing show now (lanterns or fireworks)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DebugFairShow()
		{
			Fair.GreatFair fair = MarketManager.Instance?.FairOn(Find.CurrentMap);
			if (fair == null)
			{
				Messages.Message("RimMarket: no fair on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Fair.MapComponent_MarketShow.On(Find.CurrentMap)?.Closing(fair);
		}

		[DebugAction("RimMarket", "Send shoppers now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendShoppers()
		{
			Map map = Find.CurrentMap;
			StallVisit v = MarketManager.Instance?.Visits.FirstOrDefault(x => x.map == map && !x.closed && x.StallOk);
			Building_MarketStall colony = v == null ? ColonyStalls.FirstStocked(map) : null;
			if (v == null && colony == null)
			{
				Messages.Message("RimMarket: no merchant at a stall and no stocked colony stall in an open market on this map.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			string from = v != null ? MarketShoppers.Send(v) : MarketShoppers.Send(map, colony.Stall, null);
			if (from != null) return;
			var sb = new StringBuilder("[RimMarket] no shoppers could be sent: " + (MarketShoppers.LastFailure ?? "unknown reason") + ".");
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer) continue;
				sb.Append("\n  - " + f.Name + ": " + (MarketShoppers.WhyNotFaction(f, map) ?? "could come"));
			}
			Log.Warning(sb.ToString());
		}

		[DebugAction("RimMarket", "Declare war between two market factions (truce test)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DeclareMarketWar()
		{
			Map map = Find.CurrentMap;
			List<Faction> factions = new List<Faction>();
			foreach (Lord lord in map.lordManager.lords)
			{
				Faction f = lord.faction;
				if (f == null || f.IsPlayer || f.HostileTo(Faction.OfPlayer) || factions.Contains(f)) continue;
				if (MarketManager.Instance?.VisitFor(lord) != null || lord.LordJob is LordJob_MarketShoppers) factions.Add(f);
			}
			if (factions.Count < 2)
			{
				Messages.Message("RimMarket: needs two different factions at the market (merchants or shoppers).", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Faction a = factions[0];
			Faction b = factions[1];
			a.RelationWith(b).kind = FactionRelationKind.Hostile;
			b.RelationWith(a).kind = FactionRelationKind.Hostile;
			foreach (Map m in Find.Maps) m.attackTargetsCache.Notify_FactionHostilityChanged(a, b);
			Messages.Message("RimMarket: " + a.Name + " and " + b.Name + " are now at war.", MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Routes: open a free route (pick faction)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void OpenFreeRoute()
		{
			Map map = Find.CurrentMap;
			PickFaction(f =>
			{
				Settlement best = null;
				float bestDist = float.MaxValue;
				foreach (Settlement s in Find.WorldObjects.Settlements)
				{
					if (s.Faction != f || !s.Spawned || TradeRouteUtility.RouteFrom(MarketManager.Instance, s) != null) continue;
					float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, map.Tile);
					if (d < bestDist && TradeRouteUtility.TryPath(s.Tile, map.Tile, out _, out _))
					{
						best = s;
						bestDist = d;
					}
				}
				TraderKindDef kind = TradeRouteUtility.KindsFor(f, map, null).FirstOrDefault();
				if (best == null || kind == null)
				{
					Messages.Message("RimMarket: no reachable town or no merchant kind for " + f.Name + ".", MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				TradeRouteUtility.Open(f, best, map, kind, TradeRouteUtility.Fortnightly, false, TradeRouteUtility.Toll((int)bestDist, false));
				Messages.Message("RimMarket: route opened from " + best.LabelCap + " (" + kind.label + ").", MessageTypeDefOf.NeutralEvent, historical: false);
			});
		}

		[DebugAction("RimMarket", "Routes: send every convoy now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void DispatchConvoys()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			foreach (TradeRoute r in mgr.Routes.ToList())
			{
				if (r.convoy == null || r.convoy.Destroyed) TradeRouteUtility.Depart(r);
			}
		}

		[DebugAction("RimMarket", "Routes: ambush every convoy now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void AmbushConvoys()
		{
			foreach (TradeConvoy c in Find.WorldObjects.AllWorldObjects.OfType<TradeConvoy>().ToList())
			{
				if (c.UnderAttack || c.Waiting) continue;
				Faction by = TradeRouteUtility.RoadBandits(c.Faction);
				if (by == null) continue;
				TradeRouteUtility.StartAmbush(c, by);
				c.ambushUntil = Find.TickManager.TicksGame + 600;
			}
		}

		[DebugAction("RimMarket", "Routes: convoys arrive now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void ConvoysArrive()
		{
			foreach (TradeConvoy c in Find.WorldObjects.AllWorldObjects.OfType<TradeConvoy>().ToList())
			{
				c.JumpToEnd();
				c.waitingSince = Find.TickManager.TicksGame;
				TradeRouteUtility.TryArrive(c);
			}
		}

		[DebugAction("RimMarket", "Tire market people (inn test)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void TireMarketPeople()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			if (!InnBeds.Any) Messages.Message("RimMarket: no bed is set to Market inn yet.", MessageTypeDefOf.RejectInput, historical: false);
			InnUtility.Tire(map);
		}

		[DebugAction("RimMarket", "Guards: tire the watch (relief test)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void TireTheWatch()
		{
			Map map = Find.CurrentMap;
			MarketManager mgr = MarketManager.Instance;
			if (map == null || mgr == null) return;
			int tired = 0;
			foreach (StallVisit v in mgr.Visits)
			{
				if (v.map != map || v.lord == null) continue;
				foreach (Pawn p in v.lord.ownedPawns)
				{
					if (p.needs?.rest == null || !GuardWatch.IsGuard(p)) continue;
					if (v.posted.Contains(p))
					{
						p.needs.rest.CurLevel = GuardWatch.WornRest - 0.02f;
						tired++;
					}
					else if (p.Awake()) p.needs.rest.CurLevel = System.Math.Max(p.needs.rest.CurLevel, 0.6f);
				}
				v.nextReliefTick = 0;
			}
			Messages.Message("RimMarket: " + tired + " guard(s) on watch are now worn out.", MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimMarket", "Send a slaver caravan with slaves",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendSlaver()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			var pairs = new List<(Faction faction, TraderKindDef kind)>();
			foreach (Faction f in TraderFactions())
			{
				foreach (TraderKindDef k in f.def.caravanTraderKinds)
				{
					if (k.stockGenerators != null && k.stockGenerators.Any(g => g is StockGenerator_Slaves)) pairs.Add((f, k));
				}
			}
			if (pairs.Count == 0)
			{
				Log.Warning("[RimMarket] no visible non-hostile faction sends slaver caravans.");
				return;
			}
			(Faction faction, TraderKindDef kind) pick = pairs.Where(p => !AuctionUtility.FactionShunsSlavery(p.faction)).RandomElementWithFallback(pairs.RandomElement());
			var before = new HashSet<Lord>(map.lordManager.lords);
			FireCaravan(map, pick.faction, pick.kind);
			Lord lord = map.lordManager.lords.FirstOrDefault(l => !before.Contains(l) && l.LordJob is LordJob_TradeWithColony);
			Pawn trader = lord == null ? null : TraderCaravanUtility.FindTrader(lord);
			if (trader == null) return;
			int have = lord.ownedPawns.Count(p => p.RaceProps.Humanlike && p.GetTraderCaravanRole() == TraderCaravanRole.Chattel);
			for (int i = have; i < 3; i++)
			{
				Pawn slave = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Slave, pick.faction, forceGenerateNewPawn: true));
				GenSpawn.Spawn(slave, CellFinder.RandomClosewalkCellNear(trader.Position, map, 4), map);
				lord.AddPawn(slave);
			}
		}

		[DebugAction("RimMarket", "Slave auction: skip ahead (next lot or gavel now)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void AuctionAdvance()
		{
			int n = MarketManager.Instance?.DebugAdvanceAuctions() ?? 0;
			if (n == 0)
				Messages.Message("RimMarket: no auction under way. A slaver needs a stall and an auction post in the same market.",
					MessageTypeDefOf.RejectInput, historical: false);
		}

		[DebugAction("RimMarket", "Away stalls: run the rest of the day now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void AwayRunDay()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || mgr.AwayStalls.Count == 0)
			{
				Messages.Message("RimMarket: no caravan is holding a stall away.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			mgr.DebugRunAwayDay();
		}

		[DebugAction("RimMarket", "Away stalls: clear market cooldowns",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void AwayClearCooldowns()
		{
			MarketManager.Instance?.DebugClearAwayCooldowns();
		}

		private static void PickFaction(System.Action<Faction> act)
		{
			var options = new List<DebugMenuOption>();
			foreach (Faction f in TraderFactions())
			{
				Faction faction = f;
				options.Add(new DebugMenuOption(faction.Name, DebugMenuOptionMode.Action, () => act(faction)));
			}
			if (options.Count == 0)
			{
				Log.Warning("[RimMarket] no visible non-hostile faction with caravan trader kinds.");
				return;
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}

		private static void FireCaravan(Map map, Faction faction, TraderKindDef kind)
		{
			IncidentDef def = IncidentDefOf.TraderCaravanArrival;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = faction;
			parms.traderKind = kind;
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimMarket] the trade caravan refused to fire (faction " + faction.Name + ").");
			}
		}
	}
}
