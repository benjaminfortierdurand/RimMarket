using System.Collections.Generic;
using Verse;

namespace RimMarket.Routes
{
	public class ChoiceLetter_ConvoyBlocked : ChoiceLetter
	{
		public TradeConvoy convoy;

		private bool Live => convoy != null && !convoy.Destroyed && convoy.Waiting && !convoy.letIn;

		public override bool CanShowInLetterStack => base.CanShowInLetterStack && Live;

		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				if (ArchivedOnly || !Live)
				{
					yield return Option_Close;
					yield break;
				}
				yield return new DiaOption("RimMarket.ConvoyLetIn".Translate())
				{
					action = () =>
					{
						TradeConvoy c = convoy;
						c.letIn = true;
						Find.LetterStack.RemoveLetter(this);
						c.ArriveNow();
					},
					resolveTree = true,
				};
				yield return new DiaOption("RimMarket.ConvoyKeepWaiting".Translate())
				{
					action = () => Find.LetterStack.RemoveLetter(this),
					resolveTree = true,
				};
				yield return Option_Postpone;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.Saving && convoy != null && convoy.Destroyed) convoy = null;
			Scribe_References.Look(ref convoy, "convoy");
		}
	}
}
