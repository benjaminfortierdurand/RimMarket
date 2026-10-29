using System.Collections.Generic;
using System.Text.RegularExpressions;
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
		private const float ColItem = 230f;
		private const float ColQuality = 120f;
		private const int Slots = 8;
		private const int NoQuality = 7;
		private const float ColAvailable = 80f;
		private const float ColCounter = 80f;
		private const float ColToSell = 160f;
		private const float ColRefill = 60f;
		private const float ColReserve = 110f;
		private const int RefillMax = 9999;
		private const int ReserveMax = 99999;
		private const float SilverIcon = 20f;
		private const int CountMaxDigits = 6;

		private static readonly Regex Digits = new Regex("^[0-9]*$");

		private static readonly QualityRange[] Presets =
		{
			QualityRange.All,
			new QualityRange(QualityCategory.Awful, QualityCategory.Awful),
			new QualityRange(QualityCategory.Awful, QualityCategory.Poor),
			new QualityRange(QualityCategory.Awful, QualityCategory.Normal),
			new QualityRange(QualityCategory.Awful, QualityCategory.Good),
			new QualityRange(QualityCategory.Awful, QualityCategory.Excellent),
			new QualityRange(QualityCategory.Awful, QualityCategory.Masterwork),
			new QualityRange(QualityCategory.Good, QualityCategory.Legendary),
			new QualityRange(QualityCategory.Excellent, QualityCategory.Legendary),
			new QualityRange(QualityCategory.Masterwork, QualityCategory.Legendary),
			new QualityRange(QualityCategory.Masterwork, QualityCategory.Masterwork),
			new QualityRange(QualityCategory.Legendary, QualityCategory.Legendary),
		};

		private class Row
		{
			public ThingDef def;
			public int available;
			public float unitValue;
			public float price = ColonyStalls.DefaultPriceFactor;
			public string buffer;
			public QualityRange quality = QualityRange.All;
			public bool refill;
			public int reserve;
			public bool hasQuality;
			public int[] onMap = new int[Slots];
			public int[] all = new int[Slots];
			public float[] worth = new float[Slots];
		}

		private readonly Building_MarketStall stall;
		private Row editing;
		private readonly QuickSearchWidget search = new QuickSearchWidget();
		private readonly Dictionary<ThingDef, Row> rows = new Dictionary<ThingDef, Row>();
		private readonly List<Row> shown = new List<Row>();
		private Vector2 scroll;
		private int refreshFrame = -9999;
		private bool onlyForSale;

		public override Vector2 InitialSize => new Vector2(Mathf.Min(1130f, Verse.UI.screenWidth - 32f), 680f);

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
			foreach (Row r in rows.Values) Clear(r);
			Map map = stall.Map;
			List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
			for (int i = 0; i < things.Count; i++)
			{
				Thing t = things[i];
				if (!t.Spawned || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
				ThingDef sold = ColonyStalls.StockDef(t);
				if (!ColonyStalls.CanSell(sold) || !MarketUtility.Tradeable(t)) continue;
				Count(RowFor(sold), t, true);
			}
			IReadOnlyList<Thing> stock = stall.Stock;
			for (int i = 0; i < stock.Count; i++) Count(RowFor(ColonyStalls.StockDef(stock[i])), stock[i], false);
			IReadOnlyList<StallOrder> orders = stall.Orders;
			for (int i = 0; i < orders.Count; i++) RowFor(orders[i].def);
			foreach (Row r in rows.Values) Recount(r);
		}

		private Row RowFor(ThingDef def)
		{
			if (rows.TryGetValue(def, out Row row)) return row;
			row = new Row { def = def, hasQuality = def.HasComp(typeof(CompQuality)) };
			StallOrder order = stall.OrderFor(def);
			if (order != null)
			{
				row.price = order.priceFactor;
				row.quality = order.quality;
				row.refill = order.refill;
				row.reserve = order.reserve;
			}
			rows[def] = row;
			return row;
		}

		private static void Clear(Row r)
		{
			for (int i = 0; i < Slots; i++)
			{
				r.onMap[i] = 0;
				r.all[i] = 0;
				r.worth[i] = 0f;
			}
		}

		private static void Count(Row r, Thing t, bool onMap)
		{
			int slot = t.TryGetQuality(out QualityCategory q) ? (int)q : NoQuality;
			if (onMap) r.onMap[slot] += t.stackCount;
			r.all[slot] += t.stackCount;
			r.worth[slot] += ColonyStalls.BaseValue(t) * t.stackCount;
		}

		private static int OnMap(Row r)
		{
			int n = 0;
			for (int i = 0; i < Slots; i++) n += r.onMap[i];
			return n;
		}

		private static bool InRange(Row r, int slot) => slot == NoQuality || r.quality.Includes((QualityCategory)slot);

		private static int Sum(Row r, int[] counts)
		{
			int n = 0;
			for (int i = 0; i < Slots; i++)
			{
				if (InRange(r, i)) n += counts[i];
			}
			return n;
		}

		private static void Recount(Row r)
		{
			r.available = Sum(r, r.onMap);
			int n = Sum(r, r.all);
			float worth = 0f;
			for (int i = 0; i < Slots; i++)
			{
				if (InRange(r, i)) worth += r.worth[i];
			}
			r.unitValue = n > 0 && worth > 0f ? worth / n : ColonyStalls.BaseValue(r.def);
		}

		private void BuildShown()
		{
			shown.Clear();
			foreach (Row r in rows.Values)
			{
				bool forSale = stall.OrderFor(r.def) != null;
				if (onlyForSale && !forSale) continue;
				if (!forSale && OnMap(r) <= 0 && stall.HeldOf(r.def) <= 0) continue;
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
			if (editing != null && GUI.GetNameOfFocusedControl() != FieldName(editing)) Commit();
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
				float unit = rows.TryGetValue(o.def, out Row r) ? r.unitValue : ColonyStalls.BaseValue(o.def);
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
			var qualityRect = new Rect(x, rect.y, ColQuality, rect.height);
			Widgets.Label(qualityRect, "RimMarket.StallSetupQuality".Translate());
			TooltipHandler.TipRegion(qualityRect, "RimMarket.StallSetupQualityTip".Translate());
			x += ColQuality;
			Widgets.Label(new Rect(x, rect.y, ColAvailable, rect.height), "RimMarket.StallSetupAvailable".Translate());
			x += ColAvailable;
			Widgets.Label(new Rect(x, rect.y, ColCounter, rect.height), "RimMarket.StallSetupOnCounter".Translate());
			x += ColCounter;
			var sellRect = new Rect(x, rect.y, ColToSell, rect.height);
			Widgets.Label(sellRect, "RimMarket.StallSetupToSell".Translate());
			TooltipHandler.TipRegion(sellRect, "RimMarket.StallSetupAdjustTip".Translate());
			x += ColToSell;
			var refillRect = new Rect(x, rect.y, ColRefill, rect.height);
			Widgets.Label(refillRect, "RimMarket.StallSetupRefill".Translate());
			TooltipHandler.TipRegion(refillRect, "RimMarket.StallSetupRefillTip".Translate());
			x += ColRefill;
			var reserveRect = new Rect(x, rect.y, ColReserve, rect.height);
			Widgets.Label(reserveRect, "RimMarket.StallSetupReserve".Translate());
			TooltipHandler.TipRegion(reserveRect, "RimMarket.StallSetupReserveTip".Translate());
			x += ColReserve;
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
			int cap = Cap(row, held);
			int toSell = order?.toSell ?? 0;

			float x = rect.x;
			float mid = rect.y + (rect.height - ButtonSize) / 2f;
			Widgets.InfoCardButton(x + 2f, mid, row.def);
			Widgets.ThingIcon(new Rect(x + 30f, rect.y + 1.5f, 27f, 27f), row.def);
			Text.Anchor = TextAnchor.MiddleLeft;
			if (order == null) GUI.color = new Color(0.85f, 0.85f, 0.85f);
			Widgets.Label(new Rect(x + 62f, rect.y, ColItem - 66f, rect.height), row.def.LabelCap.ToString().Truncate(ColItem - 66f));
			GUI.color = Color.white;
			if (RimMarketMod.Settings.marketSaturation) DrawDemand(new Rect(x + 62f, rect.yMax - 5f, ColItem - 72f, 3f), row.def);
			x += ColItem;

			Text.Anchor = TextAnchor.MiddleCenter;
			if (row.hasQuality) DrawQuality(new Rect(x + 4f, mid, ColQuality - 8f, ButtonSize), row);
			else
			{
				GUI.color = Color.gray;
				Widgets.Label(new Rect(x, rect.y, ColQuality, rect.height), "-");
				GUI.color = Color.white;
			}
			x += ColQuality;
			var availableRect = new Rect(x, rect.y, ColAvailable, rect.height);
			Widgets.Label(availableRect, row.available.ToString());
			if (row.hasQuality) TooltipHandler.TipRegion(availableRect, Breakdown(row));
			x += ColAvailable;
			Widgets.Label(new Rect(x, rect.y, ColCounter, rect.height), held > 0 ? held.ToString() : "-");
			x += ColCounter;

			int step = GenUI.CurrentAdjustmentMultiplier();
			float bx = x + 4f;
			TooltipHandler.TipRegion(new Rect(bx, mid, ColToSell - 8f, ButtonSize), "RimMarket.StallSetupAdjustTip".Translate());
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "-") && toSell > 0)
				Apply(row, Mathf.Max(0, toSell - step));
			bx += ButtonSize;
			DrawCount(new Rect(bx + 3f, mid, 46f, ButtonSize), row, toSell);
			bx += 52f;
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "+") && toSell < cap)
				Apply(row, Mathf.Min(cap, toSell + step));
			bx += ButtonSize + 4f;
			if (Widgets.ButtonText(new Rect(bx, mid, 44f, ButtonSize), "RimMarket.StallSetupAll".Translate()))
				Apply(row, toSell == max ? 0 : max);
			x += ColToSell;

			var refillRect = new Rect(x, rect.y, ColRefill, rect.height);
			TooltipHandler.TipRegion(refillRect, "RimMarket.StallSetupRefillTip".Translate());
			bool refill = row.refill;
			Widgets.Checkbox(new Vector2(x + (ColRefill - ButtonSize) / 2f, mid), ref refill);
			if (refill != row.refill) SetRefill(row, refill);
			x += ColRefill;

			DrawReserve(new Rect(x, rect.y, ColReserve, rect.height), row, mid, step);
			x += ColReserve;

			float px = x + 4f;
			if (Widgets.ButtonText(new Rect(px, mid, ButtonSize, ButtonSize), "-")) SetPrice(row, row.price - ColonyStalls.PriceStep * PriceSteps());
			px += ButtonSize;
			var pctRect = new Rect(px, rect.y, 52f, rect.height);
			Widgets.Label(pctRect, row.price.ToStringPercent());
			px += 52f;
			if (Widgets.ButtonText(new Rect(px, mid, ButtonSize, ButtonSize), "+")) SetPrice(row, row.price + ColonyStalls.PriceStep * PriceSteps());
			px += ButtonSize + 10f;
			int each = ColonyStalls.ShownPrice(row.unitValue, row.price * ColonyStalls.SaleFactor(row.def));
			Widgets.ThingIcon(new Rect(px, rect.y + (rect.height - SilverIcon) / 2f, SilverIcon, SilverIcon), ThingDefOf.Silver);
			px += SilverIcon + 4f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.WordWrap = false;
			Widgets.Label(new Rect(px, rect.y, rect.xMax - px, rect.height), each.ToString());
			Text.WordWrap = true;
			TooltipHandler.TipRegion(new Rect(x, rect.y, rect.xMax - x, rect.height),
				"RimMarket.StallSetupPriceTip".Translate(row.price.ToStringPercent(), each, (ColonyStalls.ChanceFor(row.price) * MarketDemand.Demand(row.def)).ToStringPercent()));
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private static int PriceSteps() => Event.current.shift ? 5 : 1;

		private static int Cap(Row row, int held) => row.refill ? Mathf.Max(RefillMax, row.available + held) : row.available + held;

		private void DrawReserve(Rect rect, Row row, float mid, int step)
		{
			if (ColonyStalls.IsArt(row.def))
			{
				GUI.color = Color.gray;
				Widgets.Label(rect, "-");
				GUI.color = Color.white;
				return;
			}
			TooltipHandler.TipRegion(rect, "RimMarket.StallSetupReserveTip".Translate());
			float bx = rect.x + 4f;
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "-") && row.reserve > 0)
				SetReserve(row, row.reserve - step);
			bx += ButtonSize;
			if (row.reserve <= 0) GUI.color = Color.gray;
			Widgets.Label(new Rect(bx, rect.y, 52f, rect.height), row.reserve > 0 ? row.reserve.ToString() : "-");
			GUI.color = Color.white;
			bx += 52f;
			if (Widgets.ButtonText(new Rect(bx, mid, ButtonSize, ButtonSize), "+") && row.reserve < ReserveMax)
				SetReserve(row, row.reserve + step);
		}

		private void SetRefill(Row row, bool refill)
		{
			row.refill = refill;
			StallOrder order = stall.OrderFor(row.def);
			if (order != null) order.refill = refill;
		}

		private void SetReserve(Row row, int reserve)
		{
			row.reserve = Mathf.Clamp(reserve, 0, ReserveMax);
			StallOrder order = stall.OrderFor(row.def);
			if (order != null) order.reserve = row.reserve;
		}

		private static void DrawDemand(Rect bar, ThingDef def)
		{
			float demand = MarketDemand.Demand(def);
			Widgets.DrawBoxSolid(bar, new Color(1f, 1f, 1f, 0.08f));
			Color fill = demand > 0.75f ? new Color(0.35f, 0.7f, 0.35f) : demand > 0.45f ? new Color(0.85f, 0.7f, 0.25f) : new Color(0.8f, 0.3f, 0.25f);
			Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * demand, bar.height), fill);
			var tipRect = new Rect(bar.x, bar.y - 22f, bar.width, 25f);
			string tip = "RimMarket.StallSetupDemandTip".Translate(demand.ToStringPercent());
			if (MarketDemand.PriceFollowsVte) tip += "\n\n" + "RimMarket.StallSetupDemandVte".Translate();
			TooltipHandler.TipRegion(tipRect, tip);
		}

		private static string FieldName(Row row) => "RimMarketToSell" + row.def.defName;

		private void DrawCount(Rect rect, Row row, int toSell)
		{
			string name = FieldName(row);
			if (editing != row) row.buffer = toSell.ToString();
			GUI.SetNextControlName(name);
			string text = Widgets.TextField(rect, row.buffer, CountMaxDigits, Digits);
			if (GUI.GetNameOfFocusedControl() != name) return;
			if (editing != row)
			{
				if (editing != null) Commit();
				editing = row;
			}
			row.buffer = text;
		}

		private void Commit()
		{
			Row row = editing;
			editing = null;
			if (row == null || stall == null || !stall.Spawned || !stall.ColonyStall) return;
			int typed = row.buffer.NullOrEmpty() ? 0 : int.Parse(row.buffer);
			typed = Mathf.Clamp(typed, 0, Cap(row, stall.HeldOf(row.def)));
			if (typed != (stall.OrderFor(row.def)?.toSell ?? 0)) Apply(row, typed);
		}

		public override void OnAcceptKeyPressed()
		{
			if (editing == null)
			{
				base.OnAcceptKeyPressed();
				return;
			}
			Commit();
			Verse.UI.UnfocusCurrentControl();
		}

		public override void PreClose()
		{
			base.PreClose();
			Commit();
		}

		private void Apply(Row row, int toSell)
		{
			if (editing == row)
			{
				editing = null;
				Verse.UI.UnfocusCurrentControl();
			}
			stall.SetOrder(row.def, toSell, row.price, row.quality, row.refill, row.reserve);
		}

		private void DrawQuality(Rect rect, Row row)
		{
			if (!Widgets.ButtonText(rect, ShortLabel(row.quality))) return;
			var options = new List<FloatMenuOption>();
			for (int i = 0; i < Presets.Length; i++)
			{
				QualityRange range = Presets[i];
				int n = 0;
				for (int q = 0; q < NoQuality; q++)
				{
					if (range.Includes((QualityCategory)q)) n += row.onMap[q];
				}
				options.Add(new FloatMenuOption(LongLabel(range) + " (" + n + ")", () => SetQuality(row, range)));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void SetQuality(Row row, QualityRange range)
		{
			row.quality = range;
			Recount(row);
			StallOrder order = stall.OrderFor(row.def);
			if (order != null) stall.SetOrder(row.def, order.toSell, row.price, range, row.refill, row.reserve);
		}

		private static bool IsAll(QualityRange r) => r.min == QualityCategory.Awful && r.max == QualityCategory.Legendary;

		private static string ShortLabel(QualityRange r)
		{
			if (IsAll(r)) return "RimMarket.QualityAnyShort".Translate();
			if (r.min == r.max) return "RimMarket.QualityOnly".Translate(r.min.GetLabel()).CapitalizeFirst();
			if (r.min == QualityCategory.Awful) return "RimMarket.QualityUpTo".Translate(r.max.GetLabel());
			if (r.max == QualityCategory.Legendary) return "RimMarket.QualityFromShort".Translate(r.min.GetLabel().CapitalizeFirst());
			return r.min.GetLabel().CapitalizeFirst() + " - " + r.max.GetLabel();
		}

		private static string LongLabel(QualityRange r)
		{
			if (IsAll(r)) return "RimMarket.QualityAny".Translate();
			if (r.min == r.max) return "RimMarket.QualityOnly".Translate(r.min.GetLabel()).CapitalizeFirst();
			if (r.min == QualityCategory.Awful) return "RimMarket.QualityOrWorse".Translate(r.max.GetLabel()).CapitalizeFirst();
			if (r.max == QualityCategory.Legendary) return "RimMarket.QualityOrBetter".Translate(r.min.GetLabel()).CapitalizeFirst();
			return ShortLabel(r);
		}

		private static string Breakdown(Row row)
		{
			var parts = new List<string>();
			for (int q = 0; q < NoQuality; q++)
			{
				if (row.onMap[q] > 0) parts.Add(((QualityCategory)q).GetLabel() + " " + row.onMap[q]);
			}
			return parts.Count == 0 ? "RimMarket.StallSetupNoneAround".Translate().ToString() : string.Join(", ", parts);
		}

		private void SetPrice(Row row, float price)
		{
			row.price = Mathf.Clamp(Mathf.Round(price / ColonyStalls.PriceStep) * ColonyStalls.PriceStep, ColonyStalls.MinPriceFactor, ColonyStalls.MaxPriceFactor);
			StallOrder order = stall.OrderFor(row.def);
			if (order != null) order.priceFactor = row.price;
		}
	}
}
