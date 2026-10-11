using System.Collections.Generic;
using System.Linq;
using RimMarket.Away;
using RimMarket.Market;
using RimMarket.Orders;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Window_MarketDeals : Window
	{
		private const int TabRoutes = 0;
		private const int TabOrders = 1;
		private const int TabWanted = 2;
		private const int TabAway = 3;
		private const float TitleHeight = 36f;
		private const float HeaderHeight = 26f;
		private const float RowPad = 8f;
		private const float ButtonWidth = 120f;
		private const float ButtonHeight = 30f;
		private const float NameShare = 0.32f;
		private const float WhatShare = 0.2f;

		private static readonly Color Dim = new Color(0.65f, 0.65f, 0.65f);
		private static readonly Color Alarm = new Color(0.95f, 0.45f, 0.35f);

		private static int tab;
		private Vector2 scroll;
		private float viewHeight = 300f;

		public override Vector2 InitialSize => new Vector2(Mathf.Min(900f, Verse.UI.screenWidth - 32f), Mathf.Min(620f, Verse.UI.screenHeight - 48f));

		public Window_MarketDeals()
		{
			doCloseX = true;
			doCloseButton = true;
			draggable = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Window_MarketDeals>()) Find.WindowStack.Add(new Window_MarketDeals());
		}

		public static int ActiveCount(MarketManager mgr)
		{
			if (mgr == null) return 0;
			return mgr.Routes.Count + mgr.Orders.Count + (mgr.Notice != null ? 1 : 0) + mgr.AwayStalls.Count;
		}

		private class Row
		{
			public string name;
			public string what;
			public string status;
			public bool alarm;
			public string button;
			public GlobalTargetInfo target = GlobalTargetInfo.Invalid;
			public System.Action action;
		}

		public override void DoWindowContents(Rect inRect)
		{
			MarketManager mgr = MarketManager.Instance;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), "RimMarket.DealsTitle".Translate());
			Text.Font = GameFont.Small;
			if (mgr == null) return;
			var tabs = new List<TabRecord>
			{
				new TabRecord("RimMarket.DealsTabRoutes".Translate(mgr.Routes.Count, TradeRouteUtility.MaxRoutes(mgr)), () => Select(TabRoutes), tab == TabRoutes),
				new TabRecord("RimMarket.DealsTabOrders".Translate(mgr.Orders.Count), () => Select(TabOrders), tab == TabOrders),
				new TabRecord("RimMarket.DealsTabWanted".Translate(mgr.Notice?.offers.Count ?? 0), () => Select(TabWanted), tab == TabWanted),
				new TabRecord("RimMarket.DealsTabAway".Translate(mgr.AwayStalls.Count), () => Select(TabAway), tab == TabAway),
			};
			Rect menu = new Rect(0f, TitleHeight + TabDrawer.TabHeight + 4f, inRect.width, inRect.height - TitleHeight - TabDrawer.TabHeight - 4f - CloseButSize.y - 10f);
			Widgets.DrawMenuSection(menu);
			TabDrawer.DrawTabs(menu, tabs);
			Rect inner = menu.ContractedBy(12f);
			List<Row> rows;
			string what, empty, hint;
			switch (tab)
			{
				case TabOrders: rows = OrderRows(mgr); what = "RimMarket.DealsColGoods"; empty = "RimMarket.DealsNoOrders"; hint = "RimMarket.DealsHintComms"; break;
				case TabWanted: rows = WantedRows(mgr); what = "RimMarket.DealsColGoods"; empty = "RimMarket.DealsNoNotice"; hint = "RimMarket.DealsHintComms"; break;
				case TabAway: rows = AwayRows(mgr); what = "RimMarket.DealsColSold"; empty = "RimMarket.DealsNoAway"; hint = "RimMarket.DealsHintAway"; break;
				default: rows = RouteRows(mgr); what = "RimMarket.DealsColMerchant"; empty = "RimMarket.DealsNoRoutes"; hint = "RimMarket.DealsHintComms"; break;
			}
			DrawTable(inner, rows, what.Translate(), empty.Translate(), hint.Translate());
		}

		private void Select(int t)
		{
			tab = t;
			scroll = Vector2.zero;
		}

		private void DrawTable(Rect rect, List<Row> rows, string whatHeader, string empty, string hint)
		{
			float nameW = rect.width * NameShare;
			float whatW = rect.width * WhatShare;
			float statusW = rect.width - nameW - whatW - ButtonWidth - 40f;
			GUI.color = Dim;
			Widgets.Label(new Rect(rect.x, rect.y, nameW, HeaderHeight), "RimMarket.DealsColTown".Translate());
			Widgets.Label(new Rect(rect.x + nameW + 10f, rect.y, whatW, HeaderHeight), whatHeader);
			Widgets.Label(new Rect(rect.x + nameW + whatW + 20f, rect.y, statusW, HeaderHeight), "RimMarket.DealsColStatus".Translate());
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(rect.x, rect.y + HeaderHeight, rect.width);
			float hintH = Text.CalcHeight(hint, rect.width);
			var view = new Rect(rect.x, rect.y + HeaderHeight + 4f, rect.width, rect.height - HeaderHeight - hintH - 12f);
			var content = new Rect(0f, 0f, view.width - 16f, Mathf.Max(viewHeight, view.height));
			Widgets.BeginScrollView(view, ref scroll, content);
			float y = 0f;
			if (rows.Count == 0)
			{
				GUI.color = Dim;
				Widgets.Label(new Rect(0f, 8f, content.width, 60f), empty);
				GUI.color = Color.white;
				y = 60f;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				Row r = rows[i];
				float h = Mathf.Max(ButtonHeight, Mathf.Max(Text.CalcHeight(r.name, nameW), Mathf.Max(Text.CalcHeight(r.what ?? "", whatW), Text.CalcHeight(r.status, statusW)))) + RowPad * 2f;
				var row = new Rect(0f, y, content.width, h);
				if (i % 2 == 1) Widgets.DrawLightHighlight(row);
				Widgets.Label(new Rect(0f, y + RowPad, nameW, h), r.name);
				Widgets.Label(new Rect(nameW + 10f, y + RowPad, whatW, h), r.what ?? "");
				if (r.alarm) GUI.color = Alarm;
				Widgets.Label(new Rect(nameW + whatW + 20f, y + RowPad, statusW, h), r.status);
				GUI.color = Color.white;
				bool canGo = r.action != null || r.target.IsValid;
				if (canGo && r.button != null)
				{
					var b = new Rect(content.width - ButtonWidth, y + (h - ButtonHeight) / 2f, ButtonWidth, ButtonHeight);
					if (Widgets.ButtonText(b, r.button))
					{
						if (r.action != null) r.action();
						else
						{
							Close();
							CameraJumper.TryJumpAndSelect(r.target);
						}
					}
				}
				y += h;
			}
			viewHeight = y;
			Widgets.EndScrollView();
			GUI.color = Dim;
			Widgets.Label(new Rect(rect.x, rect.yMax - hintH, rect.width, hintH), hint);
			GUI.color = Color.white;
		}

		private static string Town(Settlement s, Faction f) => (s?.LabelCap ?? "?") + " (" + (f?.Name ?? "?") + ")";

		private static GlobalTargetInfo Where(WorldObject convoy, WorldObject town)
		{
			if (convoy != null && !convoy.Destroyed && convoy.Spawned) return new GlobalTargetInfo(convoy);
			if (town != null && !town.Destroyed && town.Spawned) return new GlobalTargetInfo(town);
			return GlobalTargetInfo.Invalid;
		}

		private static string ShowLabel(WorldObject convoy) => (convoy != null && !convoy.Destroyed ? "RimMarket.DealsShowConvoy" : "RimMarket.DealsShowTown").Translate();

		private static bool AtMarket(MarketManager mgr, int lordId)
		{
			if (lordId < 0) return false;
			IReadOnlyList<StallVisit> visits = mgr.Visits;
			for (int i = 0; i < visits.Count; i++)
			{
				if (visits[i].lord != null && visits[i].lord.loadID == lordId) return true;
			}
			return false;
		}

		private static List<Row> RouteRows(MarketManager mgr)
		{
			var rows = new List<Row>();
			foreach (TradeRoute r in mgr.Routes)
			{
				bool here = AtMarket(mgr, r.arrivedLordId);
				rows.Add(new Row
				{
					name = Town(r.settlement, r.faction),
					what = (r.kind?.LabelCap ?? "?") + "\n" + "RimMarket.DealsEvery".Translate(r.intervalDays),
					status = here ? "RimMarket.DealsAtMarket".Translate().ToString() : TradeRouteDialog.RouteStatus(r),
					alarm = r.convoy != null && !r.convoy.Destroyed && r.convoy.UnderAttack,
					button = ShowLabel(r.convoy),
					target = Where(r.convoy, r.settlement),
				});
			}
			return rows;
		}

		private static List<Row> OrderRows(MarketManager mgr)
		{
			var rows = new List<Row>();
			foreach (SpecialOrder o in mgr.Orders)
			{
				rows.Add(new Row
				{
					name = Town(o.settlement, o.faction),
					what = SpecialOrderUtility.What(o) + "\n" + "RimMarket.DealsPaid".Translate(o.paid),
					status = SpecialOrderDialog.OrderStatus(o),
					alarm = o.convoy != null && !o.convoy.Destroyed && o.convoy.UnderAttack,
					button = ShowLabel(o.convoy),
					target = Where(o.convoy, o.settlement),
				});
			}
			return rows;
		}

		private static List<Row> WantedRows(MarketManager mgr)
		{
			var rows = new List<Row>();
			WantedNotice n = mgr.Notice;
			if (n == null) return rows;
			string goods = string.Join(", ", n.goods.Select(g => g.thingDef.LabelCap + " x" + g.count));
			int left = Mathf.Max(0, n.repliesEndTick - Find.TickManager.TicksGame);
			rows.Add(new Row
			{
				name = "RimMarket.DealsYourNotice".Translate(),
				what = goods,
				status = "RimMarket.DealsNoticeStatus".Translate(n.pending.Count, n.offers.Count, left.ToStringTicksToPeriod()),
				button = "RimMarket.DealsOpenBoard".Translate(),
				action = () => Find.WindowStack.Add(new Dialog_WantedBoard()),
			});
			foreach (WantedOffer o in n.offers)
			{
				string price = o.Barter ? "RimMarket.DealsBarter".Translate().ToString() : "RimMarket.DealsPrice".Translate(o.price).ToString();
				rows.Add(new Row
				{
					name = Town(o.settlement, o.faction),
					what = string.Join(", ", o.goods.Select(g => g.thingDef.LabelCap + " x" + g.count)),
					status = "RimMarket.DealsOffer".Translate(price, o.travelTicks.ToStringTicksToPeriod()),
					button = "RimMarket.DealsShowTown".Translate(),
					target = Where(null, o.settlement),
				});
			}
			return rows;
		}

		private static List<Row> AwayRows(MarketManager mgr)
		{
			var rows = new List<Row>();
			foreach (AwayStall a in mgr.AwayStalls)
			{
				rows.Add(new Row
				{
					name = Town(a.settlement, a.Host),
					what = "RimMarket.DealsSold".Translate(a.itemsSold, Mathf.RoundToInt(a.gross)),
					status = "RimMarket.DealsAwayStatus".Translate(a.caravan?.LabelCap ?? "?", a.TicksLeft.ToStringTicksToPeriod()),
					button = "RimMarket.DealsShowCaravan".Translate(),
					target = Where(a.caravan, a.settlement),
				});
			}
			return rows;
		}
	}
}
