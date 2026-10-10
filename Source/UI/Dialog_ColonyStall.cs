using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Dialog_ColonyStall : Window
	{
		private const float RowHeight = 30f;
		private const float HeaderHeight = 26f;
		private const float ButtonSize = 24f;
		private const int RefreshFrames = 120;
		private const float ColItem = 250f;
		private const float ColAvailable = 95f;
		private const float ColCounter = 95f;
		private const float ColToSell = 160f;
		private const float SilverIcon = 20f;

		private class Row
		{
			public ThingDef def;
			public int available;
			public float unitValue;
			public float price = ColonyStalls.DefaultPriceFactor;
		}

		private readonly Building_MarketStall stall;
		private readonly QuickSearchWidget search = new QuickSearchWidget();
		private readonly Dictionary<ThingDef, Row> rows = new Dictionary<ThingDef, Row>();
		private readonly List<Row> shown = new List<Row>();
		private Vector2 scroll;
		private int refreshFrame = -9999;
		private bool onlyForSale;

		public override Vector2 InitialSize => new Vector2(880f, 680f);

		public Dialog_ColonyStall(Building_MarketStall stall)
		{
			this.stall = stall;
			doCloseX = true;
			doCloseButton = true;
			absorbInputAroundWindow = true;
			forcePause = true;
			closeOnClickedOutside = false;
		}

		private void Refresh()
		{
			refreshFrame = Time.frameCount;
			var counts = new Dictionary<ThingDef, int>();
			var values = new Dictionary<ThingDef, float>();
			Map map = stall.Map;
			List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
			for (int i = 0; i < things.Count; i++)
			{
				Thing t = things[i];
				if (!t.Spawned || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
				if (!ColonyStalls.CanSell(t.def) || !MarketUtility.Tradeable(t)) continue;
				counts.TryGetValue(t.def, out int n);
				counts[t.def] = n + t.stackCount;
				values.TryGetValue(t.def, out float v);
				values[t.def] = v + t.MarketValue * t.stackCount;
			}
			IReadOnlyList<Thing> stock = stall.Stock;
			for (int i = 0; i < stock.Count; i++)
			{
				Thing t = stock[i];
				values.TryGetValue(t.def, out float v);
				values[t.def] = v + t.MarketValue * t.stackCount;
				if (!counts.ContainsKey(t.def)) counts[t.def] = 0;
			}
			IReadOnlyList<StallOrder> orders = stall.Orders;
			for (int i = 0; i < orders.Count; i++)
			{
				if (!counts.ContainsKey(orders[i].def)) counts[orders[i].def] = 0;
			}
			foreach (Row r in rows.Values) r.available = 0;
			foreach (KeyValuePair<ThingDef, int> kv in counts)
			{
				if (!rows.TryGetValue(kv.Key, out Row row))
				{
					row = new Row { def = kv.Key };
					StallOrder order = stall.OrderFor(kv.Key);
					if (order != null) row.price = order.priceFactor;
					rows[kv.Key] = row;
				}
				row.available = kv.Value;
				int total = kv.Value + stall.HeldOf(kv.Key);
				values.TryGetValue(kv.Key, out float value);
				row.unitValue = total > 0 && value > 0f ? value / total : kv.Key.BaseMarketValue;
			}
		}

		private void BuildShown()
		{
			shown.Clear();
			foreach (Row r in rows.Values)
			{
				bool forSale = stall.OrderFor(r.def) != null;
				if (onlyForSale && !forSale) continue;
				if (!forSale && r.available <= 0 && stall.HeldOf(r.def) <= 0) continue;
				if (search.filter.Active && !search.filter.Matches(r.def)) continue;
				shown.Add(r);
			}
			shown.SortBy(r => stall.OrderFor(r.def) != null ? 0 : 1, r => r.def.label);
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (stall == null || !stall.Spawned || !stall.ColonyStall)
			{
				Close();
				return;
			}
			if (Time.frameCount - refreshFrame > RefreshFrames) Refresh();
			BuildShown();

			float y = inRect.y;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "RimMarket.StallSetupTitle".Translate(stall.LabelCap));
			y += 36f;
			Text.Font = GameFont.Small;
			string intro = "RimMarket.StallSetupIntro".Translate();
			float introHeight = Text.CalcHeight(intro, inRect.width);
			GUI.color = new Color(0.8f, 0.8f, 0.8f);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, introHeight), intro);
			GUI.color = Color.white;
			y += introHeight + 8f;

			search.OnGUI(new Rect(inRect.x, y, 260f, 24f));
			var onlyRect = new Rect(inRect.xMax - 260f, y, 260f, 24f);
			Widgets.CheckboxLabeled(onlyRect, "RimMarket.StallSetupOnlyForSale".Translate(), ref onlyForSale);
			y += 32f;

			DrawHeader(new Rect(inRect.x, y, inRect.width - 16f, HeaderHeight));
			y += HeaderHeight + 2f;

			float bottom = inRect.yMax - CloseButSize.y - 58f;
			var outRect = new Rect(inRect.x, y, inRect.width, bottom - y);
			var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(shown.Count * RowHeight, outRect.height - 1f));
			Widgets.BeginScrollView(outRect, ref scroll, viewRect);
			for (int i = 0; i < shown.Count; i++)
			{
				float rowY = i * RowHeight;
				if (rowY + RowHeight < scroll.y || rowY > scroll.y + outRect.height) continue;
				DrawRow(new Rect(0f, rowY, viewRect.width, RowHeight), shown[i], i);
			}
			Widgets.EndScrollView();
			if (shown.Count == 0)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = Color.gray;
				Widgets.Label(outRect, "RimMarket.StallSetupNothing".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}

			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(inRect.x, bottom + 4f, inRect.width, 24f), Summary());
			string draw = DrawLine();
			if (draw != null)
			{
				GUI.color = new Color(0.8f, 0.8f, 0.8f);
				Widgets.Label(new Rect(inRect.x, bottom + 28f, inRect.width, 24f), draw);
				GUI.color = Color.white;
			}
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private string Summary()
		{
			IReadOnlyList<StallOrder> orders = stall.Orders;
			if (orders.Count == 0) return "RimMarket.StallSetupSummaryNone".Translate();
			float worth = 0f;
			int units = 0;
			for (int i = 0; i < orders.Count; i++)
			{
				StallOrder o = orders[i];
				float unit = rows.TryGetValue(o.def, out Row r) ? r.unitValue : o.def.BaseMarketValue;
				worth += ColonyStalls.ShownPrice(unit, o.priceFactor) * o.toSell;
				units += o.toSell;
			}
			return "RimMarket.StallSetupSummary".Translate(units, orders.Count, Mathf.RoundToInt(worth));
		}

		private string DrawLine()
		{
			if (stall.Orders.Count == 0) return null;
			if (!RimMarketMod.Settings.shoppersVisit) return "RimMarket.StallSetupDrawOff".Translate();
			float pull = ColonyStalls.Pull(stall.Map);
			string every = ColonyStalls.ShopperIntervalTicks(pull).ToStringTicksToPeriod();
			string key = pull >= ColonyStalls.BargainPull ? "RimMarket.StallSetupDrawBargain"
				: pull < 0.8f ? "RimMarket.StallSetupDrawDear" : "RimMarket.StallSetupDraw";
			return key.Translate(every);
		}

		private void DrawHeader(Rect rect)
		{
			GUI.color = new Color(0.75f, 0.75f, 0.75f);
			Text.Anchor = TextAnchor.MiddleLeft;
			float x = rect.x;
			Widgets.Label(new Rect(x + 58f, rect.y, ColItem - 58f, rect.height), "RimMarket.StallSetupItem".Translate());
			x += ColItem;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColAvailable, rect.height), "RimMarket.StallSetupAvailable".Translate());
			x += ColAvailable;
			Widgets.Label(new Rect(x, rect.y, ColCounter, rect.height), "RimMarket.StallSetupOnCounter".Translate());
			x += ColCounter;
			var sellRect = new Rect(x, rect.y, ColToSell, rect.height);
			Widgets.Label(sellRect, "RimMarket.StallSetupToSell".Translate());
			TooltipHandler.TipRegion(sellRect, "RimMarket.StallSetupAdjustTip".Translate());
			x += ColToSell;
			var priceRect = new Rect(x, rect.y, rect.xMax - x, rect.height);
			Widgets.Label(priceRect, "RimMarket.StallSetupPrice".Translate());
			TooltipHandler.TipRegion(priceRect, "RimMarket.StallSetupPriceHeaderTip".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
		}

		private void DrawRow(Rect rect, Row row, int index)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);
			StallOrder order = stall.OrderFor(row.def);
			int held = stall.HeldOf(row.def);
			int max = row.available + held;
			int toSell = order?.toSell ?? 0;

			float x = rect.x;
			float mid = rect.y + (rect.height - ButtonSize) / 2f;
			Widgets.InfoCardButton(x + 2f, mid, row.def);
			Widgets.ThingIcon(new Rect(x + 30f, rect.y + 1.5f, 27f, 27f), row.def);
			Text.Anchor = TextAnchor.MiddleLeft;
			if (order == null) GUI.color = new Color(0.85f, 0.85f, 0.85f);
			Widgets.Label(new Rect(x + 62f, rect.y, ColItem - 66f, rect.height), row.def.LabelCap.ToString().Truncate(ColItem - 66f));
			GUI.color = Color.white;
			x += ColItem;

			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColAvailable, rect.height), row.available.ToString());
			x += ColAvailable;
			Widgets.Label(new Rect(x, rect.y, ColCounter, rect.height), held > 0 ? held.ToString() : "-");
			x += ColCounter;

			int step = GenUI.CurrentAdjustmentMultiplier();
			float bx = x + 4f;
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "-") && toSell > 0)
				Apply(row, Mathf.Max(0, toSell - step));
			bx += ButtonSize;
			Widgets.Label(new Rect(bx, rect.y, 52f, rect.height), toSell > 0 ? toSell.ToString() : "0");
			bx += 52f;
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "+") && toSell < max)
				Apply(row, Mathf.Min(max, toSell + step));
			bx += ButtonSize + 4f;
			if (Widgets.ButtonText(new Rect(bx, mid, 44f, ButtonSize), "RimMarket.StallSetupAll".Translate()))
				Apply(row, toSell >= max ? 0 : max);
			x += ColToSell;

			float px = x + 4f;
			if (Widgets.ButtonText(new Rect(px, mid, ButtonSize, ButtonSize), "-")) SetPrice(row, row.price - ColonyStalls.PriceStep * PriceSteps());
			px += ButtonSize;
			var pctRect = new Rect(px, rect.y, 52f, rect.height);
			Widgets.Label(pctRect, row.price.ToStringPercent());
			px += 52f;
			if (Widgets.ButtonText(new Rect(px, mid, ButtonSize, ButtonSize), "+")) SetPrice(row, row.price + ColonyStalls.PriceStep * PriceSteps());
			px += ButtonSize + 10f;
			int each = ColonyStalls.ShownPrice(row.unitValue, row.price);
			Widgets.ThingIcon(new Rect(px, rect.y + (rect.height - SilverIcon) / 2f, SilverIcon, SilverIcon), ThingDefOf.Silver);
			px += SilverIcon + 4f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.WordWrap = false;
			Widgets.Label(new Rect(px, rect.y, rect.xMax - px, rect.height), each.ToString());
			Text.WordWrap = true;
			TooltipHandler.TipRegion(new Rect(x, rect.y, rect.xMax - x, rect.height),
				"RimMarket.StallSetupPriceTip".Translate(row.price.ToStringPercent(), each, ColonyStalls.ChanceFor(row.price).ToStringPercent()));
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private static int PriceSteps() => Event.current.shift ? 5 : 1;

		private void Apply(Row row, int toSell)
		{
			stall.SetOrder(row.def, toSell, row.price);
		}

		private void SetPrice(Row row, float price)
		{
			row.price = Mathf.Clamp(Mathf.Round(price / ColonyStalls.PriceStep) * ColonyStalls.PriceStep, ColonyStalls.MinPriceFactor, ColonyStalls.MaxPriceFactor);
			StallOrder order = stall.OrderFor(row.def);
			if (order != null) order.priceFactor = row.price;
		}
	}
}
