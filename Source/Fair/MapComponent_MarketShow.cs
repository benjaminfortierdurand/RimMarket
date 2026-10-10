using System.Collections.Generic;
using RimMarket.Buildings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Fair
{
	public class MapComponent_MarketShow : MapComponent
	{
		private const int PetalTicks = 2500;
		private const float PetalsPerTick = 0.5f;
		private const int SquareMargin = 3;
		private const int ShowTicks = 2400;
		private const int LanternsPerStall = 2;
		private const int MastLanterns = 6;
		private const float MastLanternHeight = 3f;
		private const int ShellsPerStall = 2;
		private const int MinShells = 8;
		private const float LaunchSpread = 3f;
		private const int RocketTicks = 66;
		private const float RocketSeconds = 1.1f;
		private const int SparksPerBurst = 28;
		private static readonly Color RocketColor = new Color(1f, 0.85f, 0.6f);
		private static readonly Color LanternGlowColor = new Color(1f, 0.6f, 0.25f, 0.55f);
		private static readonly Color[] PetalColors =
		{
			new Color(1f, 0.72f, 0.8f), new Color(1f, 0.97f, 0.94f), new Color(1f, 0.9f, 0.5f),
			new Color(0.92f, 0.32f, 0.3f), new Color(0.8f, 0.65f, 0.95f),
		};
		private static readonly Color[] SparkColors =
		{
			new Color(1f, 0.35f, 0.3f), new Color(0.4f, 0.7f, 1f), new Color(0.5f, 1f, 0.45f),
			new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.5f, 1f), Color.white,
		};

		private const int LanternShot = 0;
		private const int RocketShot = 1;
		private const int BurstShot = 2;

		private struct Shot
		{
			public int tick;
			public Vector3 at;
			public Color color;
			public int kind;
		}

		private readonly List<Shot> shots = new List<Shot>();
		private int petalStart = -1;
		private CellRect square;

		public MapComponent_MarketShow(Map map) : base(map)
		{
		}

		public static MapComponent_MarketShow On(Map map) => map?.GetComponent<MapComponent_MarketShow>();

		public override void MapComponentTick()
		{
			int now = Find.TickManager.TicksGame;
			if (shots.Count > 0) Fire(now);
			if (petalStart >= 0) Petals(now);
		}

		public void Opening(GreatFair fair)
		{
			if (fair?.marker == null || !fair.marker.Spawned || fair.marker.Map != map) return;
			List<CompMarketStall> stalls = FairUtility.StallsOf(fair);
			CellRect r = CellRect.SingleCell(fair.marker.Position);
			for (int i = 0; i < stalls.Count; i++) r = r.Encapsulate(stalls[i].Position);
			square = r.ExpandedBy(SquareMargin).ClipInsideMap(map);
			petalStart = Find.TickManager.TicksGame;
		}

		private void Petals(int now)
		{
			int age = now - petalStart;
			if (age >= PetalTicks || age < 0)
			{
				petalStart = -1;
				return;
			}
			if (Find.CurrentMap != map) return;
			float rate = PetalsPerTick * (1f - (float)age / PetalTicks);
			int count = Mathf.FloorToInt(rate) + (Rand.Chance(rate - Mathf.Floor(rate)) ? 1 : 0);
			if (count == 0) return;
			CellRect view = Find.CameraDriver.CurrentViewRect;
			float wind = Mathf.Clamp01(map.windManager.WindSpeed);
			for (int i = 0; i < count; i++)
			{
				IntVec3 c = square.RandomCell;
				if (!view.Contains(c)) continue;
				Vector3 at = c.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f));
				if (!at.ShouldSpawnMotesAt(map)) continue;
				FleckCreationData d = FleckMaker.GetDataStatic(at, map, RimMarketDefOf.RimMarket_Petal, Rand.Range(0.22f, 0.34f));
				d.rotation = Rand.Range(0f, 360f);
				d.rotationRate = Rand.Range(-160f, 160f);
				d.velocityAngle = 180f + Rand.Range(-35f, 35f) - 30f * wind;
				d.velocitySpeed = Rand.Range(0.25f, 0.55f);
				d.instanceColor = PetalColors.RandomElement();
				map.flecks.CreateFleck(d);
			}
		}

		public void Closing(GreatFair fair)
		{
			if (fair?.marker == null || !fair.marker.Spawned || fair.marker.Map != map) return;
			List<CompMarketStall> stalls = FairUtility.StallsOf(fair);
			int now = Find.TickManager.TicksGame;
			Vector3 mast = fair.marker.TrueCenter();
			if (FairUtility.Modern(stalls))
			{
				int shells = Mathf.Max(MinShells, stalls.Count * ShellsPerStall);
				for (int i = 0; i < shells; i++)
				{
					Vector3 from = mast + new Vector3(Rand.Range(-LaunchSpread, LaunchSpread), 0f, Rand.Range(-1f, 1f));
					Schedule(now + Rand.Range(0, ShowTicks), from, SparkColors.RandomElement(), RocketShot);
				}
				return;
			}
			for (int i = 0; i < stalls.Count; i++)
			{
				for (int k = 0; k < LanternsPerStall; k++)
				{
					Vector3 from = stalls[i].parent.TrueCenter() + new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(0.8f, 1.6f));
					Schedule(now + Rand.Range(0, ShowTicks), from, Color.white, LanternShot);
				}
			}
			for (int k = 0; k < MastLanterns; k++)
			{
				Vector3 from = mast + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, MastLanternHeight);
				Schedule(now + Rand.Range(0, ShowTicks), from, Color.white, LanternShot);
			}
		}

		private void Schedule(int tick, Vector3 at, Color color, int kind)
		{
			shots.Add(new Shot { tick = tick, at = at, color = color, kind = kind });
		}

		private void Fire(int now)
		{
			for (int i = shots.Count - 1; i >= 0; i--)
			{
				Shot s = shots[i];
				if (s.tick > now) continue;
				shots.RemoveAt(i);
				if (Find.CurrentMap != map || !s.at.ShouldSpawnMotesAt(map)) continue;
				if (s.kind == LanternShot) Lantern(s.at);
				else if (s.kind == RocketShot) Rocket(s, now);
				else Burst(s);
			}
		}

		private void Lantern(Vector3 at)
		{
			float angle = Rand.Range(-14f, 14f) + 20f * Mathf.Clamp01(map.windManager.WindSpeed);
			float speed = Rand.Range(0.3f, 0.42f);
			float scale = Rand.Range(0.5f, 0.62f);
			Throw(RimMarketDefOf.RimMarket_LanternGlow, at, scale * 3.2f, angle, speed, LanternGlowColor, 0f);
			Throw(RimMarketDefOf.RimMarket_SkyLantern, at, scale, angle, speed, Color.white, Rand.Range(-8f, 8f));
		}

		private void Rocket(Shot s, int now)
		{
			float height = Rand.Range(4.5f, 7f);
			float angle = Rand.Range(-10f, 10f);
			Throw(RimMarketDefOf.RimMarket_FireworkRocket, s.at, 0.32f, angle, height / RocketSeconds, RocketColor, 0f);
			Vector3 top = s.at + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * height;
			Schedule(now + RocketTicks, top, s.color, BurstShot);
		}

		private void Burst(Shot s)
		{
			Color flash = s.color * 0.7f;
			flash.a = 1f;
			Throw(RimMarketDefOf.RimMarket_FireworkFlash, s.at, 5f, 0f, 0f, flash, 0f);
			float spin = Rand.Range(0f, 360f);
			for (int k = 0; k < SparksPerBurst; k++)
			{
				float a = spin + k * 360f / SparksPerBurst + Rand.Range(-4f, 4f);
				Throw(RimMarketDefOf.RimMarket_FireworkSpark, s.at, Rand.Range(0.28f, 0.36f), a, Rand.Range(1.8f, 2.3f), s.color, 0f);
			}
			Color inner = Color.Lerp(s.color, Color.white, 0.6f);
			for (int k = 0; k < SparksPerBurst / 2; k++)
			{
				Throw(RimMarketDefOf.RimMarket_FireworkSpark, s.at, 0.22f, Rand.Range(0f, 360f), Rand.Range(0.6f, 1.1f), inner, 0f);
			}
		}

		private void Throw(FleckDef def, Vector3 at, float scale, float angle, float speed, Color color, float rotation)
		{
			FleckCreationData d = FleckMaker.GetDataStatic(at, map, def, scale);
			d.velocityAngle = angle;
			d.velocitySpeed = speed;
			d.instanceColor = color;
			d.rotation = rotation;
			map.flecks.CreateFleck(d);
		}
	}
}
