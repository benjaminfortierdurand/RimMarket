using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimMarket.Orders
{
	public class Dialog_WantedNotice : Window
	{
		private const float RowHeight = 30f;
		private const float IconSize = 26f;
		private const float ValueWidth = 110f;
		private const float CountWidth = 150f;
		private const float StepButton = 26f;
		private const float FooterHeight = 60f;

		private readonly Map map;
		private readonly List<ThingDef> all;
		private readonly Dictionary<ThingDef, int> wanted = new Dictionary<ThingDef, int>();
		private readonly QuickSearchWidget search = new QuickSearchWidget();
		private readonly List<ThingDef> shown = new List<ThingDef>();
		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(780f, 680f);

		public Dialog_WantedNotice(Map map)
		{
			this.map = map;
			all = SpecialOrderUtility.Candidates(null, null);
			all.SortBy(d => d.label);
			doCloseX = true;
			absorbInputAroundWindow = true;
			forcePause = true;
		}

		private float GoodsValue()
		{
			float total = 0f;
			foreach (KeyValuePair<ThingDef, int> kv in wanted) total += SpecialOrderUtility.UnitValue(kv.Key) * kv.Value;
			return total;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RimMarket.WantedWindowTitle".Translate());
			Text.Font = GameFont.Small;
			float y = inRect.y + 36f;
			string intro = "RimMarket.WantedWindowIntro".Translate(SpecialOrderUtility.MaxLines);
			float introH = Text.CalcHeight(intro, inRect.width);
			GUI.color = new Color(0.8f, 0.8f, 0.8f);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, introH), intro);
			GUI.color = Color.white;
			y += introH + 6f;
			search.OnGUI(new Rect(inRect.x, y, 260f, 24f));
			y += 30f;
			DrawList(new Rect(inRect.x, y, inRect.width, inRect.yMax - y - FooterHeight));
			DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight + 6f, inRect.width, FooterHeight - 6f));
		}

		private void DrawList(Rect rect)
		{
			shown.Clear();
			foreach (ThingDef d in wanted.Keys) shown.Add(d);
			shown.SortBy(d => d.label);
			for (int i = 0; i < all.Count; i++)
			{
				ThingDef d = all[i];
				if (wanted.ContainsKey(d) || !search.filter.Matches(d.label)) continue;
				shown.Add(d);
			}
			var view = new Rect(0f, 0f, rect.width - 16f, shown.Count * RowHeight);
			Widgets.BeginScrollView(rect, ref scroll, view);
			int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
			int last = Mathf.Min(shown.Count, first + Mathf.CeilToInt(rect.height / RowHeight) + 1);
			for (int i = first; i < last; i++) DrawRow(new Rect(0f, i * RowHeight, view.width, RowHeight), shown[i], i);
			Widgets.EndScrollView();
		}

		private void DrawRow(Rect r, ThingDef d, int index)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(r);
			Widgets.DrawHighlightIfMouseover(r);
			Widgets.DefIcon(new Rect(r.x + 2f, r.y + 2f, IconSize, IconSize), d);
			Widgets.InfoCardButton(r.x + IconSize + 6f, r.y + 3f, d);
			float textX = r.x + IconSize + 34f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(textX, r.y, r.width - textX - ValueWidth - CountWidth, r.height), d.LabelCap);
			Widgets.Label(new Rect(r.xMax - ValueWidth - CountWidth, r.y, ValueWidth, r.height), SpecialOrderUtility.UnitValue(d).ToStringMoney());
			wanted.TryGetValue(d, out int count);
			float cx = r.xMax - CountWidth;
			if (Widgets.ButtonText(new Rect(cx, r.y + 2f, StepButton, r.height - 4f), "-"))
			{
				Change(d, count - GenUI.CurrentAdjustmentMultiplier());
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();
			}
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(cx + StepButton, r.y, CountWidth - StepButton * 2f, r.height), count.ToString());
			if (Widgets.ButtonText(new Rect(r.xMax - StepButton, r.y + 2f, StepButton, r.height - 4f), "+"))
			{
				Change(d, count + GenUI.CurrentAdjustmentMultiplier());
				SoundDefOf.Tick_High.PlayOneShotOnCamera();
			}
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void Change(ThingDef d, int count)
		{
			if (count <= 0)
			{
				wanted.Remove(d);
				return;
			}
			if (!wanted.ContainsKey(d) && wanted.Count >= SpecialOrderUtility.MaxLines)
			{
				Messages.Message("RimMarket.OrderTooManyLines".Translate(SpecialOrderUtility.MaxLines), MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			wanted.TryGetValue(d, out int before);
			float extra = SpecialOrderUtility.UnitValue(d) * (count - before);
			if (GoodsValue() + extra > SpecialOrderUtility.MaxGoodsValue)
			{
				Messages.Message("RimMarket.OrderTooBig".Translate(SpecialOrderUtility.MaxGoodsValue.ToStringMoney()), MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			wanted[d] = count;
		}

		private void DrawFooter(Rect rect)
		{
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(rect.x, rect.y, rect.width - 340f, rect.height), "RimMarket.OrderWindowValue".Translate(GoodsValue().ToStringMoney()));
			Text.Anchor = TextAnchor.UpperLeft;
			if (Widgets.ButtonText(new Rect(rect.xMax - 330f, rect.y + 6f, 160f, 40f), "CancelButton".Translate())) Close();
			var post = new Rect(rect.xMax - 160f, rect.y + 6f, 160f, 40f);
			string why = WantedUtility.CannotPost(map);
			if (wanted.Count == 0 || why != null)
			{
				Widgets.ButtonText(post, "RimMarket.WantedPost".Translate(), active: false);
				if (why != null) TooltipHandler.TipRegion(post, why);
				return;
			}
			if (!Widgets.ButtonText(post, "RimMarket.WantedPost".Translate())) return;
			var goods = new List<ThingDefCountClass>();
			foreach (KeyValuePair<ThingDef, int> kv in wanted) goods.Add(new ThingDefCountClass(kv.Key, kv.Value));
			int replies = WantedUtility.Post(map, goods);
			Messages.Message(replies > 0 ? "RimMarket.WantedPosted".Translate() : "RimMarket.WantedNobody".Translate(),
				replies > 0 ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.NegativeEvent, historical: false);
			Close();
		}
	}
}
