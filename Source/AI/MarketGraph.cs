using System;
using System.Collections.Generic;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public static class MarketGraph
	{
		private const int SafeErrorKey = 0x524D4B01;

		private static bool warned;

		private static bool Safe(Func<bool> check, bool fallback)
		{
			try
			{
				return check();
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] a market check failed on a trade caravan: " + e, SafeErrorKey);
				return fallback;
			}
		}

		private static void SoftenTrapChecks(StateGraph graph)
		{
			for (int i = 0; i < graph.transitions.Count; i++)
			{
				List<Trigger> triggers = graph.transitions[i].triggers;
				for (int j = 0; j < triggers.Count; j++)
				{
					if (triggers[j].GetType() == typeof(Trigger_PawnCannotReachMapEdge)) triggers[j] = new Trigger_MarketTrapped();
					else if (triggers[j].GetType() == typeof(Trigger_PawnHarmed)) triggers[j] = new Trigger_MarketHarmed();
				}
			}
		}

		public static void Graft(LordJob_TradeWithColony job, StateGraph graph, IntVec3 chillSpot, Faction faction)
		{
			Lord lord = job?.lord;
			if (lord == null || graph == null || faction == null) return;

			LordToil travel = null;
			LordToil exitEscort = null;
			LordToil exitFighting = null;
			var defends = new List<LordToil_DefendPoint>();
			var exits = new List<LordToil_ExitMap>();
			for (int i = 0; i < graph.lordToils.Count; i++)
			{
				LordToil t = graph.lordToils[i];
				if (t is LordToil_Travel) { if (travel == null) travel = t; }
				else if (t is LordToil_DefendPoint dp) defends.Add(dp);
				else if (t is LordToil_ExitMapAndEscortCarriers) { if (exitEscort == null) exitEscort = t; }
				else if (t is LordToil_ExitMapTraderFighting) { if (exitFighting == null) exitFighting = t; }
				else if (t is LordToil_ExitMap em) exits.Add(em);
			}
			LordToil halt = null;
			LordToil defendAttack = null;
			for (int i = 0; i < defends.Count; i++)
			{
				if (halt == null && defends[i].FlagLoc == chillSpot) halt = defends[i];
				else if (defendAttack == null) defendAttack = defends[i];
			}
			if (halt == null && defends.Count >= 2)
			{
				defendAttack = defends[0];
				halt = defends[1];
			}
			LordToil exitDig = exits.Count > 1 ? exits[1] : (exits.Count == 1 ? exits[0] : null);
			if (travel == null || halt == null || defendAttack == null || exitEscort == null || exitDig == null || exitFighting == null)
			{
				if (!warned)
				{
					warned = true;
					Log.Warning("[RimMarket] the trade caravan graph does not look like vanilla, stalls disabled for this lord.");
				}
				return;
			}

			SoftenTrapChecks(graph);

			Pawn trader = TraderCaravanUtility.FindTrader(lord);
			var stall = new LordToil_MarketStall();
			graph.AddToil(stall);

			var backToStall = new Transition(travel, stall);
			backToStall.AddSource(halt);
			backToStall.AddTrigger(new Trigger_TickCondition(() => Safe(
				() => MarketManager.Instance != null && MarketManager.Instance.VisitFor(lord) != null, false), 15));
			backToStall.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(backToStall);

			var goStall = new Transition(travel, stall);
			goStall.AddSource(halt);
			goStall.AddTrigger(new Trigger_TickCondition(() => Safe(
				() => MarketManager.Instance != null && MarketManager.Instance.WantsStall(lord), false), 120));
			goStall.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(goStall);

			var noStall = new Transition(stall, travel);
			noStall.AddTrigger(new Trigger_TickCondition(() => Safe(
				() => MarketManager.Instance == null || MarketManager.Instance.VisitFor(lord) == null, true), 60));
			noStall.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(noStall);

			var done = new Transition(stall, exitEscort);
			done.AddTrigger(new Trigger_TickCondition(() => Safe(
				() => MarketManager.Instance != null && MarketManager.Instance.VisitShouldEnd(lord, normal: true), false), 60));
			done.AddPreAction(new TransitionAction_Custom((Action)delegate
			{
				MarketManager.Instance?.Notify_LeavingStall(lord, normal: true);
			}));
			done.AddPreAction(new TransitionAction_CheckGiveGift());
			done.AddPostAction(new TransitionAction_WakeAll());
			done.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(done);

			var forced = new Transition(stall, exitEscort);
			forced.AddTrigger(new Trigger_TickCondition(() => Safe(
				() => MarketManager.Instance != null && MarketManager.Instance.VisitShouldEnd(lord, normal: false), true), 60));
			forced.AddTrigger(new Trigger_Custom((TriggerSignal s) => s.type == TriggerSignalType.Tick && (trader?.mindState?.traderDismissed ?? false)));
			forced.AddTrigger(new Trigger_PawnExperiencingDangerousTemperatures());
			forced.AddTrigger(new Trigger_PawnExperiencingAnomalousWeather());
			var conditions = new Trigger_GameCondition();
			conditions.gameConditions = DefDatabase<GameConditionDef>.AllDefsListForReading.FindAll(x => x.causesTraderCaravanExit);
			forced.AddTrigger(conditions);
			forced.AddTrigger(new Trigger_ImportantTraderCaravanPeopleLost());
			forced.AddTrigger(new Trigger_BecamePlayerEnemy());
			forced.AddPreAction(new TransitionAction_Custom((Action)delegate
			{
				MarketManager.Instance?.Notify_LeavingStall(lord, normal: false);
			}));
			forced.AddPostAction(new TransitionAction_WakeAll());
			forced.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(forced);

			var harmed = new Transition(stall, defendAttack);
			harmed.AddTrigger(new Trigger_MarketHarmed());
			harmed.AddPreAction(new TransitionAction_SetDefendTrader());
			harmed.AddPostAction(new TransitionAction_WakeAll());
			harmed.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(harmed);

			var trapped = new Transition(stall, exitDig);
			trapped.AddTrigger(new Trigger_MarketTrapped());
			trapped.AddPostAction(new TransitionAction_Message("MessageVisitorsTrappedLeaving".Translate(faction.def.pawnsPlural.CapitalizeFirst(), faction.Name)));
			trapped.AddPostAction(new TransitionAction_WakeAll());
			trapped.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(trapped);

			var losses = new Transition(stall, exitFighting);
			losses.AddTrigger(new Trigger_FractionPawnsLost(0.2f));
			losses.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(losses);
		}
	}
}
