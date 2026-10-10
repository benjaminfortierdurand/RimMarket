using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Dialog_StallAwning : Window
	{
		private const float PreviewWidth = 300f;
		private const float SideSize = 170f;
		private const float Thumb = 66f;
		private const float ThumbGap = 6f;
		private const float LabelHeight = 28f;
		private const float SectionGap = 10f;
		private const float ButtonHeight = 38f;
		private const float ButtonWidth = 200f;
		private const float ColorPickerHeight = 80f;

		private readonly Building_MarketStall stall;
		private Vector2 scroll;
		private float viewHeight = 600f;

		public override Vector2 InitialSize => new Vector2(1000f, 720f);

		public Dialog_StallAwning(Building_MarketStall stall)
		{
			this.stall = stall;
			doCloseX = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "RimMarket.AwningTitle".Translate());
			Text.Font = GameFont.Small;
			float top = inRect.y + 44f;
			float bottom = inRect.yMax - ButtonHeight - 8f;
			var left = new Rect(inRect.x, top, PreviewWidth, bottom - top);
			var right = new Rect(left.xMax + 16f, top, inRect.width - PreviewWidth - 16f, bottom - top);
			DrawPreview(left);
			DrawChoices(right);
			DrawButtons(new Rect(inRect.x, inRect.yMax - ButtonHeight, inRect.width, ButtonHeight));
		}

		private void DrawPreview(Rect rect)
		{
			Widgets.DrawMenuSection(rect);
			StallRoof.RoofLook look = stall.Look;
			var front = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.width - 8f);
			StallRoof.DrawPreview(front, look, stall.RoofShape, stall.RoofPattern, false);
			var side = new Rect(rect.center.x - SideSize / 2f, front.yMax + 8f, SideSize, SideSize);
			if (side.yMax <= rect.yMax) StallRoof.DrawPreview(side, look, stall.RoofShape, stall.RoofPattern, true);
		}

		private void DrawChoices(Rect rect)
		{
			var view = new Rect(0f, 0f, rect.width - 16f, viewHeight);
			Widgets.BeginScrollView(rect, ref scroll, view);
			float y = 0f;
			int perRow = Mathf.Max(1, Mathf.FloorToInt((view.width + ThumbGap) / (Thumb + ThumbGap)));
			Section(ref y, view.width, "RimMarket.AwningEdge".Translate());
			for (int i = 0; i < StallRoof.Shapes; i++)
			{
				int shape = i + 1;
				Rect r = Cell(i, perRow, y);
				if (DrawThumb(r, shape, stall.RoofPattern, stall.RoofShape == shape))
					stall.SetRoof(shape, stall.RoofPattern, stall.CanvasColor, stall.StripeColor);
			}
			y += Rows(StallRoof.Shapes, perRow) + SectionGap;
			Section(ref y, view.width, "RimMarket.AwningPattern".Translate());
			for (int k = 0; k < StallRoof.Patterns; k++)
			{
				Rect r = Cell(k, perRow, y);
				if (DrawThumb(r, stall.RoofShape, k, stall.RoofPattern == k))
					stall.SetRoof(stall.RoofShape, k, stall.CanvasColor, stall.StripeColor);
			}
			y += Rows(StallRoof.Patterns, perRow) + SectionGap;
			List<Color> colors = Colors();
			Section(ref y, view.width, "RimMarket.AwningCanvas".Translate());
			Color canvas = stall.CanvasColor;
			if (Widgets.ColorSelector(new Rect(0f, y, view.width, ColorPickerHeight), ref canvas, colors, out float h1))
				stall.SetRoof(stall.RoofShape, stall.RoofPattern, canvas, stall.StripeColor);
			y += h1 + SectionGap;
			Section(ref y, view.width, "RimMarket.AwningStripe".Translate());
			Color stripe = stall.StripeColor;
			if (Widgets.ColorSelector(new Rect(0f, y, view.width, ColorPickerHeight), ref stripe, colors, out float h2))
				stall.SetRoof(stall.RoofShape, stall.RoofPattern, stall.CanvasColor, stripe);
			y += h2 + SectionGap;
			viewHeight = y;
			Widgets.EndScrollView();
		}

		private static void Section(ref float y, float width, string label)
		{
			Widgets.Label(new Rect(0f, y, width, LabelHeight), label);
			y += LabelHeight;
		}

		private static Rect Cell(int index, int perRow, float y)
		{
			int row = index / perRow;
			int col = index % perRow;
			return new Rect(col * (Thumb + ThumbGap), y + row * (Thumb + ThumbGap), Thumb, Thumb);
		}

		private static float Rows(int count, int perRow)
		{
			int rows = (count + perRow - 1) / perRow;
			return rows * (Thumb + ThumbGap);
		}

		private bool DrawThumb(Rect r, int shape, int pattern, bool selected)
		{
			Widgets.DrawBoxSolid(r, new Color(0.18f, 0.18f, 0.18f));
			StallRoof.DrawRoofThumb(r.ContractedBy(3f), shape, pattern, stall.CanvasColor, stall.StripeColor);
			if (selected) Widgets.DrawBox(r, 2);
			Widgets.DrawHighlightIfMouseover(r);
			return Widgets.ButtonInvisible(r);
		}

		private static List<Color> Colors()
		{
			var list = new List<Color>(StallRoof.Palette);
			Color mine = Faction.OfPlayer.Color;
			bool known = false;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].IndistinguishableFrom(mine)) known = true;
			}
			if (!known) list.Insert(0, mine);
			return list;
		}

		private void DrawButtons(Rect rect)
		{
			if (Widgets.ButtonText(new Rect(rect.x, rect.y, ButtonWidth, rect.height), "RimMarket.AwningRandom".Translate()))
			{
				List<Color> colors = Colors();
				Color canvas = colors.RandomElement();
				Color stripe = colors.RandomElement();
				for (int i = 0; i < 5 && stripe.IndistinguishableFrom(canvas); i++) stripe = colors.RandomElement();
				stall.SetRoof(Rand.RangeInclusive(1, StallRoof.Shapes), Rand.Range(0, StallRoof.Patterns), canvas, stripe);
			}
			if (Widgets.ButtonText(new Rect(rect.x + ButtonWidth + 10f, rect.y, ButtonWidth, rect.height), "RimMarket.AwningApplyAll".Translate()))
			{
				int n = ApplyToAll();
				Messages.Message("RimMarket.AwningAppliedAll".Translate(n), MessageTypeDefOf.NeutralEvent, historical: false);
			}
			if (Widgets.ButtonText(new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, rect.height), "CloseButton".Translate()))
				Close();
		}

		private int ApplyToAll()
		{
			if (!stall.Spawned) return 0;
			int n = 0;
			List<Thing> things = stall.Map.listerThings.ThingsOfDef(stall.def);
			for (int i = 0; i < things.Count; i++)
			{
				if (!(things[i] is Building_MarketStall other) || other == stall || other.Faction != Faction.OfPlayer) continue;
				other.SetRoof(stall.RoofShape, stall.RoofPattern, stall.CanvasColor, stall.StripeColor);
				n++;
			}
			return n;
		}
	}
}
