using UnityEngine;
using Verse;

namespace RimMarket.Market
{
	public static class MarketDifficulty
	{
		public const int Relaxed = 0;
		public const int Standard = 1;
		public const int Tough = 2;

		private static readonly float[] SalePrice = { 1.15f, 1f, 0.9f };
		private static readonly float[] DemandSpread = { 2f, 1f, 0.6f };
		private static readonly float[] Purse = { 1.25f, 1f, 0.8f };
		private static readonly float[] Thieves = { 0.6f, 1f, 1.5f };
		private static readonly float[] WealthPull = { 0.5f, 1f, 1.5f };
		private static readonly string[] Keys = { "RimMarket.DifficultyRelaxed", "RimMarket.DifficultyStandard", "RimMarket.DifficultyTough" };

		public static int Level => Mathf.Clamp(RimMarketMod.Settings.marketDifficulty, Relaxed, Tough);

		public static float SalePriceFactor => SalePrice[Level];

		public static float DemandSpreadFactor => DemandSpread[Level];

		public static float PurseFactor => Purse[Level];

		public static float ThiefFactor => Thieves[Level];

		public static float WealthPullFactor => WealthPull[Level];

		public static string Label(int level) => Keys[Mathf.Clamp(level, Relaxed, Tough)].Translate();
	}
}
