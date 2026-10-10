using RimMarket.Market;
using RimMarket.Thieves;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket.UI
{
	public class FloatMenuOptionProvider_TackleThief : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
		{
			if (!MarketManager.AnyThefts || !ThiefUtility.IsFleeingThief(clickedPawn)) return null;
			Pawn colonist = context.FirstSelectedPawn;
			if (colonist == null || !colonist.IsColonistPlayerControlled) return null;
			string label = "RimMarket.TackleThief".Translate(clickedPawn.LabelShort);
			if (colonist.WorkTagIsDisabled(WorkTags.Violent))
				return new FloatMenuOption(label + ": " + "RimMarket.TackleIncapable".Translate(colonist.LabelShortCap), null);
			if (!colonist.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
				return new FloatMenuOption(label + ": " + "NoPath".Translate().CapitalizeFirst(), null);
			Pawn thief = clickedPawn;
			var option = new FloatMenuOption(label, delegate
			{
				Job job = JobMaker.MakeJob(RimMarketDefOf.RimMarket_CatchThief, thief);
				job.locomotionUrgency = LocomotionUrgency.Sprint;
				job.playerForced = true;
				colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}, MenuOptionPriority.AttackEnemy, null, thief);
			return FloatMenuUtility.DecoratePrioritizedTask(option, colonist, thief);
		}
	}
}
