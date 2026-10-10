using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public class Window_MarketReputation : Window
	{
		private const float RowHeight = 36f;
		private const float WordWidth = 120f;
		private const float JournalRowGap = 6f;

		private static readonly Color BarBack = new Color(0.15f, 0.15f, 0.15f);
		private static readonly Color GoodColor = new Color(0.35f, 0.7f, 0.35f);
		private static readonly Color BadColor = new Color(0.75f, 0.3f, 0.3f);
		private static readonly Color DimColor = new Color(1f, 1f, 1f, 0.45f);

		private const float PickerWidth = 280f;

		private Vector2 scroll;
		private Faction selected;

		public override Vector2 InitialSize => new Vector2(640f, 720f);

		public Window_MarketReputation()
		{
			doCloseButton = true;
			doCloseX = true;
			closeOnClickedOutside = true;
			draggable = true;
			forcePause = false;
			absorbInputAroundWindow = false;
		}

		public static void Open()
		{
			if (Find.WindowStack.IsOpen<Window_MarketReputation>()) return;
			Find.WindowStack.Add(new Window_MarketReputation());
		}

		public override void DoWindowContents(Rect inRect)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null) return;
			if (selected != null && mgr.LedgerFor(selected) == null) selected = null;
			MarketReputation rep = selected == null ? mgr.Reputation : mgr.LedgerFor(selected);
			float y = 0f;

			Text.Font = GameFont.Medium;
			string title = selected == null
				? "RimMarket.RepWindowTitle".Translate(rep.HeadlineLabel).ToString()
				: "RimMarket.RepFactionTitle".Translate(selected.Name, rep.HeadlineLabel).ToString();
			Widgets.Label(new Rect(0f, y, inRect.width, 34f), title);
			Text.Font = GameFont.Small;
			y += 38f;

			DrawPicker(new Rect(0f, y, PickerWidth, 30f), mgr);
			y += 38f;

			string note = NoteFor(mgr, rep);
			if (!note.NullOrEmpty())
			{
				float h = Text.CalcHeight(note, inRect.width);
				GUI.color = DimColor;
				Widgets.Label(new Rect(0f, y, inRect.width, h), note);
				GUI.color = Color.white;
				y += h + 8f;
			}

			for (int axis = 0; axis < MarketReputation.AxisCount; axis++)
			{
				DrawAxis(new Rect(0f, y, inRect.width, RowHeight), rep, axis);
				y += RowHeight + 4f;
			}

			y += 6f;
			Widgets.DrawLineHorizontal(0f, y, inRect.width);
			y += 8f;
			string effects = selected == null
				? EffectsText(rep, false, rep.RefusalChance)
				: EffectsText(mgr.OpinionOf(selected), true, mgr.RefusalChance(selected));
			float effectsHeight = Text.CalcHeight(effects, inRect.width);
			Widgets.Label(new Rect(0f, y, inRect.width, effectsHeight), effects);
			y += effectsHeight + 8f;

			Widgets.DrawLineHorizontal(0f, y, inRect.width);
			y += 8f;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, y, inRect.width, 30f), "RimMarket.RepJournal".Translate());
			Text.Font = GameFont.Small;
			y += 32f;

			var outRect = new Rect(0f, y, inRect.width, inRect.height - y - CloseButSize.y - 12f);
			DrawJournal(outRect, FilteredJournal(mgr.Journal));
		}

		private void DrawPicker(Rect rect, MarketManager mgr)
		{
			string label = selected == null ? "RimMarket.RepGlobal".Translate().ToString() : selected.Name;
			if (!Widgets.ButtonText(rect, label)) return;
			var options = new List<FloatMenuOption>
			{
				new FloatMenuOption("RimMarket.RepGlobal".Translate(), () => { selected = null; scroll = Vector2.zero; }),
			};
			var known = new List<Faction>();
			foreach (FactionLedger l in mgr.Ledgers)
			{
				if (l?.faction != null && l.rep != null) known.Add(l.faction);
			}
			known.SortBy(f => f.Name);
			foreach (Faction f in known)
			{
				Faction faction = f;
				options.Add(new FloatMenuOption(faction.Name, () => { selected = faction; scroll = Vector2.zero; },
					faction.def.FactionIcon, faction.Color));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private string NoteFor(MarketManager mgr, MarketReputation rep)
		{
			if (selected == null) return rep.Established ? null : "RimMarket.RepNotEstablished".Translate().ToString();
			string weight = (mgr.OwnWeight(selected) * 100f).ToString("F0");
			string note = "RimMarket.RepFactionNote".Translate(weight);
			if (!rep.Established) note = "RimMarket.RepFactionUnknown".Translate(selected.Name) + " " + note;
			return note;
		}

		private List<MarketLogEntry> FilteredJournal(IReadOnlyList<MarketLogEntry> journal)
		{
			var list = new List<MarketLogEntry>();
			for (int i = 0; i < journal.Count; i++)
			{
				MarketLogEntry e = journal[i];
				if (selected == null || e.faction == selected || (e.faction == null && e.factionName == selected.Name)) list.Add(e);
			}
			return list;
		}

		private static void DrawAxis(Rect rect, MarketReputation rep, int axis)
		{
			int stage = rep.Stage(axis);
			var left = new Rect(rect.x, rect.y, WordWidth, rect.height);
			var right = new Rect(rect.xMax - WordWidth, rect.y, WordWidth, rect.height);
			var bar = new Rect(left.xMax + 8f, rect.y + 14f, rect.width - WordWidth * 2f - 16f, rect.height - 18f);

			Text.Anchor = TextAnchor.MiddleRight;
			GUI.color = stage < 0 ? BadColor : DimColor;
			Widgets.Label(left, MarketReputation.NegativeWord(axis));
			Text.Anchor = TextAnchor.MiddleLeft;
			GUI.color = stage > 0 ? GoodColor : DimColor;
			Widgets.Label(right, MarketReputation.PositiveWord(axis));
			GUI.color = Color.white;

			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperCenter;
			Widgets.Label(new Rect(bar.x, rect.y - 1f, bar.width, 16f), MarketReputation.AxisLabel(axis));
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;

			Widgets.DrawBoxSolid(bar, BarBack);
			float value = Mathf.Clamp(rep.Score(axis) / MarketReputation.AxisLimit, -1f, 1f);
			float center = bar.x + bar.width / 2f;
			float width = Mathf.Abs(value) * bar.width / 2f;
			var fill = value >= 0f
				? new Rect(center, bar.y, width, bar.height)
				: new Rect(center - width, bar.y, width, bar.height);
			Widgets.DrawBoxSolid(fill, value >= 0f ? GoodColor : BadColor);
			Widgets.DrawLineVertical(center, bar.y - 2f, bar.height + 4f);
			TooltipHandler.TipRegion(rect, MarketReputation.AxisLabel(axis) + ": " + rep.Score(axis).ToString("+0.0;-0.0;0"));
		}

		private static string EffectsText(MarketReputation rep, bool forFaction, float refusal)
		{
			if (!RimMarketMod.Settings.reputationEffects) return "RimMarket.RepEffectsOff".Translate();
			var lines = new List<string>();
			float perDay = rep.ExtraCaravanChancePerDay;
			if (!forFaction && perDay > 0f) lines.Add("RimMarket.RepEffectCaravans".Translate((1f / perDay).ToString("F1")));
			if (!forFaction && rep.RarityBias > 0f) lines.Add("RimMarket.RepEffectRare".Translate());
			if (refusal > 0f)
				lines.Add((forFaction ? "RimMarket.RepEffectRefusalFaction" : "RimMarket.RepEffectRefusal").Translate((refusal * 100f).ToString("F0")));
			float price = rep.PriceBonus;
			if (price > 0.001f) lines.Add("RimMarket.RepEffectPricesBetter".Translate((price * 100f).ToString("F1")));
			else if (price < -0.001f) lines.Add("RimMarket.RepEffectPricesWorse".Translate((-price * 100f).ToString("F1")));
			if (lines.Count == 0) lines.Add("RimMarket.RepEffectNone".Translate());
			string header = forFaction ? "RimMarket.RepEffectsHeaderFaction".Translate() : "RimMarket.RepEffectsHeader".Translate();
			return header + "\n" + string.Join("\n", lines);
		}

		private static string VerdictPhrase(int verdict)
		{
			if (verdict > 0) return "RimMarket.VerdictPleased".Translate();
			if (verdict < 0) return "RimMarket.VerdictGrumbling".Translate();
			return "RimMarket.VerdictNeutral".Translate();
		}

		private static string JournalLine(MarketLogEntry e)
		{
			string line = JournalHead(e);
			string takings = ReputationUtility.TakingsText(e.colonyBought, e.colonySold, e.guestSales, e.fees);
			return takings == null ? line : line + " " + takings;
		}

		private static string JournalHead(MarketLogEntry e)
		{
			string ago = (Find.TickManager.TicksGame - e.tick).ToStringTicksToPeriod();
			string verdict = VerdictPhrase(e.verdict);
			if (e.reasons == null || e.reasons.Count == 0)
				return "RimMarket.RepJournalLine".Translate(ago, e.traderName, e.factionName, verdict);
			var labels = new List<string>();
			for (int i = 0; i < e.reasons.Count; i++) labels.Add(e.reasons[i].Translate());
			return "RimMarket.RepJournalLineReasons".Translate(ago, e.traderName, e.factionName, verdict, labels.ToCommaList(useAnd: true));
		}

		private void DrawJournal(Rect outRect, IReadOnlyList<MarketLogEntry> journal)
		{
			if (journal.Count == 0)
			{
				GUI.color = DimColor;
				Widgets.Label(outRect, "RimMarket.RepJournalEmpty".Translate());
				GUI.color = Color.white;
				return;
			}
			float width = outRect.width - 16f;
			var lines = new List<string>();
			float total = 0f;
			for (int i = 0; i < journal.Count; i++)
			{
				string line = JournalLine(journal[i]);
				lines.Add(line);
				total += Text.CalcHeight(line, width) + JournalRowGap;
			}
			var viewRect = new Rect(0f, 0f, width, total);
			Widgets.BeginScrollView(outRect, ref scroll, viewRect);
			float y = 0f;
			for (int i = 0; i < lines.Count; i++)
			{
				float h = Text.CalcHeight(lines[i], width);
				int verdict = journal[i].verdict;
				GUI.color = verdict > 0 ? GoodColor : (verdict < 0 ? BadColor : Color.white);
				Widgets.Label(new Rect(0f, y, width, h), lines[i]);
				y += h + JournalRowGap;
			}
			GUI.color = Color.white;
			Widgets.EndScrollView();
		}
	}
}
