using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.Orders
{
	public class WantedComm : ICommunicable
	{
		public static readonly WantedComm Instance = new WantedComm();

		public string GetCallLabel() => "RimMarket.WantedCallLabel".Translate();

		public string GetInfoText() => "RimMarket.WantedCallInfo".Translate();

		public void TryOpenComms(Pawn negotiator)
		{
			if (negotiator?.Map != null) Find.WindowStack.Add(new Dialog_WantedNotice(negotiator.Map));
		}

		public Faction GetFaction() => null;

		public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
		{
			if (Market.MarketManager.Instance?.Notice != null)
				return new FloatMenuOption("RimMarket.WantedCallBoard".Translate(), () => Find.WindowStack.Add(new Dialog_WantedBoard()));
			string label = GetCallLabel();
			string why = WantedUtility.CannotPost(negotiator?.Map);
			if (why != null) return new FloatMenuOption(label + " (" + why + ")", null);
			return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
			{
				Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_PostWantedNotice, console);
				negotiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}, MenuOptionPriority.InitiateSocial), negotiator, console);
		}

		public static IEnumerable<ICommunicable> With(IEnumerable<ICommunicable> targets, Pawn pawn)
		{
			if (targets == null || !RimMarketMod.Settings.specialOrders || pawn?.Map == null || !pawn.Map.IsPlayerHome) return targets;
			return targets.Concat(new ICommunicable[] { Instance });
		}
	}

	public class JobDriver_PostWantedNotice : JobDriver
	{
		private const int WriteTicks = 120;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => job.targetA.Thing is Building_CommsConsole c && !c.CanUseCommsNow);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
			yield return Toils_General.Wait(WriteTicks).WithProgressBarToilDelay(TargetIndex.A);
			yield return new Toil
			{
				initAction = () => WantedComm.Instance.TryOpenComms(pawn),
				defaultCompleteMode = ToilCompleteMode.Instant,
			};
		}
	}

	[HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
	public static class Patch_CommTargetsWanted
	{
		public static void Postfix(Pawn myPawn, ref IEnumerable<ICommunicable> __result)
		{
			__result = WantedComm.With(__result, myPawn);
		}
	}

	[HarmonyPatch]
	public static class Patch_MessengerTargetsWanted
	{
		private static MethodBase target;

		public static bool Prepare()
		{
			if (target != null) return true;
			Type t = GenTypes.GetTypeInAnyAssembly("MedievalOverhaul.Building_ScribeTable");
			target = t == null ? null : AccessTools.Method(t, "GetCommTargets_Messenger");
			return target != null;
		}

		public static MethodBase TargetMethod() => target;

		public static void Postfix(Pawn myPawn, ref IEnumerable<ICommunicable> __result)
		{
			__result = WantedComm.With(__result, myPawn);
		}
	}
}
