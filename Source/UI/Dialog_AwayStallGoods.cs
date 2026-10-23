using System.Collections.Generic;
using RimMarket.Away;
using RimMarket.Market;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Dialog_AwayStallGoods : Window
	{
		private const float RowHeight = 30f;
		private const float HeaderHeight = 26f;
		private const int RefreshFrames = 60;
		private const float ColCount = 90f;
		private const float ColValue = 110f;
		private const float ColSell = 70f;

		private class Row
		{
			public ThingDef def;
			public int count;
			public float value;
		}

		private readonly AwayStall stall;
		private readonly List<Row> rows = new List<Row>();
		private Vector2 scroll;
		private int refreshFrame = -9999;

		public override Vector2 InitialSize => new Vector2(640f, 560f);

		public Dialog_AwayStallGoods(AwayStall stall)
		{
			this.stall = stall;
			doCloseX = true;
			doCloseButton = true;
			absorbInputAroundWindow = true;
			forcePause = true;
		}

		private bool Open => stall != null && MarketManager.Instance != null && MarketManager.Instance.AwayStallFor(stall.caravan) == stall;

		private void Refresh()
		{
			refreshFrame = Time.frameCount;
			rows.Clear();
			var byDef = new Dictionary<ThingDef, Row>();
			List<Thing> items = AwayStallUtility.SellableItems(stall.caravan, stall.settlement);
			for (int i = 0; i < items.Count; i++)
			{
				Thing t = items[i];
				if (!byDef.TryGetValue(t.def, out Row r))
				{
					r = new Row { def = t.def };
					byDef[t.def] = r;
					rows.Add(r);
				}
				r.count += t.stackCount;
				r.value += t.MarketValue * t.stackCount;
			}
			rows.SortBy(r => r.def.label);
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (!Open)
			{
				Close();
				return;
			}
			if (Time.frameCount - refreshFrame > RefreshFrames) Refresh();

			float y = inRect.y;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "RimMarket.AwayGoodsTitle".Translate(stall.settlement?.LabelCap ?? "?"));
			Text.Font = GameFont.Small;
			y += 38f;
			string intro = "RimMarket.AwayGoodsIntro".Translate();
			float h = Text.CalcHeight(intro, inRect.width);
			GUI.color = new Color(0.8f, 0.8f, 0.8f);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, h), intro);
			GUI.color = Color.white;
			y += h + 8f;

			if (Widgets.ButtonText(new Rect(inRect.x, y, 120f, 26f), "RimMarket.AwayGoodsSellAll".Translate())) stall.keep.Clear();
			if (Widgets.ButtonText(new Rect(inRect.x + 128f, y, 120f, 26f), "RimMarket.AwayGoodsKeepAll".Translate()))
			{
				for (int i = 0; i < rows.Count; i++)
				{
					if (!stall.keep.Contains(rows[i].def)) stall.keep.Add(rows[i].def);
				}
			}
			y += 34f;

			DrawHeader(new Rect(inRect.x, y, inRect.width - 16f, HeaderHeight));
			y += HeaderHeight + 2f;
			float bottom = inRect.yMax - CloseButSize.y - 34f;
			var outRect = new Rect(inRect.x, y, inRect.width, bottom - y);
			var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(rows.Count * RowHeight, outRect.height - 1f));
			Widgets.BeginScrollView(outRect, ref scroll, viewRect);
			for (int i = 0; i < rows.Count; i++) DrawRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), rows[i], i);
			Widgets.EndScrollView();

			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(inRect.x, bottom + 4f, inRect.width, 24f), Summary());
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private string Summary()
		{
			int kinds = 0;
			float value = 0f;
			for (int i = 0; i < rows.Count; i++)
			{
				if (stall.keep.Contains(rows[i].def)) continue;
				kinds++;
				value += rows[i].value;
			}
			return "RimMarket.AwayGoodsSummary".Translate(kinds, rows.Count, Mathf.RoundToInt(value));
		}

		private void DrawHeader(Rect rect)
		{
			GUI.color = new Color(0.75f, 0.75f, 0.75f);
			Text.Anchor = TextAnchor.MiddleLeft;
			float x = rect.x;
			float colItem = rect.width - ColCount - ColValue - ColSell;
			Widgets.Label(new Rect(x + 34f, rect.y, colItem - 34f, rect.height), "RimMarket.AwayGoodsItem".Translate());
			x += colItem;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColCount, rect.height), "RimMarket.AwayGoodsCount".Translate());
			x += ColCount;
			Widgets.Label(new Rect(x, rect.y, ColValue, rect.height), "RimMarket.AwayGoodsValue".Translate());
			x += ColValue;
			Widgets.Label(new Rect(x, rect.y, ColSell, rect.height), "RimMarket.AwayGoodsSell".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
		}

		private void DrawRow(Rect rect, Row row, int index)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);
			bool sell = !stall.keep.Contains(row.def);
			float x = rect.x;
			float colItem = rect.width - ColCount - ColValue - ColSell;
			Widgets.ThingIcon(new Rect(x + 3f, rect.y + 3f, 24f, 24f), row.def);
			Text.Anchor = TextAnchor.MiddleLeft;
			if (!sell) GUI.color = new Color(0.65f, 0.65f, 0.65f);
			Widgets.Label(new Rect(x + 34f, rect.y, colItem - 38f, rect.height), row.def.LabelCap.ToString().Truncate(colItem - 38f));
			x += colItem;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColCount, rect.height), row.count.ToString());
			x += ColCount;
			Widgets.Label(new Rect(x, rect.y, ColValue, rect.height), Mathf.RoundToInt(row.value).ToString());
			x += ColValue;
			GUI.color = Color.white;
			bool was = sell;
			Widgets.Checkbox(new Vector2(x + (ColSell - 24f) / 2f, rect.y + 3f), ref sell);
			if (sell != was)
			{
				if (sell) stall.keep.Remove(row.def);
				else stall.keep.Add(row.def);
			}
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}
