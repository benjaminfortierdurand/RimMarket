using RimMarket.Compat;
using Verse;

namespace RimMarket
{
	public class RimMarketSettings : ModSettings
	{
		public const int FeeNone = 0;
		public const int FeeArrival = 1;
		public const int FeeShare = 2;

		public const int DefFeeMode = FeeArrival;
		public const float DefFeePercent = 0.025f;
		public const int DefFeeCap = 250;
		public const float DefSharePercent = 0.10f;
		public const bool DefFeeInGoods = true;
		public const bool DefTurnAwayIfBroke = false;
		public const bool DefWaiveFeeAfterToll = true;
		public const float DefMarketDays = 1f;
		public const int DefMaxMerchants = 3;
		public const int DefGuardsAtStall = 1;
		public const int DefMaxGuards = 4;
		public const int NoGuardLimit = 10;
		public const bool DefHaggleEnabled = true;
		public const float DefHaggleBonus = 0.05f;
		public const bool DefShowMessages = true;
		public const bool DefColonistsBrowse = true;
		public const bool DefGuestsShop = true;
		public const bool DefThievesEnabled = true;
		public const float DefThiefMtbDays = 2.5f;
		public const bool DefReputationEffects = true;
		public const float DefExtraCaravans = 1f;
		public const bool DefAwayStalls = true;
		public const bool DefHegemony = true;
		public const bool DefSlaveAuctions = true;
		public const int DefInnPrice = 20;
		public const bool DefTradeRoutes = true;
		public const bool DefSpecialOrders = true;
		public const float DefRouteOpenFactor = 1f;
		public const float DefRouteTollFactor = 1f;
		public const bool DefShopkeepers = false;
		public const bool DefMarketSaturation = true;
		public const int DefMarketDifficulty = 1;
		public const float DefColonyShopperDays = 1.2f;
		public const bool DefFairEnabled = true;
		public const int DefFairCost = 1500;
		public const int DefFairCooldownDays = 15;
		public const float DefBargainEffect = 1f;

		public int feeMode = DefFeeMode;
		public float feePercent = DefFeePercent;
		public int feeCap = DefFeeCap;
		public float sharePercent = DefSharePercent;
		public bool feeInGoods = DefFeeInGoods;
		public bool turnAwayIfBroke = DefTurnAwayIfBroke;
		public bool waiveFeeAfterToll = DefWaiveFeeAfterToll;
		public float marketDays = DefMarketDays;
		public int maxMerchants = DefMaxMerchants;
		public int guardsAtStall = DefGuardsAtStall;
		public int maxGuards = DefMaxGuards;
		public bool haggleEnabled = DefHaggleEnabled;
		public float haggleBonus = DefHaggleBonus;
		public bool showMessages = DefShowMessages;
		public bool colonistsBrowse = DefColonistsBrowse;
		public bool guestsShop = DefGuestsShop;
		public bool shoppersVisit = true;
		public bool thievesEnabled = DefThievesEnabled;
		public float thiefMtbDays = DefThiefMtbDays;
		public bool reputationEffects = DefReputationEffects;
		public float extraCaravans = DefExtraCaravans;
		public bool awayStalls = DefAwayStalls;
		public bool hegemonyPrices = DefHegemony;
		public bool hegemonyStock = DefHegemony;
		public bool hegemonyGoodwill = DefHegemony;
		public bool hegemonyTerritory = DefHegemony;
		public bool slaveAuctions = DefSlaveAuctions;
		public int innPrice = DefInnPrice;
		public bool tradeRoutes = DefTradeRoutes;
		public bool specialOrders = DefSpecialOrders;
		public float routeOpenFactor = DefRouteOpenFactor;
		public float routeTollFactor = DefRouteTollFactor;
		public bool shopkeepers = DefShopkeepers;
		public bool marketSaturation = DefMarketSaturation;
		public int marketDifficulty = DefMarketDifficulty;
		public float colonyShopperDays = DefColonyShopperDays;
		public bool fairEnabled = DefFairEnabled;
		public int fairCost = DefFairCost;
		public int fairCooldownDays = DefFairCooldownDays;
		public float bargainEffect = DefBargainEffect;

		public static bool ShareModeEnabled => HospitalityBridge.Active || HotelsBridge.Active;

		public int GuardCap => maxGuards >= NoGuardLimit ? int.MaxValue : System.Math.Max(maxGuards, guardsAtStall + 1);

		public int MarketTicks => (int)(marketDays * 60000f);

		public int FeeModeSafe => Sanitize(feeMode);

		public static int Sanitize(int mode)
		{
			if (mode == FeeShare && !ShareModeEnabled) return FeeArrival;
			if (mode < FeeNone || mode > FeeShare) return DefFeeMode;
			return mode;
		}

		public static int NextMode(int mode)
		{
			int next = mode + 1;
			if (next == FeeShare && !ShareModeEnabled) next++;
			return next > FeeShare ? FeeNone : next;
		}

		public static string FeeModeLabel(int mode)
		{
			switch (mode)
			{
				case FeeNone: return "RimMarket.FeeModeNone".Translate();
				case FeeShare: return "RimMarket.FeeModeShare".Translate();
				default: return "RimMarket.FeeModeArrival".Translate();
			}
		}

		public void ResetMarket()
		{
			marketDifficulty = DefMarketDifficulty;
			feeMode = DefFeeMode;
			feePercent = DefFeePercent;
			feeCap = DefFeeCap;
			sharePercent = DefSharePercent;
			feeInGoods = DefFeeInGoods;
			turnAwayIfBroke = DefTurnAwayIfBroke;
			waiveFeeAfterToll = DefWaiveFeeAfterToll;
			marketDays = DefMarketDays;
			maxMerchants = DefMaxMerchants;
			guardsAtStall = DefGuardsAtStall;
			maxGuards = DefMaxGuards;
			haggleEnabled = DefHaggleEnabled;
			haggleBonus = DefHaggleBonus;
		}

		public void ResetVisitors()
		{
			colonistsBrowse = DefColonistsBrowse;
			guestsShop = DefGuestsShop;
			shoppersVisit = true;
			colonyShopperDays = DefColonyShopperDays;
			bargainEffect = DefBargainEffect;
			shopkeepers = DefShopkeepers;
			marketSaturation = DefMarketSaturation;
			innPrice = DefInnPrice;
		}

		public void ResetEvents()
		{
			thievesEnabled = DefThievesEnabled;
			thiefMtbDays = DefThiefMtbDays;
			slaveAuctions = DefSlaveAuctions;
			fairEnabled = DefFairEnabled;
			fairCost = DefFairCost;
			fairCooldownDays = DefFairCooldownDays;
			reputationEffects = DefReputationEffects;
			extraCaravans = DefExtraCaravans;
			showMessages = DefShowMessages;
		}

		public void ResetTrade()
		{
			tradeRoutes = DefTradeRoutes;
			routeOpenFactor = DefRouteOpenFactor;
			routeTollFactor = DefRouteTollFactor;
			specialOrders = DefSpecialOrders;
			awayStalls = DefAwayStalls;
			hegemonyPrices = DefHegemony;
			hegemonyStock = DefHegemony;
			hegemonyGoodwill = DefHegemony;
			hegemonyTerritory = DefHegemony;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref feeMode, "feeMode", DefFeeMode);
			Scribe_Values.Look(ref feePercent, "feePercent", DefFeePercent);
			Scribe_Values.Look(ref feeCap, "feeCap", DefFeeCap);
			Scribe_Values.Look(ref sharePercent, "sharePercent", DefSharePercent);
			Scribe_Values.Look(ref feeInGoods, "feeInGoods", DefFeeInGoods);
			Scribe_Values.Look(ref turnAwayIfBroke, "turnAwayIfBroke", DefTurnAwayIfBroke);
			Scribe_Values.Look(ref waiveFeeAfterToll, "waiveFeeAfterToll", DefWaiveFeeAfterToll);
			Scribe_Values.Look(ref marketDays, "marketDays", DefMarketDays);
			Scribe_Values.Look(ref maxMerchants, "maxMerchants", DefMaxMerchants);
			Scribe_Values.Look(ref guardsAtStall, "guardsAtStall", DefGuardsAtStall);
			Scribe_Values.Look(ref maxGuards, "maxGuards", DefMaxGuards);
			Scribe_Values.Look(ref haggleEnabled, "haggleEnabled", DefHaggleEnabled);
			Scribe_Values.Look(ref haggleBonus, "haggleBonus", DefHaggleBonus);
			Scribe_Values.Look(ref showMessages, "showMessages", DefShowMessages);
			Scribe_Values.Look(ref colonistsBrowse, "colonistsBrowse", DefColonistsBrowse);
			Scribe_Values.Look(ref guestsShop, "guestsShop", DefGuestsShop);
			Scribe_Values.Look(ref shoppersVisit, "shoppersVisit", true);
			Scribe_Values.Look(ref thievesEnabled, "thievesEnabled", DefThievesEnabled);
			Scribe_Values.Look(ref thiefMtbDays, "thiefMtbDays", DefThiefMtbDays);
			Scribe_Values.Look(ref reputationEffects, "reputationEffects", DefReputationEffects);
			Scribe_Values.Look(ref extraCaravans, "extraCaravans", DefExtraCaravans);
			Scribe_Values.Look(ref awayStalls, "awayStalls", DefAwayStalls);
			Scribe_Values.Look(ref hegemonyPrices, "hegemonyPrices", DefHegemony);
			Scribe_Values.Look(ref hegemonyStock, "hegemonyStock", DefHegemony);
			Scribe_Values.Look(ref hegemonyGoodwill, "hegemonyGoodwill", DefHegemony);
			Scribe_Values.Look(ref hegemonyTerritory, "hegemonyTerritory", DefHegemony);
			Scribe_Values.Look(ref slaveAuctions, "slaveAuctions", DefSlaveAuctions);
			Scribe_Values.Look(ref innPrice, "innPrice", DefInnPrice);
			Scribe_Values.Look(ref tradeRoutes, "tradeRoutes", DefTradeRoutes);
			Scribe_Values.Look(ref specialOrders, "specialOrders", DefSpecialOrders);
			Scribe_Values.Look(ref routeOpenFactor, "routeOpenFactor", DefRouteOpenFactor);
			Scribe_Values.Look(ref routeTollFactor, "routeTollFactor", DefRouteTollFactor);
			Scribe_Values.Look(ref shopkeepers, "shopkeepers", DefShopkeepers);
			Scribe_Values.Look(ref marketSaturation, "marketSaturation", DefMarketSaturation);
			Scribe_Values.Look(ref marketDifficulty, "marketDifficulty", DefMarketDifficulty);
			Scribe_Values.Look(ref colonyShopperDays, "colonyShopperDays", DefColonyShopperDays);
			Scribe_Values.Look(ref fairEnabled, "fairEnabled", DefFairEnabled);
			Scribe_Values.Look(ref fairCost, "fairCost", DefFairCost);
			Scribe_Values.Look(ref fairCooldownDays, "fairCooldownDays", DefFairCooldownDays);
			Scribe_Values.Look(ref bargainEffect, "bargainEffect", DefBargainEffect);
		}
	}
}
