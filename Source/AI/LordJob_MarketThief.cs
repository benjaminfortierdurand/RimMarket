using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class LordJob_MarketThief : LordJob
	{
		public const string MemoLeave = "RimMarket_ThiefLeave";
		private const int TimeoutTicks = 20000;

		private int loiterTicks = 2500;
		private bool bold;

		public int LoiterTicks => loiterTicks;

		public bool Bold => bold;

		public override bool AddFleeToil => false;

		public LordJob_MarketThief()
		{
		}

		public LordJob_MarketThief(int loiterTicks, bool bold = false)
		{
			this.loiterTicks = loiterTicks;
			this.bold = bold;
		}

		public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
		{
			if (reason == PawnLostCondition.Incapped) return false;
			return base.ShouldRemovePawn(p, reason);
		}

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();
			var work = new LordToil_MarketThief();
			graph.StartingToil = work;
			var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: false, interruptCurrentJob: true);
			graph.AddToil(leave);
			var done = new Transition(work, leave);
			done.AddTrigger(new Trigger_Memo(MemoLeave));
			done.AddTrigger(new Trigger_TicksPassed(TimeoutTicks));
			done.AddTrigger(new Trigger_BecamePlayerEnemy());
			done.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(done);
			return graph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref loiterTicks, "loiterTicks", 2500);
			Scribe_Values.Look(ref bold, "bold");
		}
	}

	public class LordToil_MarketThief : LordToil
	{
		private const int RefreshInterval = 1000;
		private const int CheckInterval = 250;

		public override bool AllowSatisfyLongNeeds => false;

		public override void UpdateAllDuties()
		{
			Map map = lord.Map;
			IntVec3 focus = ThiefUtility.MarketFocus(map);
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null) continue;
				IntVec3 cell = focus.IsValid ? focus : p.Position;
				p.mindState.duty = new PawnDuty(RimMarketDefOf.RimMarket_Thief, cell) { locomotion = LocomotionUrgency.Walk };
			}
		}

		public override void LordToilTick()
		{
			int now = Find.TickManager.TicksGame;
			if (now % CheckInterval != 0) return;
			if (!ThiefUtility.AnyTargetOnMap(lord.Map))
			{
				lord.ReceiveMemo(LordJob_MarketThief.MemoLeave);
				return;
			}
			if (now % RefreshInterval == 0) UpdateAllDuties();
		}
	}
}
