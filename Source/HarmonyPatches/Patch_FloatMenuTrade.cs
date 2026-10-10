using System.Collections.Generic;
using HarmonyLib;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using Verse;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(FloatMenuOptionProvider_Trade), nameof(FloatMenuOptionProvider_Trade.GetOptionsFor),
		new[] { typeof(Pawn), typeof(FloatMenuContext) })]
	public static class Patch_FloatMenuTrade
	{
		public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> values, Pawn clickedPawn, FloatMenuContext context)
		{
			StallVisit v = MarketManager.AnyVisits && clickedPawn != null ? MarketManager.Instance.VisitFor(clickedPawn) : null;
			Pawn colonist = context?.FirstSelectedPawn;
			if (v == null || !v.StallOk || colonist == null || clickedPawn.TraderKind == null)
			{
				foreach (FloatMenuOption o in values) yield return o;
				yield break;
			}
			string expected = "TradeWith".Translate(clickedPawn.LabelShort + ", " + clickedPawn.TraderKind.label).ToString();
			CompMarketStall stall = v.stall;
			bool replaced = false;
			foreach (FloatMenuOption o in values)
			{
				if (!replaced && o.action != null && o.Label != null && o.Label.StartsWith(expected))
				{
					o.action = () => MarketUtility.StartTradeJob(colonist, stall, clickedPawn);
					replaced = true;
				}
				yield return o;
			}
		}
	}
}
