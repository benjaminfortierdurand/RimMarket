using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Routes
{
	public class CaravanArrivalAction_HelpConvoy : CaravanArrivalAction
	{
		private TradeConvoy convoy;

		public override string Label => "RimMarket.HelpConvoy".Translate();

		public override string ReportString => "RimMarket.HelpingConvoy".Translate(TradeRouteUtility.FromName(convoy));

		public CaravanArrivalAction_HelpConvoy()
		{
		}

		public CaravanArrivalAction_HelpConvoy(TradeConvoy convoy)
		{
			this.convoy = convoy;
		}

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
		{
			FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
			if (!report) return report;
			if (convoy == null || convoy.Tile != destinationTile) return false;
			return ConvoyBattleUtility.CanHelp(convoy);
		}

		public override void Arrived(Caravan caravan)
		{
			ConvoyBattleUtility.Arrive(convoy, caravan);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref convoy, "convoy");
		}
	}
}
