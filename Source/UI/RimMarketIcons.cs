using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	[StaticConstructorOnStartup]
	public static class RimMarketIcons
	{
		public static readonly Texture2D Trade = Load("RimMarket_Trade");
		public static readonly Texture2D OpenMarket = Load("RimMarket_OpenMarket");
		public static readonly Texture2D CloseMarket = Load("RimMarket_CloseMarket");
		public static readonly Texture2D FeeMode = Load("RimMarket_FeeMode");
		public static readonly Texture2D ShowMarker = Load("RimMarket_ShowMarker");
		public static readonly Texture2D Bid = Load("RimMarket_Bid");
		public static readonly Texture2D Reputation = Load("RimMarket_Reputation");
		public static readonly Texture2D ColonyStall = Load("RimMarket_ColonyStall");
		public static readonly Texture2D GreatFair = Load("RimMarket_GreatFair");
		public static readonly Texture2D PaintAwning = Load("RimMarket_PaintAwning");
		public static readonly Texture2D WantedNotice = Load("RimMarket_WantedNotice");
		public static readonly Texture2D Deals = Load("RimMarket_Deals");
		public static readonly Texture2D AreaBadge = Load("RimMarket_AreaBadge");
		public static readonly Texture2D StallSetup =
			ContentFinder<Texture2D>.Get("UI/Commands/SellableItems", reportFailure: false) ?? Trade;
		public static readonly Texture2D Plus =
			ContentFinder<Texture2D>.Get("UI/Buttons/Plus", reportFailure: false) ?? Trade;
		public static readonly Texture2D Minus =
			ContentFinder<Texture2D>.Get("UI/Buttons/Minus", reportFailure: false) ?? Trade;
		public static readonly Texture2D AreaExpand =
			ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand", reportFailure: false) ?? Plus;
		public static readonly Texture2D AreaClear =
			ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedClear", reportFailure: false) ?? Minus;
		public static readonly Texture2D ConvoyAttacked =
			ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/RimMarket_TradeConvoyAttacked", reportFailure: false);

		private static Texture2D Load(string name)
			=> ContentFinder<Texture2D>.Get("UI/Commands/" + name, reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimMarket_Trade", reportFailure: false)
				?? TexCommand.Attack;
	}
}
