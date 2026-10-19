using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
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
		[DebugAction("RimMarket", "End all market visits",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void EndAll()
		{
			MarketManager.Instance?.DebugEndAll();
		}

		[DebugAction("RimMarket", "Send visitors home",
			actionType = DebugActionType.ToolMap,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendHome()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			Pawn clicked = Verse.UI.MouseCell().GetThingList(map).OfType<Pawn>().FirstOrDefault(p => p.Faction != null && !p.Faction.IsPlayer
				&& !p.Faction.HostileTo(Faction.OfPlayer) && p.HostFaction == null && !p.IsPrisoner && !p.IsSlave);
			if (clicked == null) return;
			Lord lord = clicked.GetLord();
			StallVisit visit = lord != null ? MarketManager.Instance?.VisitFor(lord) : null;
			if (visit != null)
			{
				visit.closed = true;
				visit.endReason = StallVisit.EndClosed;
				Messages.Message("[RimMarket] " + clicked.Faction.Name + " market visit closed, the caravan packs up.", new LookTargets(clicked), MessageTypeDefOf.NeutralEvent, false);
				return;
			}
			List<Pawn> group = lord != null ? lord.ownedPawns.ToList() : new List<Pawn> { clicked };
			group.RemoveAll(p => p == null || !p.Spawned || p.Dead || p.Downed || p.Faction != clicked.Faction || p.HostFaction != null || p.IsPrisoner || p.IsSlave);
			if (group.Count == 0) return;
			for (int i = 0; i < group.Count; i++) group[i].GetLord()?.RemovePawn(group[i]);
			LordMaker.MakeNewLord(clicked.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, canDig: true), map, group);
			Messages.Message("[RimMarket] " + group.Count + " " + clicked.Faction.Name + " visitors sent home.", new LookTargets(group), MessageTypeDefOf.NeutralEvent, false);
		}

		[DebugAction("RimMarket", "Send trade caravan (random)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SendCaravanRandom()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			Faction faction = ReputationUtility.TraderFactions().RandomElementWithFallback();
			if (faction == null)
			{
				Messages.Message("[RimMarket] no visible friendly faction with caravan traders.", MessageTypeDefOf.RejectInput, false);
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
			foreach (Faction f in ReputationUtility.TraderFactions())
			{
				Faction faction = f;
				if (faction.def.caravanTraderKinds == null) continue;
				foreach (TraderKindDef k in faction.def.caravanTraderKinds)
				{
					TraderKindDef kind = k;
					options.Add(new DebugMenuOption(faction.Name + " | " + kind.label, DebugMenuOptionMode.Action, () => FireCaravan(map, faction, kind)));
				}
			}
			if (options.Count == 0)
			{
				Messages.Message("[RimMarket] no visible friendly faction with caravan traders.", MessageTypeDefOf.RejectInput, false);
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
			foreach (Lord lord in map.lordManager.lords.ToList())
			{
				if (!(lord.LordJob is LordJob_TradeWithColony) || mgr.VisitFor(lord) != null) continue;
				if (mgr.TryBeginVisit(lord) == null) continue;
				n++;
				lord.CurLordToil?.UpdateAllDuties();
			}
			Messages.Message("[RimMarket] " + n + " caravan(s) sent to a stall.", MessageTypeDefOf.NeutralEvent, false);
		}

		private static void FireCaravan(Map map, Faction faction, TraderKindDef kind)
		{
			IncidentDef def = IncidentDefOf.TraderCaravanArrival;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = faction;
			parms.traderKind = kind;
			if (!def.Worker.TryExecute(parms))
				Messages.Message("[RimMarket] the trade caravan from " + faction.Name + " could not arrive.", MessageTypeDefOf.RejectInput, false);
		}

		[DebugAction("RimMarket", "Open trade route (free)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void OpenRoute()
		{
			Map map = Find.CurrentMap;
			MarketManager mgr = MarketManager.Instance;
			if (map == null || mgr == null) return;
			var options = new List<FloatMenuOption>();
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer || f.defeated || f.HostileTo(Faction.OfPlayer) || f.def.caravanTraderKinds == null || f.def.caravanTraderKinds.Count == 0) continue;
				Settlement town = Find.WorldObjects.Settlements
					.Where(x => x.Faction == f && x.Tile.Layer == map.Tile.Layer && TradeRouteUtility.RouteFrom(mgr, x) == null)
					.OrderBy(x => Find.WorldGrid.ApproxDistanceInTiles(x.Tile, map.Tile)).FirstOrDefault();
				if (town == null) continue;
				Faction faction = f;
				options.Add(new FloatMenuOption(f.Name + " (" + town.LabelCap + ")", delegate
				{
					TradeRoute r = TradeRouteUtility.Open(faction, town, map, faction.def.caravanTraderKinds.RandomElement(), TradeRouteUtility.Weekly, false, 0);
					bool left = r != null && TradeRouteUtility.Depart(r);
					Messages.Message("[RimMarket] route from " + town.LabelCap + (left ? " opened, convoy on the road." : " opened, but no convoy could leave."),
						left ? new LookTargets(r.convoy) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, false);
				}));
			}
			if (options.Count == 0)
			{
				Messages.Message("[RimMarket] no friendly faction town to open a route from.", MessageTypeDefOf.RejectInput, false);
				return;
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		[DebugAction("RimMarket", "Send convoys now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void SendConvoys()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			int sent = 0;
			foreach (TradeRoute r in mgr.Routes.ToList())
			{
				if ((r.convoy == null || r.convoy.Destroyed) && TradeRouteUtility.Depart(r)) sent++;
			}
			Messages.Message("[RimMarket] " + sent + " convoy(s) sent.", MessageTypeDefOf.NeutralEvent, false);
		}

		[DebugAction("RimMarket", "Convoys arrive now",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void ConvoysArrive()
		{
			int n = 0;
			foreach (TradeConvoy c in Find.WorldObjects.AllWorldObjects.OfType<TradeConvoy>().ToList())
			{
				if (c.UnderAttack || c.Waiting) continue;
				c.JumpToEnd();
				n++;
			}
			Messages.Message("[RimMarket] " + n + " convoy(s) moved to their market.", MessageTypeDefOf.NeutralEvent, false);
		}

		[DebugAction("RimMarket", "Ambush a trade convoy",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.Playing)]
		private static void AmbushConvoy()
		{
			TradeConvoy convoy = Find.WorldObjects.AllWorldObjects.OfType<TradeConvoy>().FirstOrDefault(c => !c.UnderAttack && !c.Waiting && !c.AtEnd);
			if (convoy == null)
			{
				Messages.Message("[RimMarket] no trade convoy on the road.", MessageTypeDefOf.RejectInput, false);
				return;
			}
			Faction enemy = Find.FactionManager.AllFactionsListForReading
				.Where(f => f != convoy.Faction && !f.IsPlayer && !f.defeated && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(convoy.Faction) && f.HostileTo(Faction.OfPlayer))
				.OrderByDescending(f => f.def.permanentEnemy).FirstOrDefault() ?? TradeRouteUtility.RoadBandits(convoy.Faction);
			if (enemy == null)
			{
				Messages.Message("[RimMarket] nobody around to ambush that convoy.", MessageTypeDefOf.RejectInput, false);
				return;
			}
			while (Find.WorldObjects.AnyMapParentAt(convoy.Tile) && convoy.StepForward()) { }
			TradeRouteUtility.StartAmbush(convoy, enemy);
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
			Map map = Find.CurrentMap;
			if (map != null)
			{
				sb.Append("\n  visitors on the map:");
				int shown = 0;
				foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
				{
					if (!p.RaceProps.Humanlike || p.Faction == null || p.Faction.IsPlayer || p.Faction.HostileTo(Faction.OfPlayer)) continue;
					Lord lord = p.GetLord();
					sb.Append("\n    " + p.LabelShort + " (" + p.Faction.Name + ", " + p.kindDef?.defName + ")"
						+ " | lord " + (lord?.LordJob?.GetType().FullName ?? "none")
						+ " | toil " + (lord?.CurLordToil?.GetType().Name ?? "-")
						+ " | duty " + (p.mindState?.duty?.def?.defName ?? "none")
						+ " | job " + (p.CurJobDef?.defName ?? "none")
						+ " | guest " + (p.HostFaction?.Name ?? "no"));
					shown++;
				}
				if (shown == 0) sb.Append(" none");
			}
			Log.Message(sb.ToString());
		}
	}
}
