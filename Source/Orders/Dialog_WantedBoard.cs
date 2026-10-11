using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.Routes;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Orders
{
	public class Dialog_WantedBoard : Window
	{
		private const float RowHeight = 92f;
		private const float RowGap = 6f;
		private const float AcceptWidth = 140f;
		private const float FooterHeight = 46f;

		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(860f, 640f);

		public Dialog_WantedBoard()
		{
			doCloseX = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			WantedNotice n = MarketManager.Instance?.Notice;
			Map map = n?.home?.Map;
			if (n == null || map == null)
			{
				Close();
				return;
			}
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RimMarket.WantedBoardTitle".Translate());
			Text.Font = GameFont.Small;
			float y = inRect.y + 36f;
			string head = "RimMarket.WantedBoardHead".Translate(WantedUtility.ListOf(n.goods));
			if (n.pending.Count > 0) head += " " + "RimMarket.WantedBoardWaiting".Translate();
			float h = Text.CalcHeight(head, inRect.width);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, h), head);
			y += h + 8f;
			var list = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - FooterHeight);
			if (n.offers.Count == 0)
			{
				GUI.color = Color.gray;
				Widgets.Label(list, "RimMarket.WantedBoardNone".Translate());
				GUI.color = Color.white;
			}
			else DrawOffers(list, n, map);
			var footer = new Rect(inRect.x, inRect.yMax - FooterHeight + 6f, inRect.width, FooterHeight - 6f);
			if (Widgets.ButtonText(new Rect(footer.x, footer.y, 200f, footer.height), "RimMarket.WantedWithdraw".Translate()))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RimMarket.WantedWithdrawConfirm".Translate(), () =>
				{
					WantedUtility.Withdraw();
					Close();
				}, destructive: true));
			}
			if (Widgets.ButtonText(new Rect(footer.xMax - 160f, footer.y, 160f, footer.height), "CloseButton".Translate())) Close();
		}

		private void DrawOffers(Rect rect, WantedNotice n, Map map)
		{
			List<WantedOffer> offers = new List<WantedOffer>(n.offers);
			float width = rect.width - 16f;
			var heights = new List<float>();
			float total = 0f;
			for (int i = 0; i < offers.Count; i++)
			{
				float h = Mathf.Max(RowHeight, Text.CalcHeight(OfferText(offers[i], map), width - 12f - AcceptWidth - 10f) + 16f);
				heights.Add(h);
				total += h + RowGap;
			}
			var view = new Rect(0f, 0f, width, total);
			Widgets.BeginScrollView(rect, ref scroll, view);
			float y = 0f;
			for (int i = 0; i < offers.Count; i++)
			{
				if (DrawOffer(new Rect(0f, y, view.width, heights[i]), offers[i], map)) break;
				y += heights[i] + RowGap;
			}
			Widgets.EndScrollView();
		}

		private static string OfferText(WantedOffer o, Map map)
		{
			string title = "RimMarket.WantedOfferTitle".Translate(o.faction.Name.Colorize(o.faction.Color), o.settlement.LabelCap,
				(o.travelTicks + 30000).ToStringTicksToPeriod(), TradeRouteUtility.RiskText(o.risk, o.threat));
			string goods = WantedUtility.ListOf(o.goods);
			if (o.quality >= 0) goods += ", " + ((QualityCategory)o.quality).GetLabel();
			if (o.partial) goods += " " + "RimMarket.WantedPartial".Translate();
			string pay;
			if (o.Barter)
			{
				pay = "RimMarket.WantedPayBarter".Translate(WantedUtility.ListOf(o.barter), o.barterFallback);
				if (!WantedUtility.HasBarterNow(o, map)) pay += " " + "RimMarket.WantedBarterMissing".Translate();
			}
			else pay = "RimMarket.WantedPaySilver".Translate(o.price);
			int left = Mathf.Max(0, o.expiresTick - Find.TickManager.TicksGame);
			return title + "\n" + goods + "\n" + pay + "  " + "RimMarket.WantedExpires".Translate(left.ToStringTicksToPeriod()).Colorize(ColoredText.SubtleGrayColor);
		}

		private static void DrawHaggle(Rect rect, WantedOffer o, Map map)
		{
			Pawn negotiator = WantedUtility.BestNegotiator(map);
			string why = o.haggled ? "RimMarket.WantedHaggleDone".Translate().ToString()
				: negotiator == null ? "RimMarket.WantedHaggleNoOne".Translate().ToString() : null;
			if (why != null)
			{
				Widgets.ButtonText(rect, "RimMarket.WantedHaggle".Translate(), active: false);
				TooltipHandler.TipRegion(rect, why);
				return;
			}
			TooltipHandler.TipRegion(rect, "RimMarket.WantedHaggleDesc".Translate(negotiator.LabelShort));
			if (!Widgets.ButtonText(rect, "RimMarket.WantedHaggle".Translate())) return;
			var options = new List<FloatMenuOption>();
			for (int i = 0; i < WantedUtility.HaggleCuts.Length; i++)
			{
				int step = i;
				string key = WantedUtility.HaggleWalkout[i] >= 1f ? "RimMarket.WantedHaggleAskSure" : WantedUtility.HaggleWalkout[i] > 0f ? "RimMarket.WantedHaggleAskRisky" : "RimMarket.WantedHaggleAsk";
				string label = key.Translate(WantedUtility.HaggleCuts[i].ToStringPercent(), WantedUtility.HaggleChance(o, negotiator, i).ToStringPercent());
				options.Add(new FloatMenuOption(label, () => WantedUtility.Haggle(o, map, negotiator, step)));
			}
			Find.WindowStack.Add(new FloatMenu(options, "RimMarket.WantedHaggleMenu".Translate(negotiator.LabelShort)));
		}

		private bool DrawOffer(Rect r, WantedOffer o, Map map)
		{
			Widgets.DrawMenuSection(r);
			Rect inner = r.ContractedBy(6f);
			var text = new Rect(inner.x, inner.y, inner.width - AcceptWidth - 10f, inner.height);
			Widgets.Label(text, OfferText(o, map));
			var accept = new Rect(inner.xMax - AcceptWidth, inner.y + 2f, AcceptWidth, 36f);
			var haggle = new Rect(accept.x, accept.yMax + 6f, AcceptWidth, 32f);
			DrawHaggle(haggle, o, map);
			string why = WantedUtility.CannotAccept(o, map);
			if (why != null)
			{
				Widgets.ButtonText(accept, "RimMarket.WantedAccept".Translate(), active: false);
				TooltipHandler.TipRegion(accept, why);
				return false;
			}
			if (!Widgets.ButtonText(accept, "RimMarket.WantedAccept".Translate())) return false;
			WantedUtility.Accept(o, map);
			Close();
			return true;
		}
	}
}
