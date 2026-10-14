using System.Text;
using LudeonTK;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using Verse;
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
