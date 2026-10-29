using RimWorld;
using Verse;
using Verse.AI;

namespace RimMarket
{
	[DefOf]
	public static class RimMarketDefOf
	{
		public static ThingDef RimMarket_MarketMarker;

		public static ThingDef RimMarket_Stall;

		public static DutyDef RimMarket_TendStall;
		public static DutyDef RimMarket_CallAuctionDuty;

		public static DutyDef RimMarket_ParkAtStall;

		public static DutyDef RimMarket_GuardPost;

		public static DutyDef RimMarket_WaitAtCamp;

		public static ThingDef RimMarket_CaravanCamp;

		public static ThingDef RimMarket_GuardRestSpot;

		public static DutyDef RimMarket_RestAtSpotDuty;

		public static DutyDef RimMarket_WaitNearCamp;

		public static JobDef RimMarket_RestAtSpot;

		public static JobDef RimMarket_CheckAnimals;

		public static DutyDef RimMarket_ShopAtMarket;

		public static JobDef RimMarket_SleepAtInn;

		public static WorldObjectDef RimMarket_TradeConvoy;

		public static WorldObjectDef RimMarket_ConvoyBattle;

		public static HistoryEventDef RimMarket_ConvoySaved;

		public static HistoryEventDef RimMarket_TradeRoute;

		public static JobDef RimMarket_StandAtStall;
		public static JobDef RimMarket_CallAuction;

		public static JobDef RimMarket_PayStallFee;

		public static JobDef RimMarket_TradeAtStall;

		public static JobDef RimMarket_BrowseStall;

		public static JobDef RimMarket_BuyAtStall;

		public static ThoughtDef RimMarket_BrowsedMarket;

		public static ThoughtDef RimMarket_BoughtAtMarket;

		public static ThoughtDef RimMarket_WentToFair;

		public static ThoughtDef RimMarket_FairStallWon;

		public static HistoryEventDef RimMarket_FairPrize;

		public static HistoryEventDef RimMarket_InnStay;

		public static JobDef RimMarket_Snatch;

		public static DutyDef RimMarket_GuardStall;

		public static JobDef RimMarket_CatchThief;

		public static DutyDef RimMarket_Thief;

		public static HediffDef RimMarket_Subdued;

		public static HistoryEventDef RimMarket_StallRobbed;

		public static HistoryEventDef RimMarket_ThiefCaught;

		public static HistoryEventDef RimMarket_AwayStall;

		public static ThingDef RimMarket_AuctionBlock;

		public static ConceptDef RimMarket_Concept_Guide;

		public static ConceptDef RimMarket_Concept_Stall;

		public static ConceptDef RimMarket_Concept_Spots;

		public static JobDef RimMarket_StandOnBlock;

		public static JobDef RimMarket_AttendAuction;

		public static DutyDef RimMarket_AttendAuctionDuty;

		public static ThoughtDef RimMarket_SlaveAuctionHeld;

		public static InteractionDef RimMarket_ChangeOfGuard;

		public static JobDef RimMarket_TendColonyStall;

		public static WorkTypeDef RimMarket_Shopkeeping;

		public static SoundDef Silver_Drop;

		public static JobDef RimMarket_PostWantedNotice;

		public static FleckDef RimMarket_Petal;

		public static FleckDef RimMarket_SkyLantern;

		public static FleckDef RimMarket_LanternGlow;

		public static FleckDef RimMarket_FireworkRocket;

		public static FleckDef RimMarket_FireworkSpark;

		public static FleckDef RimMarket_FireworkFlash;

		static RimMarketDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(RimMarketDefOf));
		}
	}
}
