using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimMarket.Inn;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimMarket.Reputation
{
	public static class ReputationUtility
	{
		public const float ThiefCaught = 4f;
		public const float ThiefDeterred = 2f;
		public const float ThiefEscaped = -6f;
		public const float HaggleWon = -1f;
		public const float HaggleLost = -2f;

		private const float PleasedAt = 2f;
		private const float GrumblingAt = -2f;
		private const int RefusalSeed = 0x5A17;

		public static IEnumerable<Faction> ArrivalFactions(PawnGroupKindDef groupKind)
		{
			return Find.FactionManager.AllFactionsVisible.Where(f =>
				!f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && !f.HostileTo(Faction.OfPlayer)
				&& f.def.humanlikeFaction
				&& f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(g => g.kindDef == groupKind));
		}

		public static IEnumerable<Faction> TraderFactions()
		{
			return ArrivalFactions(PawnGroupKindDefOf.Trader).Where(f =>
				f.def.caravanTraderKinds != null && f.def.caravanTraderKinds.Count > 0);
		}

		public static void Record(MarketManager mgr, StallVisit v, int axis, float points, string reason)
		{
			mgr?.Record(v, v?.faction, axis, points, reason);
		}

		public static void EvaluateVisitEnd(MarketManager mgr, StallVisit v, int reason)
		{
			if (mgr == null || v == null || reason == StallVisit.EndLordGone) return;

			bool traderHurt = !v.feud && (v.trader == null || v.trader.Dead || v.trader.Downed);
			bool lostPeople = !v.feud && v.lord != null && v.lord.numPawnsLostViolently > 0;
			if (traderHurt)
				Record(mgr, v, MarketReputation.Safety, -15f, "RimMarket.ReasonMerchantHurt");
			else if (lostPeople)
				Record(mgr, v, MarketReputation.Safety, -8f, "RimMarket.ReasonCaravanHurt");
			else if (!v.robbed && reason == StallVisit.EndNormal && (v.lord == null || v.lord.numPawnsLostViolently == 0))
				Record(mgr, v, MarketReputation.Safety, 1.5f, "RimMarket.ReasonSafe");

			switch (reason)
			{
				case StallVisit.EndNormal:
					Record(mgr, v, MarketReputation.Fairness, 2f, "RimMarket.ReasonFullDay");
					break;
				case StallVisit.EndClosed:
					Record(mgr, v, MarketReputation.Fairness, v.feePaid ? -6f : -2f, "RimMarket.ReasonClosedEarly");
					break;
				case StallVisit.EndStallGone:
					Record(mgr, v, MarketReputation.Fairness, -4f, "RimMarket.ReasonStallGone");
					break;
			}

			EvaluateFees(mgr, v);
			EvaluateAppeal(mgr, v);
		}

		private static void EvaluateFees(MarketManager mgr, StallVisit v)
		{
			if (v.feeMode == RimMarketSettings.FeeNone)
			{
				Record(mgr, v, MarketReputation.Fees, 3f, "RimMarket.ReasonNoFee");
				return;
			}
			if (v.feeMode == RimMarketSettings.FeeShare)
			{
				float rate = RimMarketMod.Settings.sharePercent;
				if (rate <= 0.10f) Record(mgr, v, MarketReputation.Fees, 1f, "RimMarket.ReasonGentleFee");
				else if (rate > 0.20f) Record(mgr, v, MarketReputation.Fees, -4f, "RimMarket.ReasonRacket");
				else Record(mgr, v, MarketReputation.Fees, -1f, null);
				return;
			}
			if (v.feeAmount <= 0)
			{
				if (!v.feeShort) Record(mgr, v, MarketReputation.Fees, 1f, "RimMarket.ReasonNoFee");
				return;
			}
			float ratio = v.feeAmount / Mathf.Max(1f, v.wealthAtArrival);
			if (ratio <= 0.02f) Record(mgr, v, MarketReputation.Fees, 2f, "RimMarket.ReasonGentleFee");
			else if (ratio <= 0.04f) Record(mgr, v, MarketReputation.Fees, 0.5f, null);
			else if (ratio <= 0.07f) Record(mgr, v, MarketReputation.Fees, -3f, "RimMarket.ReasonSteepFee");
			else Record(mgr, v, MarketReputation.Fees, -6f, "RimMarket.ReasonRacket");
			if (v.tolledToday) Record(mgr, v, MarketReputation.Fees, -3f, "RimMarket.ReasonTaxedTwice");
		}

		private static void EvaluateAppeal(MarketManager mgr, StallVisit v)
		{
			if (v.StallOk && v.map != null)
			{
				float beauty = BeautyUtility.AverageBeautyPerceptible(v.stall.CustomerCell, v.map);
				if (beauty >= 3f) Record(mgr, v, MarketReputation.Appeal, 2f, "RimMarket.ReasonHandsome");
				else if (beauty < 0f) Record(mgr, v, MarketReputation.Appeal, -2f, "RimMarket.ReasonShabby");
				Building_MarketMarker marker = MarketUtility.MarkerFor(v.stall);
				if (marker != null && MarketUtility.CountStalls(marker, out _, out _) >= 3)
					Record(mgr, v, MarketReputation.Appeal, 1f, "RimMarket.ReasonBigMarket");
			}
			if (v.silverEarned >= 100)
				Record(mgr, v, MarketReputation.Appeal, Mathf.Min(4f, v.silverEarned / 100f), "RimMarket.ReasonGoodSales");
			if (v.browsers >= 2) Record(mgr, v, MarketReputation.Appeal, 1f, "RimMarket.ReasonBusy");
			if (v.innNights > 0)
			{
				float comfort = v.innComfort / (float)v.innNights;
				if (comfort >= 0.5f)
				{
					Record(mgr, v, MarketReputation.Appeal, 2f, "RimMarket.ReasonInnFine");
					if (!v.innSteep) InnUtility.ThankForBed(v.faction);
				}
				else if (comfort <= -0.5f) Record(mgr, v, MarketReputation.Appeal, -2f, "RimMarket.ReasonInnPoor");
				if (v.innSteep) Record(mgr, v, MarketReputation.Fairness, -2f, "RimMarket.ReasonInnSteep");
			}
		}

		public static int Verdict(StallVisit v)
		{
			float total = v.RepTotal;
			if (total >= PleasedAt) return 1;
			if (total <= GrumblingAt) return -1;
			return 0;
		}

		public static string ReasonsText(StallVisit v, int verdict, int max = 2)
		{
			var picked = new List<KeyValuePair<string, float>>();
			for (int i = 0; i < v.reasonKeys.Count; i++)
			{
				float value = v.reasonValues[i];
				if (verdict > 0 && value <= 0f) continue;
				if (verdict < 0 && value >= 0f) continue;
				picked.Add(new KeyValuePair<string, float>(v.reasonKeys[i], value));
			}
			picked.SortByDescending(kv => Mathf.Abs(kv.Value));
			var labels = new List<string>();
			for (int i = 0; i < picked.Count && labels.Count < max; i++) labels.Add(picked[i].Key.Translate());
			return labels.ToCommaList(useAnd: true);
		}

		public static MarketLogEntry MakeLogEntry(StallVisit v, int verdict)
		{
			var entry = new MarketLogEntry
			{
				tick = Find.TickManager.TicksGame,
				faction = v.faction,
				factionName = v.faction?.Name ?? "?",
				traderName = v.trader?.LabelShortCap ?? "?",
				verdict = verdict,
				deltas = new List<float>(v.repDeltas),
				colonyBought = Mathf.RoundToInt(v.colonyBought),
				colonySold = Mathf.RoundToInt(v.colonySold),
				guestSales = v.silverEarned,
				fees = v.feeCollected,
			};
			var ordered = new List<int>();
			for (int i = 0; i < v.reasonKeys.Count; i++)
			{
				float value = v.reasonValues[i];
				if (verdict > 0 && value <= 0f) continue;
				if (verdict < 0 && value >= 0f) continue;
				ordered.Add(i);
			}
			ordered.SortByDescending(i => Mathf.Abs(v.reasonValues[i]));
			for (int i = 0; i < ordered.Count && entry.reasons.Count < 3; i++) entry.reasons.Add(v.reasonKeys[ordered[i]]);
			return entry;
		}

		public static string TakingsText(int bought, int sold, int guests, int fees, int inn = 0)
		{
			var parts = new List<string>();
			if (bought > 0) parts.Add("RimMarket.TakingsBought".Translate(bought));
			if (sold > 0) parts.Add("RimMarket.TakingsSold".Translate(sold));
			if (guests > 0) parts.Add("RimMarket.TakingsGuests".Translate(guests));
			if (fees > 0) parts.Add("RimMarket.TakingsFees".Translate(fees));
			if (inn > 0) parts.Add("RimMarket.TakingsInn".Translate(inn));
			if (parts.Count == 0) return null;
			return "RimMarket.TakingsLine".Translate(parts.ToCommaList(useAnd: true));
		}

		public static string TakingsText(StallVisit v)
		{
			string text = TakingsText(Mathf.RoundToInt(v.colonyBought), Mathf.RoundToInt(v.colonySold), v.silverEarned, v.feeCollected, v.innEarned);
			return text ?? "RimMarket.TakingsNone".Translate().ToString();
		}

		public static bool RefusesMarket(Lord lord, float chance)
		{
			if (lord == null || !RimMarketMod.Settings.reputationEffects) return false;
			return chance > 0f && Rand.ChanceSeeded(chance, lord.loadID ^ RefusalSeed);
		}

		public static string RefusalKey(MarketReputation rep, bool grudge)
		{
			if (grudge) return "RimMarket.MessageRefuseGrudge";
			switch (rep.WorstAxis)
			{
				case MarketReputation.Safety: return "RimMarket.MessageRefuseUnsafe";
				case MarketReputation.Fairness: return "RimMarket.MessageRefuseUnfair";
				case MarketReputation.Fees: return "RimMarket.MessageRefuseRacket";
				default: return "RimMarket.MessageRefuseShabby";
			}
		}

		public static bool TraderKindAllowed(TraderKindDef kind, Faction faction, Map map)
		{
			if (kind == null) return false;
			if (kind.faction != null && kind.faction != faction.def) return false;
			if (ModsConfig.IdeologyActive && faction.ideos != null && kind.category == "Slaver")
			{
				foreach (Ideo ideo in faction.ideos.AllIdeos)
				{
					if (!ideo.IdeoApprovesOfSlavery()) return false;
				}
			}
			if (kind.permitRequiredForTrading != null
				&& !map.mapPawns.FreeColonists.Any(p => p.royalty != null && p.royalty.HasPermit(kind.permitRequiredForTrading, faction)))
				return false;
			return kind.CalculatedCommonality > 0f;
		}

		public static TraderKindDef PickTraderKind(Faction faction, Map map, float rarityBias)
		{
			List<TraderKindDef> kinds = faction.def.caravanTraderKinds;
			if (kinds == null) return null;
			var allowed = kinds.Where(k => TraderKindAllowed(k, faction, map)).ToList();
			if (allowed.Count == 0) return null;
			float exponent = 1f - Mathf.Clamp01(rarityBias);
			return allowed.RandomElementByWeight(k => Mathf.Pow(k.CalculatedCommonality, exponent));
		}

		public static float CaravanWeight(MarketReputation opinion)
		{
			if (opinion == null) return 1f;
			return Mathf.Max(0.2f, 1f + opinion.Total / 60f);
		}

		public static bool TrySendMarketCaravan(Map map, bool announce)
		{
			if (map == null) return false;
			MarketManager mgr = MarketManager.Instance;
			List<Faction> candidates = TraderFactions().Where(f => f.def.caravanTraderKinds.Any(k => TraderKindAllowed(k, f, map))).ToList();
			if (mgr != null) candidates.RemoveAll(f => mgr.RefusalChance(f) > 0f);
			candidates.RemoveAll(f => !MarketTruce.Welcome(map, f) || !Routes.TradeRouteUtility.CaravansCanReach(f, map));
			if (!candidates.TryRandomElementByWeight(f => CaravanWeight(mgr?.OpinionOf(f)), out Faction faction)) return false;
			float rarityBias = mgr?.OpinionOf(faction).RarityBias ?? 0f;
			TraderKindDef kind = PickTraderKind(faction, map, rarityBias);
			if (kind == null) return false;
			IncidentDef def = IncidentDefOf.TraderCaravanArrival;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = faction;
			parms.traderKind = kind;
			if (!def.Worker.TryExecute(parms)) return false;
			if (announce && RimMarketMod.Settings.showMessages)
				Messages.Message("RimMarket.MessageWordSpread".Translate(faction.Name, kind.label), MessageTypeDefOf.PositiveEvent, historical: false);
			return true;
		}

		public static Faction TrySendFairCaravan(Map map, List<Faction> already)
		{
			if (map == null) return null;
			MarketManager mgr = MarketManager.Instance;
			List<Faction> candidates = TraderFactions().Where(f => f.def.caravanTraderKinds.Any(k => TraderKindAllowed(k, f, map))).ToList();
			if (mgr != null) candidates.RemoveAll(f => mgr.RefusalChance(f) > 0f || !mgr.FairInvited(f));
			candidates.RemoveAll(f => !MarketTruce.Welcome(map, f) || !Routes.TradeRouteUtility.CaravansCanReach(f, map));
			if (candidates.Count == 0) return null;
			List<Faction> fresh = candidates.Where(f => already == null || !already.Contains(f)).ToList();
			List<Faction> pool = fresh.Count > 0 ? fresh : candidates;
			if (!pool.TryRandomElementByWeight(f => CaravanWeight(mgr?.OpinionOf(f)), out Faction faction)) return null;
			TraderKindDef kind = PickTraderKind(faction, map, mgr?.OpinionOf(faction).RarityBias ?? 0f);
			if (kind == null) return null;
			IncidentDef def = IncidentDefOf.TraderCaravanArrival;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			parms.faction = faction;
			parms.traderKind = kind;
			return def.Worker.TryExecute(parms) ? faction : null;
		}

		public static bool TolledToday(Faction faction)
		{
			if (faction == null || !RimTollBridge.Available) return false;
			int last = RimTollBridge.LastTollTick(faction);
			return last >= 0 && Find.TickManager.TicksGame - last < 60000;
		}
	}
}
