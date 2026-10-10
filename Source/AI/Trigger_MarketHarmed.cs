using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.AI
{
	public class Trigger_MarketHarmed : Trigger_PawnHarmed
	{
		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (!SignalIsHarm(signal) || Quarrel(lord, signal)) return false;
			return base.ActivateOn(lord, signal);
		}

		private static bool Quarrel(Lord lord, TriggerSignal signal)
		{
			Pawn victim = signal.Pawn;
			if (victim?.MentalStateDef == MentalStateDefOf.SocialFighting) return true;
			if (!(signal.dinfo.Instigator is Pawn by)) return false;
			if (by.MentalStateDef == MentalStateDefOf.SocialFighting) return true;
			if (by.GetLord() == lord) return true;
			return victim != null && by.Faction != null && by.Faction == victim.Faction;
		}
	}
}
