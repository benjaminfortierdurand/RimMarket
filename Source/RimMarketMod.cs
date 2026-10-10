using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimMarket
{
	public class RimMarketMod : Mod
	{
		public const string HarmonyId = "forti.rimmarket";

		public static RimMarketSettings Settings { get; private set; }

		public RimMarketMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<RimMarketSettings>();
			var harmony = new Harmony(HarmonyId);
			harmony.PatchAll();
			Log.Message("[RimMarket] loaded, the stalls are open.");
		}

		public override string SettingsCategory() => "RimMarket";

		private static Vector2 scroll;
		private static float viewHeight = 900f;
		private const float SubIndent = 24f;

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var view = new Rect(0f, 0f, inRect.width - 16f, viewHeight);
			Widgets.BeginScrollView(inRect, ref scroll, view);
			var l = new Listing_Standard { maxOneColumn = true };
			l.Begin(view);
			RimMarketSettings s = Settings;

			l.Label("RimMarket.Settings.Intro".Translate());
			l.GapLine();

			l.Label("RimMarket.Settings.FeeMode".Translate() + ": " + RimMarketSettings.FeeModeLabel(s.FeeModeSafe));
			if (l.ButtonText("RimMarket.Settings.FeeModeCycle".Translate()))
				s.feeMode = RimMarketSettings.NextMode(s.FeeModeSafe);

			l.Label("RimMarket.Settings.FeePercent".Translate() + ": " + (s.feePercent * 100f).ToString("F0") + "%");
			s.feePercent = l.Slider(s.feePercent, 0.01f, 0.15f);

			l.Label("RimMarket.Settings.FeeCap".Translate() + ": " + s.feeCap);
			s.feeCap = 10 * Mathf.RoundToInt(l.Slider(s.feeCap, 50f, 1500f) / 10f);

			if (RimMarketSettings.ShareModeEnabled)
			{
				l.Label("RimMarket.Settings.SharePercent".Translate() + ": " + (s.sharePercent * 100f).ToString("F0") + "%");
				s.sharePercent = l.Slider(s.sharePercent, 0.02f, 0.40f);
			}

			l.CheckboxLabeled("RimMarket.Settings.FeeInGoods".Translate(), ref s.feeInGoods,
				"RimMarket.Settings.FeeInGoodsHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.TurnAwayIfBroke".Translate(), ref s.turnAwayIfBroke,
				"RimMarket.Settings.TurnAwayIfBrokeHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.WaiveAfterToll".Translate(), ref s.waiveFeeAfterToll,
				"RimMarket.Settings.WaiveAfterTollHint".Translate());

			l.GapLine();
			l.Label("RimMarket.Settings.MarketDays".Translate() + ": " + s.marketDays.ToString("F2"));
			s.marketDays = Mathf.Round(l.Slider(s.marketDays, 0.5f, 3f) * 4f) / 4f;

			l.Label("RimMarket.Settings.MaxMerchants".Translate() + ": " + s.maxMerchants);
			s.maxMerchants = Mathf.RoundToInt(l.Slider(s.maxMerchants, 1f, 8f));

			l.Label("RimMarket.Settings.GuardsAtStall".Translate() + ": " + s.guardsAtStall, tooltip: "RimMarket.Settings.GuardsAtStallHint".Translate());
			s.guardsAtStall = Mathf.RoundToInt(l.Slider(s.guardsAtStall, 0f, 3f));

			l.GapLine();
			l.CheckboxLabeled("RimMarket.Settings.Haggle".Translate(), ref s.haggleEnabled,
				"RimMarket.Settings.HaggleHint".Translate());
			l.Label("RimMarket.Settings.HaggleBonus".Translate() + ": " + (s.haggleBonus * 100f).ToString("F0") + "%");
			s.haggleBonus = Mathf.Round(l.Slider(s.haggleBonus, 0.02f, 0.10f) * 100f) / 100f;

			l.GapLine();
			l.CheckboxLabeled("RimMarket.Settings.ColonistsBrowse".Translate(), ref s.colonistsBrowse,
				"RimMarket.Settings.ColonistsBrowseHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.GuestsShop".Translate(), ref s.guestsShop,
				"RimMarket.Settings.GuestsShopHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.ShoppersVisit".Translate(), ref s.shoppersVisit,
				"RimMarket.Settings.ShoppersVisitHint".Translate());
			if (s.shoppersVisit)
			{
				l.Indent(SubIndent);
				l.ColumnWidth -= SubIndent;
				l.Label("RimMarket.Settings.ColonyShopperDays".Translate(s.colonyShopperDays.ToString("0.0")),
					tooltip: "RimMarket.Settings.ColonyShopperDaysHint".Translate());
				s.colonyShopperDays = Mathf.Round(l.Slider(s.colonyShopperDays, 0.3f, 5f) * 10f) / 10f;
				l.Label("RimMarket.Settings.BargainEffect".Translate(s.bargainEffect.ToStringPercent()),
					tooltip: "RimMarket.Settings.BargainEffectHint".Translate());
				s.bargainEffect = Mathf.Round(l.Slider(s.bargainEffect, 0f, 2f) * 20f) / 20f;
				l.ColumnWidth += SubIndent;
				l.Outdent(SubIndent);
			}
			l.Label("RimMarket.Settings.InnPrice".Translate() + ": " + s.innPrice, tooltip: "RimMarket.Settings.InnPriceHint".Translate());
			s.innPrice = 5 * Mathf.RoundToInt(l.Slider(s.innPrice, 0f, 60f) / 5f);
			l.CheckboxLabeled("RimMarket.Settings.Shopkeepers".Translate(), ref s.shopkeepers,
				"RimMarket.Settings.ShopkeepersHint".Translate());

			l.GapLine();
			l.CheckboxLabeled("RimMarket.Settings.Reputation".Translate(), ref s.reputationEffects,
				"RimMarket.Settings.ReputationHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.AwayStalls".Translate(), ref s.awayStalls,
				"RimMarket.Settings.AwayStallsHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.Fair".Translate(), ref s.fairEnabled,
				"RimMarket.Settings.FairHint".Translate());
			if (s.fairEnabled)
			{
				l.Indent(SubIndent);
				l.ColumnWidth -= SubIndent;
				l.Label("RimMarket.Settings.FairCost".Translate(s.fairCost));
				s.fairCost = 100 * Mathf.RoundToInt(l.Slider(s.fairCost, 0f, 5000f) / 100f);
				l.Label("RimMarket.Settings.FairCooldown".Translate(s.fairCooldownDays));
				s.fairCooldownDays = Mathf.RoundToInt(l.Slider(s.fairCooldownDays, 5f, 60f));
				l.ColumnWidth += SubIndent;
				l.Outdent(SubIndent);
			}
			l.CheckboxLabeled("RimMarket.Settings.TradeRoutes".Translate(), ref s.tradeRoutes,
				"RimMarket.Settings.TradeRoutesHint".Translate());
			if (s.tradeRoutes)
			{
				l.Indent(SubIndent);
				l.ColumnWidth -= SubIndent;
				l.Label("RimMarket.Settings.RouteOpenCost".Translate(s.routeOpenFactor.ToStringPercent(), Routes.TradeRouteUtility.OpenPrice(20)));
				s.routeOpenFactor = Mathf.Round(l.Slider(s.routeOpenFactor, 0f, 3f) * 20f) / 20f;
				l.Label("RimMarket.Settings.RouteToll".Translate(s.routeTollFactor.ToStringPercent(), Routes.TradeRouteUtility.EffectiveToll(Routes.TradeRouteUtility.Toll(20, false))));
				s.routeTollFactor = Mathf.Round(l.Slider(s.routeTollFactor, 0f, 3f) * 20f) / 20f;
				l.ColumnWidth += SubIndent;
				l.Outdent(SubIndent);
			}
			l.CheckboxLabeled("RimMarket.Settings.SpecialOrders".Translate(), ref s.specialOrders,
				"RimMarket.Settings.SpecialOrdersHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.SlaveAuctions".Translate(), ref s.slaveAuctions,
				"RimMarket.Settings.SlaveAuctionsHint".Translate());
			l.CheckboxLabeled("RimMarket.Settings.Thieves".Translate(), ref s.thievesEnabled,
				"RimMarket.Settings.ThievesHint".Translate());
			if (s.thievesEnabled)
			{
				l.Label("RimMarket.Settings.ThiefMtb".Translate() + ": " + s.thiefMtbDays.ToString("F1"));
				s.thiefMtbDays = Mathf.Round(l.Slider(s.thiefMtbDays, 0.5f, 10f) * 2f) / 2f;
			}

			l.GapLine();
			l.CheckboxLabeled("RimMarket.Settings.Messages".Translate(), ref s.showMessages);

			l.Gap();
			if (l.ButtonText("RimMarket.Settings.Reset".Translate()))
				s.ResetToDefaults();

			viewHeight = l.CurHeight + 12f;
			l.End();
			Widgets.EndScrollView();
		}
	}
}
