using Verse;

namespace RimMarket.AI
{
	public class JobGiver_CallAuction : JobGiver_StandAtStall
	{
		protected override JobDef StandDef => RimMarketDefOf.RimMarket_CallAuction;
	}
}
