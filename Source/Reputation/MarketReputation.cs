using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimMarket.Reputation
{
	public class MarketReputation : IExposable
	{
		public const int Safety = 0;
		public const int Fairness = 1;
		public const int Fees = 2;
		public const int Appeal = 3;
		public const int AxisCount = 4;

		public const int HeadlineUnknown = 0;
		public const int HeadlineNotorious = 1;
		public const int HeadlineDubious = 2;
		public const int HeadlineUnremarkable = 3;
		public const int HeadlineRespectable = 4;
		public const int HeadlineRenowned = 5;

		public const float AxisLimit = 50f;
		private const float EnterStage = 12f;
		private const float LeaveStage = 8f;
		private const float DecayPerDay = 0.03f;
		private const float EstablishedAt = 8f;
		private const float GoodThreshold = 30f;
		private const float BadThreshold = -30f;
		private const float EffectSpan = 120f;
		private const float RefusalSpan = 75f;

		private static readonly string[] AxisKeys = { "Safety", "Fairness", "Fees", "Appeal" };
		private static readonly string[] PositiveWords = { "RimMarket.RepSafe", "RimMarket.RepHonest", "RimMarket.RepLowTax", "RimMarket.RepLively" };
		private static readonly string[] NegativeWords = { "RimMarket.RepCutthroat", "RimMarket.RepGrasping", "RimMarket.RepRacket", "RimMarket.RepShabby" };
		private static readonly string[] HeadlineKeys =
		{
			"RimMarket.RepUnknown", "RimMarket.RepNotorious", "RimMarket.RepDubious",
			"RimMarket.RepUnremarkable", "RimMarket.RepRespectable", "RimMarket.RepRenowned",
		};

		private List<float> scores = new List<float> { 0f, 0f, 0f, 0f };
		private List<int> stages = new List<int> { 0, 0, 0, 0 };
		private float lifetimePoints;
		private int lastDecayTick = -1;

		public float Score(int axis) => scores[axis];

		public int Stage(int axis) => stages[axis];

		public bool Established => lifetimePoints >= EstablishedAt;

		public float Familiarity => Mathf.Clamp01(lifetimePoints / EstablishedAt);

		public static MarketReputation Blend(MarketReputation general, MarketReputation own, float ownWeight)
		{
			var r = new MarketReputation();
			float w = Mathf.Clamp01(ownWeight);
			for (int i = 0; i < AxisCount; i++)
			{
				float g = general != null ? general.scores[i] : 0f;
				float o = own != null ? own.scores[i] : 0f;
				r.scores[i] = g * (1f - w) + o * w;
				r.RecomputeStage(i);
			}
			r.lifetimePoints = Mathf.Max(general?.lifetimePoints ?? 0f, own?.lifetimePoints ?? 0f);
			return r;
		}

		public float Total
		{
			get
			{
				float t = 0f;
				for (int i = 0; i < AxisCount; i++) t += scores[i];
				return t;
			}
		}

		public int Headline
		{
			get
			{
				if (!Established) return HeadlineUnknown;
				float t = Total;
				if (t >= 80f) return HeadlineRenowned;
				if (t >= GoodThreshold) return HeadlineRespectable;
				if (t > BadThreshold) return HeadlineUnremarkable;
				if (t > -80f) return HeadlineDubious;
				return HeadlineNotorious;
			}
		}

		public string HeadlineLabel => HeadlineKeys[Headline].Translate();

		public static string AxisLabel(int axis) => ("RimMarket.Axis" + AxisKeys[axis]).Translate();

		public static string PositiveWord(int axis) => PositiveWords[axis].Translate();

		public static string NegativeWord(int axis) => NegativeWords[axis].Translate();

		public string StageWord(int axis)
		{
			int s = stages[axis];
			if (s > 0) return PositiveWord(axis);
			if (s < 0) return NegativeWord(axis);
			return null;
		}

		public int WorstAxis
		{
			get
			{
				int worst = 0;
				for (int i = 1; i < AxisCount; i++)
				{
					if (scores[i] < scores[worst]) worst = i;
				}
				return worst;
			}
		}

		public float ExtraCaravanChancePerDay
		{
			get
			{
				if (!Established || Total < GoodThreshold) return 0f;
				return Mathf.Lerp(0.15f, 0.6f, (Total - GoodThreshold) / EffectSpan);
			}
		}

		public float RefusalChance
		{
			get
			{
				if (!Established || Total > BadThreshold) return 0f;
				return Mathf.Lerp(0.2f, 0.95f, (BadThreshold - Total) / RefusalSpan);
			}
		}

		public float PriceBonus
		{
			get
			{
				if (!Established) return 0f;
				return Mathf.Clamp(Total / 200f * 0.06f, -0.06f, 0.06f);
			}
		}

		public float RarityBias
		{
			get
			{
				if (!Established || Total <= GoodThreshold) return 0f;
				return Mathf.Lerp(0f, 0.8f, (Total - GoodThreshold) / EffectSpan);
			}
		}

		public void Add(int axis, float points)
		{
			if (axis < 0 || axis >= AxisCount || points == 0f) return;
			scores[axis] = Mathf.Clamp(scores[axis] + points, -AxisLimit, AxisLimit);
			lifetimePoints += Mathf.Abs(points);
			RecomputeStage(axis);
		}

		private void RecomputeStage(int axis)
		{
			float s = scores[axis];
			int cur = stages[axis];
			if (cur == 0)
			{
				if (s >= EnterStage) stages[axis] = 1;
				else if (s <= -EnterStage) stages[axis] = -1;
			}
			else if (cur > 0 && s < LeaveStage)
			{
				stages[axis] = s <= -EnterStage ? -1 : 0;
			}
			else if (cur < 0 && s > -LeaveStage)
			{
				stages[axis] = s >= EnterStage ? 1 : 0;
			}
		}

		public void Decay(int now)
		{
			if (lastDecayTick < 0)
			{
				lastDecayTick = now;
				return;
			}
			float days = (now - lastDecayTick) / 60000f;
			if (days < 1f) return;
			lastDecayTick = now;
			float factor = Mathf.Pow(1f - DecayPerDay, days);
			for (int i = 0; i < AxisCount; i++)
			{
				scores[i] *= factor;
				RecomputeStage(i);
			}
		}

		public void ExposeData()
		{
			Scribe_Collections.Look(ref scores, "scores", LookMode.Value);
			Scribe_Collections.Look(ref stages, "stages", LookMode.Value);
			Scribe_Values.Look(ref lifetimePoints, "lifetimePoints");
			Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", -1);
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (scores == null) scores = new List<float>();
			while (scores.Count < AxisCount) scores.Add(0f);
			if (stages == null) stages = new List<int>();
			while (stages.Count < AxisCount) stages.Add(0);
		}
	}
}
