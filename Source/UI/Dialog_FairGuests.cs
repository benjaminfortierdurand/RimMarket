using System.Collections.Generic;
using System.Linq;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Dialog_FairGuests : Window
	{
		private const float RowHeight = 30f;
		private const float HeaderHeight = 26f;
		private const float ColGoodwill = 80f;
		private const float ColEnemies = 230f;
		private const float ColInvite = 70f;

		private readonly List<Faction> factions;
		private readonly Dictionary<Faction, string> enemiesOf = new Dictionary<Faction, string>();
		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(720f, 560f);

		public Dialog_FairGuests()
		{
			doCloseX = true;
			doCloseButton = true;
			absorbInputAroundWindow = true;
			forcePause = true;
			factions = Find.FactionManager.AllFactionsVisibleInViewOrder
				.Where(f => !f.IsPlayer && !f.defeated && !f.temporary && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer))
				.ToList();
			for (int i = 0; i < factions.Count; i++)
			{
				Faction f = factions[i];
				string enemies = string.Join(", ", factions.Where(o => o != f && o.HostileTo(f)).Select(o => o.Name));
				enemiesOf[f] = enemies.NullOrEmpty() ? "-" : enemies;
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null)
			{
				Close();
				return;
			}
			float y = inRect.y;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "RimMarket.FairGuestsTitle".Translate());
			Text.Font = GameFont.Small;
			y += 38f;
			string intro = "RimMarket.FairGuestsIntro".Translate();
			float h = Text.CalcHeight(intro, inRect.width);
			GUI.color = new Color(0.8f, 0.8f, 0.8f);
			Widgets.Label(new Rect(inRect.x, y, inRect.width, h), intro);
			GUI.color = Color.white;
			y += h + 8f;

			if (Widgets.ButtonText(new Rect(inRect.x, y, 120f, 26f), "RimMarket.FairGuestsInviteAll".Translate()))
			{
				for (int i = 0; i < factions.Count; i++) mgr.SetFairInvited(factions[i], true);
			}
			if (Widgets.ButtonText(new Rect(inRect.x + 128f, y, 120f, 26f), "RimMarket.FairGuestsInviteNone".Translate()))
			{
				for (int i = 0; i < factions.Count; i++) mgr.SetFairInvited(factions[i], false);
			}
			y += 34f;

			DrawHeader(new Rect(inRect.x, y, inRect.width - 16f, HeaderHeight));
			y += HeaderHeight + 2f;
			float bottom = inRect.yMax - CloseButSize.y - 34f;
			var outRect = new Rect(inRect.x, y, inRect.width, bottom - y);
			var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(factions.Count * RowHeight, outRect.height - 1f));
			Widgets.BeginScrollView(outRect, ref scroll, viewRect);
			for (int i = 0; i < factions.Count; i++) DrawRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), factions[i], i, mgr);
			Widgets.EndScrollView();

			int invited = factions.Count(mgr.FairInvited);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(inRect.x, bottom + 4f, inRect.width, 24f), "RimMarket.FairGuestsSummary".Translate(invited, factions.Count));
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void DrawHeader(Rect rect)
		{
			GUI.color = new Color(0.75f, 0.75f, 0.75f);
			Text.Anchor = TextAnchor.MiddleLeft;
			float x = rect.x;
			float colName = rect.width - ColGoodwill - ColEnemies - ColInvite;
			Widgets.Label(new Rect(x + 34f, rect.y, colName - 34f, rect.height), "RimMarket.FairGuestsFaction".Translate());
			x += colName;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColGoodwill, rect.height), "RimMarket.FairGuestsGoodwill".Translate());
			x += ColGoodwill;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(x + 6f, rect.y, ColEnemies - 6f, rect.height), "RimMarket.FairGuestsEnemies".Translate());
			x += ColEnemies;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(x, rect.y, ColInvite, rect.height), "RimMarket.FairGuestsInvited".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
		}

		private void DrawRow(Rect rect, Faction faction, int index, MarketManager mgr)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);
			bool invited = mgr.FairInvited(faction);
			float x = rect.x;
			float colName = rect.width - ColGoodwill - ColEnemies - ColInvite;
			GUI.color = faction.Color;
			GUI.DrawTexture(new Rect(x + 3f, rect.y + 3f, 24f, 24f), faction.def.FactionIcon);
			GUI.color = invited ? Color.white : new Color(0.65f, 0.65f, 0.65f);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(x + 34f, rect.y, colName - 38f, rect.height), faction.Name.Truncate(colName - 38f));
			x += colName;
			Text.Anchor = TextAnchor.MiddleCenter;
			GUI.color = faction.PlayerRelationKind.GetColor();
			Widgets.Label(new Rect(x, rect.y, ColGoodwill, rect.height), faction.PlayerGoodwill.ToStringWithSign());
			GUI.color = invited ? Color.white : new Color(0.65f, 0.65f, 0.65f);
			x += ColGoodwill;
			string enemies = enemiesOf[faction];
			var enemyRect = new Rect(x + 6f, rect.y, ColEnemies - 6f, rect.height);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(enemyRect, enemies.Truncate(enemyRect.width));
			if (enemies.Length > 1) TooltipHandler.TipRegion(enemyRect, enemies);
			x += ColEnemies;
			GUI.color = Color.white;
			bool was = invited;
			Widgets.Checkbox(new Vector2(x + (ColInvite - 24f) / 2f, rect.y + 3f), ref invited);
			if (invited != was) mgr.SetFairInvited(faction, invited);
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}
