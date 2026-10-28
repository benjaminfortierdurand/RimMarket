using System.Collections.Generic;
using HarmonyLib;
using RimMarket.Compat;
using RimMarket.UI;
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

		private const int TabMarket = 0;
		private const int TabVisitors = 1;
		private const int TabEvents = 2;
		private const int TabTrade = 3;
		private const float RowHeight = 30f;
		private const float CaptionHeight = 28f;
		private const float SubIndent = 24f;
		private const float ValueWidth = 70f;
		private const float SliderShare = 0.36f;
		private const float FooterHeight = 40f;
		private const float ResetWidth = 180f;
		private const float GuideWidth = 220f;

		private static int tab;
		private static Vector2 scroll;
		private static float viewHeight = 600f;
		private float y;
		private float width;

		public override void DoSettingsWindowContents(Rect inRect)
		{
			RimMarketSettings s = Settings;
			var tabs = new List<TabRecord>
			{
				new TabRecord("RimMarket.Settings.TabMarket".Translate(), () => Select(TabMarket), tab == TabMarket),
				new TabRecord("RimMarket.Settings.TabVisitors".Translate(), () => Select(TabVisitors), tab == TabVisitors),
				new TabRecord("RimMarket.Settings.TabEvents".Translate(), () => Select(TabEvents), tab == TabEvents),
				new TabRecord("RimMarket.Settings.TabTrade".Translate(), () => Select(TabTrade), tab == TabTrade),
			};
			Rect menu = inRect;
			menu.yMin += TabDrawer.TabHeight;
			Widgets.DrawMenuSection(menu);
			TabDrawer.DrawTabs(menu, tabs);
			Rect inner = menu.ContractedBy(12f);
			var footer = new Rect(inner.x, inner.yMax - FooterHeight + 8f, inner.width, FooterHeight - 8f);
			inner.yMax -= FooterHeight;
			var view = new Rect(0f, 0f, inner.width - 16f, Mathf.Max(viewHeight, inner.height - 1f));
			Widgets.BeginScrollView(inner, ref scroll, view);
			y = 0f;
			width = view.width;
			switch (tab)
			{
				case TabVisitors: VisitorsTab(s); break;
				case TabEvents: EventsTab(s); break;
				case TabTrade: TradeTab(s); break;
				default: MarketTab(s); break;
			}
			viewHeight = y;
			Widgets.EndScrollView();
			if (Widgets.ButtonText(new Rect(footer.x, footer.y, GuideWidth, footer.height), "RimMarket.Settings.OpenGuide".Translate()))
				Window_MarketGuide.Open();
			if (Widgets.ButtonText(new Rect(footer.xMax - ResetWidth, footer.y, ResetWidth, footer.height), "RimMarket.Settings.ResetTab".Translate()))
			{
				switch (tab)
				{
					case TabVisitors: s.ResetVisitors(); break;
					case TabEvents: s.ResetEvents(); break;
					case TabTrade: s.ResetTrade(); break;
					default: s.ResetMarket(); break;
				}
			}
		}

		private static void Select(int t)
		{
			tab = t;
			scroll = Vector2.zero;
		}

		private void MarketTab(RimMarketSettings s)
		{
			DifficultyRow(s);
			FeeModeRow(s);
			int mode = s.FeeModeSafe;
			if (mode == RimMarketSettings.FeeArrival)
			{
				s.feePercent = Slider("FeePercent", s.feePercent, 0.01f, 0.15f, 0.005f, s.feePercent.ToStringPercent("0.#"), Hint("FeePercent"), SubIndent);
				s.feeCap = Mathf.RoundToInt(Slider("FeeCap", s.feeCap, 50f, 1500f, 10f, s.feeCap.ToString(), Hint("FeeCap"), SubIndent));
			}
			if (mode == RimMarketSettings.FeeShare)
				s.sharePercent = Slider("SharePercent", s.sharePercent, 0.02f, 0.40f, 0.01f, s.sharePercent.ToStringPercent(), Hint("SharePercent"), SubIndent);
			if (mode != RimMarketSettings.FeeNone)
				Check("FeeInGoods", ref s.feeInGoods, Hint("FeeInGoods"), SubIndent);
			if (mode == RimMarketSettings.FeeArrival)
			{
				Check("TurnAwayIfBroke", ref s.turnAwayIfBroke, Hint("TurnAwayIfBroke"), SubIndent);
				if (RimTollBridge.Available) Check("WaiveAfterToll", ref s.waiveFeeAfterToll, Hint("WaiveAfterToll"), SubIndent);
			}
			Caption("CaptionCaravans");
			s.marketDays = Slider("MarketDays", s.marketDays, 0.5f, 3f, 0.25f, Days(s.marketDays), Hint("MarketDays"));
			s.maxMerchants = Mathf.RoundToInt(Slider("MaxMerchants", s.maxMerchants, 1f, 8f, 1f, s.maxMerchants.ToString(), Hint("MaxMerchants")));
			s.guardsAtStall = Mathf.RoundToInt(Slider("GuardsAtStall", s.guardsAtStall, 0f, 3f, 1f, s.guardsAtStall.ToString(), Hint("GuardsAtStall")));
			int least = s.guardsAtStall + 1;
			string most = s.maxGuards >= RimMarketSettings.NoGuardLimit ? "RimMarket.Settings.NoLimit".Translate().ToString() : Mathf.Max(s.maxGuards, least).ToString();
			s.maxGuards = Mathf.RoundToInt(Slider("MaxGuards", Mathf.Max(s.maxGuards, least), least, RimMarketSettings.NoGuardLimit, 1f, most, Hint("MaxGuards")));
			Check("Haggle", ref s.haggleEnabled, Hint("Haggle"));
			if (s.haggleEnabled)
				s.haggleBonus = Slider("HaggleBonus", s.haggleBonus, 0.02f, 0.10f, 0.01f, s.haggleBonus.ToStringPercent(), Hint("HaggleBonus"), SubIndent);
		}

		private void VisitorsTab(RimMarketSettings s)
		{
			Check("ColonistsBrowse", ref s.colonistsBrowse, Hint("ColonistsBrowse"));
			if (HospitalityBridge.Active || HotelsBridge.Active) Check("GuestsShop", ref s.guestsShop, Hint("GuestsShop"));
			Check("ShoppersVisit", ref s.shoppersVisit, Hint("ShoppersVisit"));
			if (s.shoppersVisit)
			{
				s.colonyShopperDays = Slider("ColonyShopperDays", s.colonyShopperDays, 0.3f, 5f, 0.1f, Days(s.colonyShopperDays), Hint("ColonyShopperDays"), SubIndent);
				s.bargainEffect = Slider("BargainEffect", s.bargainEffect, 0f, 2f, 0.05f, s.bargainEffect.ToStringPercent(), Hint("BargainEffect"), SubIndent);
			}
			Caption("CaptionColony");
			Check("Shopkeepers", ref s.shopkeepers, Hint("Shopkeepers"));
			Check("MarketSaturation", ref s.marketSaturation, Hint("MarketSaturation"));
			s.innPrice = Mathf.RoundToInt(Slider("InnPrice", s.innPrice, 0f, 60f, 5f, s.innPrice.ToString(), Hint("InnPrice")));
		}

		private void EventsTab(RimMarketSettings s)
		{
			Check("Thieves", ref s.thievesEnabled, Hint("Thieves"));
			if (s.thievesEnabled)
				s.thiefMtbDays = Slider("ThiefMtb", s.thiefMtbDays, 0.5f, 10f, 0.5f, Days(s.thiefMtbDays), Hint("ThiefMtb"), SubIndent);
			Check("SlaveAuctions", ref s.slaveAuctions, Hint("SlaveAuctions"));
			Check("Fair", ref s.fairEnabled, Hint("Fair"));
			if (s.fairEnabled)
			{
				s.fairCost = Mathf.RoundToInt(Slider("FairCost", s.fairCost, 0f, 5000f, 100f, s.fairCost.ToString(), Hint("FairCost"), SubIndent));
				s.fairCooldownDays = Mathf.RoundToInt(Slider("FairCooldown", s.fairCooldownDays, 5f, 60f, 1f, Days(s.fairCooldownDays), Hint("FairCooldown"), SubIndent));
			}
			Check("Reputation", ref s.reputationEffects, Hint("Reputation"));
			if (s.reputationEffects)
				s.extraCaravans = Slider("ExtraCaravans", s.extraCaravans, 0f, 2f, 0.1f, s.extraCaravans.ToStringPercent(), Hint("ExtraCaravans"), SubIndent);
			Check("Messages", ref s.showMessages, Hint("Messages"));
		}

		private void TradeTab(RimMarketSettings s)
		{
			Check("TradeRoutes", ref s.tradeRoutes, Hint("TradeRoutes"));
			if (s.tradeRoutes)
			{
				string open = "RimMarket.Settings.RouteOpenCostHint".Translate(Routes.TradeRouteUtility.OpenPrice(20));
				s.routeOpenFactor = Slider("RouteOpenCost", s.routeOpenFactor, 0f, 3f, 0.05f, s.routeOpenFactor.ToStringPercent(), open, SubIndent);
				string toll = "RimMarket.Settings.RouteTollHint".Translate(Routes.TradeRouteUtility.EffectiveToll(Routes.TradeRouteUtility.Toll(20, false)));
				s.routeTollFactor = Slider("RouteToll", s.routeTollFactor, 0f, 3f, 0.05f, s.routeTollFactor.ToStringPercent(), toll, SubIndent);
			}
			Check("SpecialOrders", ref s.specialOrders, Hint("SpecialOrders"));
			Check("AwayStalls", ref s.awayStalls, Hint("AwayStalls"));
			if (HegemonyBridge.Installed)
			{
				Caption("CaptionHegemony");
				Check("HegemonyPrices", ref s.hegemonyPrices, Hint("HegemonyPrices"));
				Check("HegemonyStock", ref s.hegemonyStock, Hint("HegemonyStock"));
				Check("HegemonyGoodwill", ref s.hegemonyGoodwill, Hint("HegemonyGoodwill"));
				Check("HegemonyTerritory", ref s.hegemonyTerritory, Hint("HegemonyTerritory"));
			}
		}

		private static string Hint(string key) => ("RimMarket.Settings." + key + "Hint").Translate();

		private static string Days(float days) => "RimMarket.Settings.Days".Translate(days.ToString("0.##"));

		private Rect NextRow(float indent)
		{
			var r = new Rect(indent, y, width - indent, RowHeight);
			y += RowHeight;
			return r;
		}

		private static void Hover(Rect r, string hint)
		{
			Widgets.DrawHighlightIfMouseover(r);
			if (!hint.NullOrEmpty()) TooltipHandler.TipRegion(r, hint);
		}

		private void Caption(string key)
		{
			var r = new Rect(0f, y + 6f, width, CaptionHeight - 6f);
			GUI.color = Color.gray;
			Text.Anchor = TextAnchor.LowerLeft;
			Widgets.Label(r, ("RimMarket.Settings." + key).Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(0f, y + CaptionHeight, width);
			y += CaptionHeight + 2f;
		}

		private void Check(string key, ref bool value, string hint, float indent = 0f)
		{
			Rect r = NextRow(indent);
			Hover(r, hint);
			Widgets.CheckboxLabeled(r, ("RimMarket.Settings." + key).Translate(), ref value);
		}

		private float Slider(string key, float value, float min, float max, float step, string shown, string hint, float indent = 0f)
		{
			Rect r = NextRow(indent);
			Hover(r, hint);
			float sliderWidth = Mathf.Round(width * SliderShare);
			var label = new Rect(r.x, r.y, r.width - sliderWidth - ValueWidth - 12f, r.height);
			var bar = new Rect(label.xMax + 6f, r.y, sliderWidth, r.height);
			var number = new Rect(r.xMax - ValueWidth, r.y, ValueWidth, r.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(label, ("RimMarket.Settings." + key).Translate());
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(number, shown);
			Text.Anchor = TextAnchor.UpperLeft;
			float v = Widgets.HorizontalSlider(bar, value, min, max, middleAlignment: true);
			return Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
		}

		private void DifficultyRow(RimMarketSettings s)
		{
			Rect r = NextRow(0f);
			Hover(r, Hint("Difficulty"));
			float buttonWidth = Mathf.Round(width * SliderShare) + ValueWidth + 6f;
			var label = new Rect(r.x, r.y, r.width - buttonWidth - 12f, r.height);
			var button = new Rect(r.xMax - buttonWidth, r.y + 2f, buttonWidth, r.height - 4f);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(label, "RimMarket.Settings.Difficulty".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			if (!Widgets.ButtonText(button, Market.MarketDifficulty.Label(s.marketDifficulty).CapitalizeFirst())) return;
			var options = new List<FloatMenuOption>();
			for (int level = Market.MarketDifficulty.Relaxed; level <= Market.MarketDifficulty.Tough; level++)
			{
				int pick = level;
				options.Add(new FloatMenuOption(Market.MarketDifficulty.Label(pick).CapitalizeFirst(), () => s.marketDifficulty = pick));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void FeeModeRow(RimMarketSettings s)
		{
			Rect r = NextRow(0f);
			Hover(r, Hint("FeeMode"));
			float buttonWidth = Mathf.Round(width * SliderShare) + ValueWidth + 6f;
			var label = new Rect(r.x, r.y, r.width - buttonWidth - 12f, r.height);
			var button = new Rect(r.xMax - buttonWidth, r.y + 2f, buttonWidth, r.height - 4f);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(label, "RimMarket.Settings.FeeMode".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			if (!Widgets.ButtonText(button, RimMarketSettings.FeeModeLabel(s.FeeModeSafe).CapitalizeFirst())) return;
			var options = new List<FloatMenuOption>();
			for (int m = RimMarketSettings.FeeNone; m <= RimMarketSettings.FeeShare; m++)
			{
				if (m == RimMarketSettings.FeeShare && !RimMarketSettings.ShareModeEnabled) continue;
				int mode = m;
				options.Add(new FloatMenuOption(RimMarketSettings.FeeModeLabel(mode).CapitalizeFirst(), () => s.feeMode = mode));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
