using HarmonyLib;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents))]
	public static class Patch_DialogTrade_DoWindowContents
	{
		private const float ButtonWidth = 160f;
		private const float Gap = 10f;
		private const float IconSize = 40f;
		private const float IconGap = 4f;
		private const float MinWidth = 100f;

		private static readonly Color Won = new Color(0.6f, 1f, 0.6f);
		private static readonly Color Lost = new Color(1f, 0.6f, 0.6f);

		public static void Postfix(Rect inRect)
		{
			if (!MarketManager.AnyVisits || !RimMarketMod.Settings.haggleEnabled) return;
			if (TradeSession.giftMode || !(TradeSession.trader is Pawn trader)) return;
			StallVisit v = MarketManager.Instance.VisitFor(trader);
			if (v == null) return;
			Pawn negotiator = TradeSession.playerNegotiator;
			Widgets.BeginGroup(inRect);
			float left = inRect.width / 2f + ButtonWidth / 2f + Gap + ButtonWidth + Gap;
			float right = inRect.width - IconSize - IconGap - IconSize - Gap;
			float width = Mathf.Clamp(right - left, MinWidth, ButtonWidth);
			var rect = new Rect(left, inRect.height - 55f, width, 40f);
			if (!v.haggleUsed)
			{
				if (Widgets.ButtonText(rect, "RimMarket.HaggleButton".Translate()))
				{
					MarketManager.Instance.TryHaggle(v, negotiator);
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
				}
				TooltipHandler.TipRegion(rect, "RimMarket.HaggleTip".Translate(negotiator?.LabelShortCap ?? "?",
					(RimMarketMod.Settings.haggleBonus * 100f).ToString("F0")));
			}
			else
			{
				GUI.color = v.haggleWon ? Won : Lost;
				Text.Anchor = TextAnchor.MiddleLeft;
				string key = v.haggleWon ? "RimMarket.HaggleDone" : "RimMarket.HaggleFailed";
				Widgets.Label(rect, key.Translate((RimMarketMod.Settings.haggleBonus * 100f).ToString("F0")));
				Text.Anchor = TextAnchor.UpperLeft;
				GUI.color = Color.white;
			}
			Widgets.EndGroup();
		}
	}
}
