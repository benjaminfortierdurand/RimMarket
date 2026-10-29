using System.Collections.Generic;
using System.Linq;
using RimMarket.Inn;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Dialog_InnRegister : Window
	{
		private const float RowHeight = 28f;
		private const float HeaderHeight = 24f;
		private const float BookLine = 22f;
		private const float BookGap = 6f;
		private const float ColPrice = 60f;
		private const float ColTonight = 120f;
		private const float ColNights = 56f;
		private const float ColEarned = 66f;
		private const int PriceStep = 5;
		private const int RefreshFrames = 60;
		private static readonly Color Soft = new Color(0.75f, 0.75f, 0.75f);
		private static readonly Color Dim = new Color(0.6f, 0.6f, 0.6f);

		private class Row
		{
			public Building_Bed bed;
			public int tier;
			public int price;
			public string tonight;
			public int nights;
			public int earned;
		}

		private readonly List<Row> rows = new List<Row>();
		private Vector2 bedScroll;
		private Vector2 bookScroll;
		private int refreshFrame = -9999;

		public override Vector2 InitialSize => new Vector2(960f, 640f);

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Dialog_InnRegister>()) Find.WindowStack.Add(new Dialog_InnRegister());
		}

		public Dialog_InnRegister()
		{
			doCloseX = true;
			doCloseButton = true;
			absorbInputAroundWindow = true;
			forcePause = true;
		}

		private void Refresh()
		{
			refreshFrame = Time.frameCount;
			rows.Clear();
			List<Building_Bed> beds = InnBeds.On(Find.CurrentMap);
			for (int i = 0; i < beds.Count; i++)
			{
				Building_Bed bed = beds[i];
				CompInnBed comp = InnBeds.CompOf(bed);
				Pawn guest = bed.CurOccupants.FirstOrDefault() ?? bed.OwnersForReading.FirstOrDefault();
				int tier = InnUtility.Tier(bed);
				rows.Add(new Row
				{
					bed = bed,
					tier = tier,
					price = InnUtility.TierPrice(tier),
					tonight = guest != null ? guest.LabelShortCap : null,
					nights = comp?.nights ?? 0,
					earned = comp?.earned ?? 0,
				});
			}
			rows.SortBy(r => -r.tier, r => r.bed.thingIDNumber);
		}

		public override void DoWindowContents(Rect inRect)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || Find.CurrentMap == null)
			{
				Close();
				return;
			}
			if (Time.frameCount - refreshFrame > RefreshFrames) Refresh();

			float y = inRect.y;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "RimMarket.InnRegisterTitle".Translate());
			Text.Font = GameFont.Small;
			y += 36f;
			int taken = rows.Count(r => r.tonight != null);
			GUI.color = Soft;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f),
				"RimMarket.InnRegisterSummary".Translate(taken, rows.Count, rows.Sum(r => r.earned), rows.Sum(r => r.nights)));
			GUI.color = Color.white;
			y += 30f;

			y = DrawPrices(new Rect(inRect.x, y, inRect.width, 60f), mgr);
			y += 8f;

			float bottom = inRect.yMax - CloseButSize.y - 10f;
			float split = Mathf.Round(inRect.width * 0.56f);
			DrawBeds(new Rect(inRect.x, y, split - 12f, bottom - y));
			DrawBook(new Rect(inRect.x + split, y, inRect.width - split, bottom - y), mgr);
		}

		private float DrawPrices(Rect rect, MarketManager mgr)
		{
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(rect.x, rect.y, 200f, 24f), "RimMarket.InnRegisterPrices".Translate());
			if (Widgets.ButtonText(new Rect(rect.xMax - 160f, rect.y, 160f, 24f), "RimMarket.InnRegisterReset".Translate()))
			{
				for (int t = 0; t < InnUtility.TierCount; t++) mgr.SetInnPrice(t, -1);
				refreshFrame = -9999;
			}
			float y = rect.y + 28f;
			float colW = rect.width / InnUtility.TierCount;
			for (int t = 0; t < InnUtility.TierCount; t++)
			{
				var cell = new Rect(rect.x + t * colW, y, colW - 10f, 28f);
				Widgets.DrawHighlightIfMouseover(cell);
				TooltipHandler.TipRegion(cell, TierTip(t));
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(new Rect(cell.x + 4f, cell.y, 90f, cell.height), InnUtility.TierLabel(t).CapitalizeFirst());
				int price = InnUtility.TierPrice(t);
				float x = cell.x + 96f;
				if (Widgets.ButtonText(new Rect(x, cell.y + 2f, 24f, 24f), "-")) SetPrice(mgr, t, price - PriceStep * Steps());
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(new Rect(x + 26f, cell.y, 54f, cell.height), price > 0 ? price.ToString() : "RimMarket.InnRegisterFree".Translate().ToString());
				if (Widgets.ButtonText(new Rect(x + 82f, cell.y + 2f, 24f, 24f), "+")) SetPrice(mgr, t, price + PriceStep * Steps());
				Text.Anchor = TextAnchor.UpperLeft;
			}
			return y + 32f;
		}

		private static string TierTip(int tier)
		{
			string key = tier == InnUtility.TierDorm ? "RimMarket.InnTierDormDesc" : tier == InnUtility.TierRoom ? "RimMarket.InnTierRoomDesc" : "RimMarket.InnTierSuiteDesc";
			return key.Translate() + "\n\n" + "RimMarket.InnRegisterDefault".Translate(InnUtility.DefaultTierPrice(tier));
		}

		private static int Steps() => Event.current.shift ? 5 : 1;

		private void SetPrice(MarketManager mgr, int tier, int price)
		{
			mgr.SetInnPrice(tier, Mathf.Clamp(price, 0, InnUtility.MaxTierPrice));
			refreshFrame = -9999;
		}

		private void DrawBeds(Rect rect)
		{
			Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "RimMarket.InnRegisterBeds".Translate());
			var header = new Rect(rect.x, rect.y + 26f, rect.width - 16f, HeaderHeight);
			float colRoom = header.width - ColPrice - ColTonight - ColNights - ColEarned;
			GUI.color = Soft;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(header.x + 4f, header.y, colRoom, header.height), "RimMarket.InnRegisterColRoom".Translate());
			Text.Anchor = TextAnchor.MiddleCenter;
			float x = header.x + colRoom;
			Widgets.Label(new Rect(x, header.y, ColPrice, header.height), "RimMarket.InnRegisterColPrice".Translate());
			x += ColPrice;
			Widgets.Label(new Rect(x, header.y, ColTonight, header.height), "RimMarket.InnRegisterColTonight".Translate());
			x += ColTonight;
			Widgets.Label(new Rect(x, header.y, ColNights, header.height), "RimMarket.InnRegisterColNights".Translate());
			x += ColNights;
			Widgets.Label(new Rect(x, header.y, ColEarned, header.height), "RimMarket.InnRegisterColEarned".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

			var outRect = new Rect(rect.x, header.yMax + 2f, rect.width, rect.yMax - header.yMax - 2f);
			var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(rows.Count * RowHeight, outRect.height - 1f));
			Widgets.BeginScrollView(outRect, ref bedScroll, viewRect);
			Building_Bed jump = null;
			for (int i = 0; i < rows.Count; i++)
			{
				var r = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
				if (DrawBedRow(r, rows[i], i, colRoom)) jump = rows[i].bed;
			}
			Widgets.EndScrollView();
			if (jump != null)
			{
				Close();
				CameraJumper.TryJumpAndSelect(jump);
			}
		}

		private static bool DrawBedRow(Rect rect, Row row, int index, float colRoom)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);
			TooltipHandler.TipRegion(rect, "RimMarket.InnRegisterJump".Translate());
			Widgets.ThingIcon(new Rect(rect.x + 2f, rect.y + 2f, 24f, 24f), row.bed);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(rect.x + 30f, rect.y, colRoom - 30f, rect.height), InnUtility.TierLabel(row.tier).CapitalizeFirst());
			Text.Anchor = TextAnchor.MiddleCenter;
			float x = rect.x + colRoom;
			Widgets.Label(new Rect(x, rect.y, ColPrice, rect.height), row.price.ToString());
			x += ColPrice;
			GUI.color = row.tonight != null ? Color.white : Dim;
			Widgets.Label(new Rect(x, rect.y, ColTonight, rect.height), (row.tonight ?? "-").Truncate(ColTonight - 4f));
			GUI.color = Color.white;
			x += ColTonight;
			Widgets.Label(new Rect(x, rect.y, ColNights, rect.height), row.nights.ToString());
			x += ColNights;
			Widgets.Label(new Rect(x, rect.y, ColEarned, rect.height), row.earned.ToString());
			Text.Anchor = TextAnchor.UpperLeft;
			return Widgets.ButtonInvisible(rect);
		}

		private void DrawBook(Rect rect, MarketManager mgr)
		{
			Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "RimMarket.InnRegisterBook".Translate());
			IReadOnlyList<InnReview> book = mgr.GuestBook;
			if (book.Count > 0)
			{
				GUI.color = Soft;
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(new Rect(rect.x + 4f, rect.y + 26f, rect.width - 4f, HeaderHeight),
					"RimMarket.InnRegisterAverage".Translate(book.Average(r => r.stars).ToString("0.0"), book.Count));
				Text.Anchor = TextAnchor.UpperLeft;
				GUI.color = Color.white;
			}
			Widgets.DrawLineHorizontal(rect.x, rect.y + 26f + HeaderHeight, rect.width);
			var outRect = new Rect(rect.x, rect.y + 26f + HeaderHeight + 2f, rect.width, rect.height - 28f - HeaderHeight);
			if (book.Count == 0)
			{
				GUI.color = Dim;
				Widgets.Label(outRect, "RimMarket.InnRegisterBookEmpty".Translate());
				GUI.color = Color.white;
				return;
			}
			float entry = BookLine * 3f + BookGap;
			var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(book.Count * entry, outRect.height - 1f));
			Widgets.BeginScrollView(outRect, ref bookScroll, viewRect);
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < book.Count; i++)
			{
				InnReview r = book[i];
				float y = i * entry;
				if (y + entry < bookScroll.y || y > bookScroll.y + outRect.height) continue;
				if (i % 2 == 1) Widgets.DrawLightHighlight(new Rect(0f, y, viewRect.width, entry - 2f));
				string who = "RimMarket.InnReviewWho".Translate(r.guest, r.role.Translate(), r.faction);
				string stay = "RimMarket.InnReviewStay".Translate(InnUtility.TierLabel(r.tier), r.price, r.stars, (now - r.tick).ToStringTicksToPeriod(allowSeconds: false));
				Widgets.Label(new Rect(4f, y, viewRect.width - 8f, BookLine), who.Truncate(viewRect.width - 8f));
				GUI.color = Soft;
				Widgets.Label(new Rect(14f, y + BookLine, viewRect.width - 18f, BookLine), stay.Truncate(viewRect.width - 18f));
				string said = string.Join(", ", r.notes.Select(n => n.Translate().ToString()));
				Widgets.Label(new Rect(14f, y + BookLine * 2f, viewRect.width - 18f, BookLine), ("\"" + said.CapitalizeFirst() + "\"").Truncate(viewRect.width - 18f));
				GUI.color = Color.white;
			}
			Widgets.EndScrollView();
		}
	}
}
