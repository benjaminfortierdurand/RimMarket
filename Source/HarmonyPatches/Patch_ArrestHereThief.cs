using System.Reflection;
using HarmonyLib;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch]
	public static class Patch_ArrestHereThief
	{
		private static MethodBase target;
		private static JobDef arrestJob;

		public static bool Prepare()
		{
			System.Type type = AccessTools.TypeByName("CapturedPersons.FloatMenuOptionProvider_ArrestHere");
			target = type == null ? null : AccessTools.Method(type, "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) });
			return target != null;
		}

		public static MethodBase TargetMethod() => target;

		public static void Postfix(FloatMenuOption __result, Pawn clickedPawn, FloatMenuContext context)
		{
			if (__result == null || !ThiefUtility.IsCulprit(clickedPawn)) return;
			string warn = ": " + "AngersFaction".Translate().CapitalizeFirst();
			string label = __result.Label;
			if (label != null && label.Contains(warn))
				__result.Label = label.Replace(warn, ": " + "RimMarket.CaughtRedHanded".Translate());
			if (arrestJob == null) arrestJob = DefDatabase<JobDef>.GetNamedSilentFail("CP_ImprisonInPlace");
			Pawn arrester = context?.FirstSelectedPawn;
			if (arrestJob == null || arrester == null || __result.action == null || __result.Disabled) return;
			JobDef def = arrestJob;
			__result.action = delegate
			{
				Job job = JobMaker.MakeJob(def, clickedPawn);
				job.count = 1;
				arrester.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
			};
		}
	}
}
