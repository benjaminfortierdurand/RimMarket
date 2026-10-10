using System.Collections.Generic;
using RimMarket.Market;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Away
{
	public class CaravanArrivalAction_HoldStall : CaravanArrivalAction
	{
		private Settlement settlement;

		public override string Label => "RimMarket.AwayOption".Translate(settlement.Label);

		public override string ReportString => "RimMarket.AwayReport".Translate(settlement.Label);

		public CaravanArrivalAction_HoldStall()
		{
		}

		public CaravanArrivalAction_HoldStall(Settlement settlement)
		{
			this.settlement = settlement;
		}

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
		{
			FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
			if (!report) return report;
			if (settlement != null && settlement.Tile != destinationTile) return false;
			return AwayStallUtility.CanHoldStall(caravan, settlement);
		}

		public override void Arrived(Caravan caravan)
		{
			MarketManager.Instance?.BeginAwayStall(caravan, settlement);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref settlement, "settlement");
		}

		public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, Settlement settlement)
		{
			return CaravanArrivalActionUtility.GetFloatMenuOptions(
				() => AwayStallUtility.CanHoldStall(caravan, settlement),
				() => new CaravanArrivalAction_HoldStall(settlement),
				"RimMarket.AwayOption".Translate(settlement.Label),
				caravan, settlement.Tile, settlement);
		}
	}
}
