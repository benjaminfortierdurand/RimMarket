using System.Collections.Generic;
using RimMarket.Compat;
using UnityEngine;
using Verse;

namespace RimMarket.Market
{
	public class DemandEntry : IExposable
	{
		public ThingDef def;
		public float pressure;
		public int tick;

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref pressure, "pressure");
			Scribe_Values.Look(ref tick, "tick");
		}
	}

	public static class MarketDemand
	{
		private const float HalfDemandAt = 500f;
		private const float RecoveryTicks = 120000f;
		private const float MinDemand = 0.2f;
		private const float MinPriceFactor = 0.5f;
		private const float ForgetBelow = 1f;

		private static bool On => RimMarketMod.Settings.marketSaturation;

		public static bool PriceFollowsVte => VteBridge.Active;

		private static float Pressure(DemandEntry e, int now) => e.pressure * Mathf.Exp(-(now - e.tick) / RecoveryTicks);

		public static float Demand(ThingDef def)
		{
			if (!On || def == null) return 1f;
			DemandEntry e = MarketManager.Instance?.DemandFor(def, create: false);
			if (e == null) return 1f;
			return Mathf.Max(MinDemand, 1f / (1f + Pressure(e, Find.TickManager.TicksGame) / HalfDemandAt));
		}

		public static float PriceFactor(ThingDef def)
		{
			if (PriceFollowsVte) return 1f;
			return Mathf.Lerp(MinPriceFactor, 1f, Demand(def));
		}

		public static void Record(ThingDef def, float value)
		{
			if (!On || def == null || value <= 0f) return;
			DemandEntry e = MarketManager.Instance?.DemandFor(def, create: true);
			if (e == null) return;
			int now = Find.TickManager.TicksGame;
			e.pressure = Pressure(e, now) + value;
			e.tick = now;
		}

		public static void Forget(List<DemandEntry> entries)
		{
			int now = Find.TickManager.TicksGame;
			entries.RemoveAll(e => e == null || e.def == null || Pressure(e, now) < ForgetBelow);
		}
	}
}
