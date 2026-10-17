using RimMarket.Routes;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class LordJob_ConvoyCamp : LordJob
	{
		public const string MemoLeave = "RimMarket_ConvoyLeave";

		private IntVec3 center;
		private float radius;

		public override bool AddFleeToil => false;

		public LordJob_ConvoyCamp()
		{
		}

		public LordJob_ConvoyCamp(IntVec3 center, float radius)
		{
			this.center = center;
			this.radius = radius;
		}

		public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
		{
			if (reason == PawnLostCondition.Incapped) return false;
			return base.ShouldRemovePawn(p, reason);
		}

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();
			var camp = new LordToil_ConvoyCamp(center, radius);
			graph.StartingToil = camp;
			var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: false, interruptCurrentJob: true);
			graph.AddToil(leave);
			var go = new Transition(camp, leave);
			go.AddTrigger(new Trigger_Memo(MemoLeave));
			go.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(go);
			return graph;
		}

		public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
		{
			base.Notify_PawnLost(p, condition);
			if (condition == PawnLostCondition.ExitedMap && lord?.Map?.Parent is ConvoyBattle battle) battle.Notify_PawnLeft(p);
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref center, "center");
			Scribe_Values.Look(ref radius, "radius");
		}
	}

	public class LordToil_ConvoyCamp : LordToil_DefendPoint
	{
		private const float InnerRadius = 3f;

		public override bool AllowSatisfyLongNeeds => false;

		public override float? CustomWakeThreshold => 0.5f;

		public LordToil_ConvoyCamp()
		{
		}

		public LordToil_ConvoyCamp(IntVec3 center, float radius)
			: base(center, radius, InnerRadius)
		{
		}

		public override void UpdateAllDuties()
		{
			LordToilData_DefendPoint d = Data;
			Pawn trader = TraderCaravanUtility.FindTrader(lord);
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				TraderCaravanRole role = p.GetTraderCaravanRole();
				if (role == TraderCaravanRole.Guard)
				{
					p.mindState.duty = Defend(d.defendPoint, d.defendRadius);
					continue;
				}
				if (p == trader || trader == null || !trader.Spawned)
				{
					p.mindState.duty = Defend(d.defendPoint, InnerRadius);
					continue;
				}
				p.mindState.duty = new PawnDuty(role == TraderCaravanRole.Chattel ? DutyDefOf.Escort : DutyDefOf.Follow, trader, InnerRadius);
				p.mindState.duty.locomotion = LocomotionUrgency.Walk;
			}
		}

		private static PawnDuty Defend(IntVec3 point, float radius)
		{
			return new PawnDuty(DutyDefOf.Defend, point)
			{
				focusSecond = point,
				radius = radius,
				wanderRadius = radius - 1f,
			};
		}
	}
}
