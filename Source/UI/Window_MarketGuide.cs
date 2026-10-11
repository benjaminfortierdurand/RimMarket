using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Window_MarketGuide : Window
	{
		private const float NavWidth = 220f;
		private const float NavRow = 34f;
		private const float Gap = 14f;
		private const float TitleHeight = 36f;
		private const float LineHeight = 26f;
		private const float IconSize = 20f;
		private const float ButtonHeight = 34f;
		private const float ButtonWidth = 190f;
		private const float MaxArtHeight = 230f;
		private const float RefreshSeconds = 0.5f;

		private static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
		private static readonly Color Caption = new Color(0.65f, 0.65f, 0.65f);
		private static readonly Color DiagColor = new Color(0.95f, 0.8f, 0.35f);

		private static int selected;
		private Vector2 scroll;
		private float bodyHeight = 400f;
		private float nextRefresh;
		private readonly Dictionary<GuideChapter, List<GuideCheck>> cache = new Dictionary<GuideChapter, List<GuideCheck>>();

		public override Vector2 InitialSize => new Vector2(Mathf.Min(920f, Verse.UI.screenWidth - 32f), Mathf.Min(680f, Verse.UI.screenHeight - 48f));

		public Window_MarketGuide()
		{
			doCloseX = true;
			draggable = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		public static void Open(int chapter = -1)
		{
			if (chapter >= 0) selected = chapter;
			if (!Find.WindowStack.IsOpen<Window_MarketGuide>()) Find.WindowStack.Add(new Window_MarketGuide());
		}

		private static Map CurrentMap => Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null;

		private List<GuideCheck> Checks(GuideChapter c)
		{
			if (c.checks == null) return null;
			if (Time.realtimeSinceStartup >= nextRefresh)
			{
				cache.Clear();
				nextRefresh = Time.realtimeSinceStartup + RefreshSeconds;
			}
			if (!cache.TryGetValue(c, out List<GuideCheck> list))
			{
				list = c.checks(CurrentMap);
				cache[c] = list;
			}
			return list;
		}

		public override void DoWindowContents(Rect inRect)
		{
			List<GuideChapter> chapters = MarketGuide.Chapters;
			selected = Mathf.Clamp(selected, 0, chapters.Count - 1);
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width * 0.5f, TitleHeight), "RimMarket.Guide.Title".Translate());
			Text.Font = GameFont.Small;
			DrawProgress(new Rect(inRect.width * 0.5f, 6f, inRect.width * 0.5f - 30f, TitleHeight - 6f), chapters);
			var nav = new Rect(0f, TitleHeight + 8f, NavWidth, inRect.height - TitleHeight - 8f);
			var page = new Rect(NavWidth + Gap, nav.y, inRect.width - NavWidth - Gap, nav.height);
			DrawNav(nav, chapters);
			DrawPage(page, chapters[selected]);
		}

		private void DrawProgress(Rect rect, List<GuideChapter> chapters)
		{
			Text.Anchor = TextAnchor.MiddleRight;
			GUI.color = Caption;
			if (CurrentMap == null) Widgets.Label(rect, "RimMarket.Guide.NoMap".Translate());
			else
			{
				int done = 0, total = 0;
				foreach (GuideChapter c in chapters)
				{
					if (c.advanced || c.diagnostic) continue;
					List<GuideCheck> list = Checks(c);
					if (list == null) continue;
					foreach (GuideCheck k in list)
					{
						if (k.kind != GuideLine.Required) continue;
						total++;
						if (k.done) done++;
					}
				}
				Widgets.Label(rect, "RimMarket.Guide.Progress".Translate(done, total));
			}
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void DrawNav(Rect rect, List<GuideChapter> chapters)
		{
			float y = rect.y;
			bool captioned = false;
			for (int i = 0; i < chapters.Count; i++)
			{
				GuideChapter c = chapters[i];
				if (c.advanced && !captioned)
				{
					captioned = true;
					y += 6f;
					GUI.color = Caption;
					Widgets.Label(new Rect(rect.x + 6f, y, rect.width, 24f), "RimMarket.Guide.More".Translate());
					GUI.color = Color.white;
					y += 24f;
				}
				if (c.diagnostic) y += 10f;
				var row = new Rect(rect.x, y, rect.width, NavRow);
				if (i == selected) Widgets.DrawHighlightSelected(row);
				else Widgets.DrawHighlightIfMouseover(row);
				bool dim = false;
				Texture2D icon = c.diagnostic ? null : NavIcon(c, out dim);
				if (icon != null)
				{
					GUI.color = dim ? Dim : Color.white;
					GUI.DrawTexture(new Rect(row.x + 6f, row.y + (NavRow - IconSize) / 2f, IconSize, IconSize), icon);
					GUI.color = Color.white;
				}
				Text.Anchor = TextAnchor.MiddleLeft;
				if (c.diagnostic) GUI.color = DiagColor;
				Widgets.Label(new Rect(row.x + 32f, row.y, row.width - 36f, row.height), c.Title);
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				if (Widgets.ButtonInvisible(row))
				{
					selected = i;
					scroll = Vector2.zero;
				}
				y += NavRow + 2f;
			}
		}

		private Texture2D NavIcon(GuideChapter c, out bool dim)
		{
			dim = false;
			List<GuideCheck> list = Checks(c);
			if (list == null || CurrentMap == null) return null;
			int required = 0, requiredDone = 0, optional = 0, optionalDone = 0;
			foreach (GuideCheck k in list)
			{
				if (k.kind == GuideLine.Required) { required++; if (k.done) requiredDone++; }
				else if (k.kind == GuideLine.Optional) { optional++; if (k.done) optionalDone++; }
			}
			if (required > 0)
			{
				if (requiredDone == required) return Widgets.CheckboxOnTex;
				return requiredDone > 0 ? Widgets.CheckboxPartialTex : Widgets.CheckboxOffTex;
			}
			if (optional == 0) return null;
			dim = optionalDone == 0;
			return optionalDone == optional ? Widgets.CheckboxOnTex : Widgets.CheckboxPartialTex;
		}

		private void DrawPage(Rect rect, GuideChapter c)
		{
			Widgets.DrawMenuSection(rect);
			Rect inner = rect.ContractedBy(14f);
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inner.x, inner.y, inner.width, TitleHeight), c.Title);
			Text.Font = GameFont.Small;
			float y = inner.y + TitleHeight;
			if (c.art != null)
			{
				float h = Mathf.Min(MaxArtHeight, inner.width * c.art.height / c.art.width);
				var art = new Rect(inner.x, y, inner.width, h);
				GUI.DrawTexture(art, c.art, ScaleMode.ScaleAndCrop);
				Widgets.DrawBox(art);
				y += h + 10f;
			}
			bool hasButtons = c.actions.Count > 0;
			float bottom = inner.yMax - (hasButtons ? ButtonHeight + 10f : 0f);
			var view = new Rect(inner.x, y, inner.width, bottom - y);
			var content = new Rect(0f, 0f, view.width - 16f, Mathf.Max(bodyHeight, view.height));
			Widgets.BeginScrollView(view, ref scroll, content);
			float cy = 0f;
			string body = c.Body;
			float bh = Text.CalcHeight(body, content.width);
			Widgets.Label(new Rect(0f, cy, content.width, bh), body);
			cy += bh + 10f;
			List<GuideCheck> list = Checks(c);
			if (list != null && CurrentMap == null)
			{
				GUI.color = Caption;
				Widgets.Label(new Rect(0f, cy, content.width, LineHeight), "RimMarket.Guide.NoMap".Translate());
				GUI.color = Color.white;
				cy += LineHeight;
			}
			else if (list != null)
			{
				foreach (GuideCheck k in list) cy += DrawLine(new Rect(0f, cy, content.width, LineHeight), k);
			}
			bodyHeight = cy;
			Widgets.EndScrollView();
			if (!hasButtons) return;
			Map map = CurrentMap;
			float x = inner.x;
			foreach (GuideAction a in c.actions)
			{
				var b = new Rect(x, inner.yMax - ButtonHeight, ButtonWidth, ButtonHeight);
				bool on = a.enabled == null || a.enabled(map);
				if (Widgets.ButtonText(b, a.label.Translate(), active: on) && on) a.act(map);
				x += ButtonWidth + 10f;
			}
		}

		private float DrawLine(Rect rect, GuideCheck k)
		{
			float textWidth = rect.width - IconSize - 10f;
			string label = k.kind == GuideLine.Optional ? k.label + " (" + "RimMarket.Guide.Optional".Translate() + ")" : k.label;
			float h = Mathf.Max(LineHeight, Text.CalcHeight(label, textWidth) + 4f);
			Texture2D icon;
			bool dim = false;
			switch (k.kind)
			{
				case GuideLine.Info:
					icon = TexButton.Info;
					break;
				case GuideLine.Optional:
					icon = k.done ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
					dim = !k.done;
					break;
				default:
					icon = k.done ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
					break;
			}
			GUI.color = dim ? Dim : Color.white;
			GUI.DrawTexture(new Rect(rect.x, rect.y + 3f, IconSize, IconSize), icon);
			GUI.color = Color.white;
			Widgets.Label(new Rect(rect.x + IconSize + 10f, rect.y + 2f, textWidth, h), label);
			return h;
		}
	}
}
