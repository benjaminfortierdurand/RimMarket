using System.Reflection;
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

		public static void Postfix(Rect inRect)
		{
			if (!HaggleButton.Wanted(out _)) return;
			Widgets.BeginGroup(inRect);
			float left = inRect.width / 2f + ButtonWidth / 2f + Gap + ButtonWidth + Gap;
			float right = inRect.width - IconSize - IconGap - IconSize - Gap;
			float width = Mathf.Clamp(right - left, MinWidth, ButtonWidth);
			HaggleButton.Draw(new Rect(left, inRect.height - 55f, width, 40f));
			Widgets.EndGroup();
		}
	}

	[StaticConstructorOnStartup]
	public static class Patch_DynamicTrade_DoWindowContents
	{
		private const float ButtonWidth = 160f;
		private const float ButtonHeight = 40f;
		private const float CenterGroup = 368f;
		private const float Gap = 10f;
		private const float MinWidth = 100f;

		static Patch_DynamicTrade_DoWindowContents()
		{
			MethodBase target = AccessTools.Method("DynamicTradeInterface.UserInterface.Window_DynamicTrade:DoWindowContents");
			if (target == null) return;
			new Harmony(RimMarketMod.HarmonyId).Patch(target, postfix: new HarmonyMethod(typeof(Patch_DynamicTrade_DoWindowContents), nameof(Postfix)));
			Log.Message("[RimMarket] Dynamic Trade Interface found, the haggle button is added to its trade window.");
		}

		public static void Postfix(Rect inRect)
		{
			if (!HaggleButton.Wanted(out _)) return;
			float room = inRect.width / 2f - CenterGroup / 2f - Gap;
			if (room < MinWidth) return;
			HaggleButton.Draw(new Rect(inRect.x, inRect.yMax - 4f - ButtonHeight, Mathf.Min(ButtonWidth, room), ButtonHeight));
		}
	}

	public static class HaggleButton
	{
		private static readonly Color Won = new Color(0.6f, 1f, 0.6f);
		private static readonly Color Lost = new Color(1f, 0.6f, 0.6f);

		public static bool Wanted(out StallVisit visit)
		{
			visit = null;
			if (!MarketManager.AnyVisits || !RimMarketMod.Settings.haggleEnabled) return false;
			if (TradeSession.giftMode || TradeSession.TradeCurrency == TradeCurrency.Favor || !(TradeSession.trader is Pawn trader)) return false;
			visit = MarketManager.Instance.VisitFor(trader);
			return visit != null;
		}

		public static void Draw(Rect rect)
		{
			if (!Wanted(out StallVisit v)) return;
			Pawn negotiator = TradeSession.playerNegotiator;
			if (!v.haggleUsed)
			{
				if (Widgets.ButtonText(rect, "RimMarket.HaggleButton".Translate()))
				{
					MarketManager.Instance.TryHaggle(v, negotiator);
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
				}
				TooltipHandler.TipRegion(rect, "RimMarket.HaggleTip".Translate(negotiator?.LabelShortCap ?? "?",
					(RimMarketMod.Settings.haggleBonus * 100f).ToString("F0")));
				return;
			}
			GUI.color = v.haggleWon ? Won : Lost;
			Text.Anchor = TextAnchor.MiddleLeft;
			string key = v.haggleWon ? "RimMarket.HaggleDone" : "RimMarket.HaggleFailed";
			Widgets.Label(rect, key.Translate((RimMarketMod.Settings.haggleBonus * 100f).ToString("F0")));
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
		}
	}
}
