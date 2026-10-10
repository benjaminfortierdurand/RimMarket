using System.Collections.Generic;
using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Fair
{
	[StaticConstructorOnStartup]
	public class MapComponent_FairBunting : MapComponent
	{
		private const int RebuildTicks = 250;
		private const int ViewMargin = 2;
		private const float SegmentsPerCell = 1.2f;
		private const float MaxSpan = 14f;
		private const float SagPerCell = 0.16f;
		private const float MaxSag = 1.3f;
		private const float PennantSpacing = 0.55f;
		private const float PennantDrop = 0.22f;
		private const float RopeWidth = 0.07f;
		private static readonly Vector3 PennantSize = new Vector3(0.3f, 1f, 0.45f);
		private const float BulbSpacing = 0.42f;
		private const float BulbDrop = 0.15f;
		private const float WireWidth = 0.05f;
		private const float GlowSize = 1.2f;
		private const float NightStart = 0.35f;
		private const float NightRamp = 0.4f;
		private const int GlowSteps = 8;
		private static readonly Vector3 BulbSize = new Vector3(0.21f, 1f, 0.32f);
		private static readonly Color DefaultColor = new Color(0.66f, 0.17f, 0.16f);
		private static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f);
		private static readonly Color[] Palette =
		{
			new Color(0.77f, 0.23f, 0.2f), new Color(0.24f, 0.43f, 0.71f), new Color(0.35f, 0.59f, 0.27f),
			new Color(0.84f, 0.66f, 0.21f), new Color(0.55f, 0.31f, 0.63f), new Color(0.24f, 0.59f, 0.59f),
		};
		private const float GreySaturation = 0.15f;
		private const float MinSaturation = 0.45f;
		private const float MinValue = 0.55f;
		private const float MaxValue = 0.92f;
		private const float MinRingDepth = 2.5f;
		private const float HullEpsilon = 0.01f;
		private const float TightSag = 0.35f;
		private const float PoleCenterZ = 1.625f;
		private static readonly Vector3 PoleSize = new Vector3(1f, 1f, 4.25f);
		private const float CrownTexCenterZ = 3.05f;
		private static readonly Vector3 CrownSize = new Vector3(1.75f, 1f, 1.75f);
		private const float CrownZ = 3.18f;
		private const float CrownRadiusX = 0.52f;
		private const float CrownRadiusZ = 0.18f;
		private const float MetalTopCenterZ = 3.6f;
		private static readonly Vector3 MetalTopSize = new Vector3(0.75f, 1f, 0.75f);
		private const float RingZ = 3.5f;
		private const float RingRadiusX = 0.2f;
		private const float RingRadiusZ = 0.07f;
		private const float LampZ = 3.76f;
		private const float LampGlowSize = 2.6f;
		private static readonly Color LampColor = new Color(1f, 0.88f, 0.66f);
		private const float MastHeight = 4f;
		private const float RibbonRange = 11f;
		private const int MaxRibbons = 8;
		private const float MinRibbonAngle = 14f;
		private const float RibbonWidth = 0.14f;
		private const float TwistLength = 1.3f;
		private const float RibbonSagPerCell = 0.09f;
		private const float RibbonMaxSag = 0.9f;
		private const float SideCos = 0.6f;
		private const float SideSin = 0.8f;
		private const float SideBow = 0.7f;

		private static readonly Texture2D TriangleTex = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_PennantTriangle");
		private static readonly Texture2D TriangleMask = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_PennantTriangle_m");
		private static readonly Texture2D ForkTex = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_PennantFork");
		private static readonly Texture2D ForkMask = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_PennantFork_m");
		private static readonly Material RopeMat = MaterialPool.MatFrom("Things/Fair/RimMarket_BuntingRope", ShaderDatabase.Transparent);
		private static readonly Material WireMat = MaterialPool.MatFrom("Things/Fair/RimMarket_LightWire", ShaderDatabase.Transparent);
		private static readonly Texture2D BulbTex = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_Bulb");
		private static readonly Texture2D BulbMask = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_Bulb_m");
		private static readonly Dictionary<Color, Material> bulbMats = new Dictionary<Color, Material>();
		private static readonly Dictionary<Color, Material> glowMats = new Dictionary<Color, Material>();
		private static readonly Material RosetteMat = MaterialPool.MatFrom("Things/Fair/RimMarket_Rosette", ShaderDatabase.Cutout);
		private static readonly Vector3 RosetteSize = new Vector3(0.42f, 1f, 0.63f);
		private const float RosetteDrop = 0.55f;
		private static readonly Dictionary<Color, Material> triangleMats = new Dictionary<Color, Material>();
		private static readonly Dictionary<Color, Material> forkMats = new Dictionary<Color, Material>();
		private static readonly Material PoleMat = MaterialPool.MatFrom("Things/Fair/RimMarket_MaypolePole", ShaderDatabase.Cutout);
		private static readonly Material CrownMat = MaterialPool.MatFrom("Things/Fair/RimMarket_MaypoleCrown", ShaderDatabase.Cutout);
		private static readonly Material MetalMat = MaterialPool.MatFrom("Things/Fair/RimMarket_MaypoleMetal", ShaderDatabase.Cutout);
		private static readonly Material MetalTopMat = MaterialPool.MatFrom("Things/Fair/RimMarket_MaypoleMetalTop", ShaderDatabase.Cutout);
		private static readonly Dictionary<Color, Material> ribbonMats = new Dictionary<Color, Material>();

		private struct Span
		{
			public Vector3 a;
			public Vector3 b;
			public Color color;
			public bool lights;
			public float bow;
			public bool tight;
		}

		private readonly List<Span> spans = new List<Span>();
		private readonly List<Span> ribbons = new List<Span>();
		private readonly List<float> ribbonAngles = new List<float>();
		private int builtTick = -99999;
		private bool hasMast;
		private bool metalMast;
		private Vector3 mastBase;
		private Building_MarketMarker maskedMarker;

		public MapComponent_FairBunting(Map map) : base(map)
		{
		}

		public override void MapComponentUpdate()
		{
			if (Find.CurrentMap != map || WorldRendererUtility.WorldRendered) return;
			SyncMarker();
			if (!FairUtility.Festive(map))
			{
				if (spans.Count > 0) spans.Clear();
				ribbons.Clear();
				hasMast = false;
				return;
			}
			int now = Find.TickManager.TicksGame;
			if (now - builtTick >= RebuildTicks || now < builtTick) Rebuild(now);
			float ropeAlt = AltitudeLayer.PawnState.AltitudeFor() + Altitudes.AltInc * 2f;
			float wind = map.windManager.WindSpeed;
			float time = Time.realtimeSinceStartup;
			float night = Mathf.Clamp01((1f - map.skyManager.CurSkyGlow - NightStart) / NightRamp);
			CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(ViewMargin);
			DrawRosette(ropeAlt, time);
			for (int i = 0; i < spans.Count; i++)
			{
				if (!InView(spans[i], view)) continue;
				if (spans[i].lights) DrawLights(spans[i], ropeAlt, night, time, i);
				else DrawSpan(spans[i], ropeAlt, wind, time, i);
			}
			DrawMast(ropeAlt, wind, night, time, view);
		}

		public override void MapComponentTick()
		{
			if (Find.TickManager.TicksGame % RebuildTicks != 0) return;
			SyncMarker();
		}

		private void SyncMarker()
		{
			Building_MarketMarker up = FairUtility.MastOn(map);
			if (up == maskedMarker) return;
			Remesh(maskedMarker);
			Remesh(up);
			maskedMarker = up;
		}

		private void Remesh(Building_MarketMarker marker)
		{
			if (marker != null && marker.Spawned && marker.Map == map) marker.DirtyMapMesh(map);
		}

		private void Rebuild(int now)
		{
			builtTick = now;
			spans.Clear();
			GreatFair fair = FairUtility.On(map);
			List<CompMarketStall> all = FairUtility.StallsOf(fair);
			for (int i = 0; i < all.Count; i++)
			{
				if (!Corners(all[i], out Vector3 left, out Vector3 right)) continue;
				spans.Add(new Span
				{
					a = left,
					b = right,
					color = ColorOf(all[i]) ?? DefaultColor,
					lights = all[i].Props.stringLights,
					tight = true,
				});
			}
			var remaining = new List<CompMarketStall>(all);
			while (remaining.Count >= 2)
			{
				List<CompMarketStall> ring = PeelRing(remaining, out bool loop);
				AddChain(ring, loop);
			}
			BuildMast(fair, all);
		}

		private void BuildMast(GreatFair fair, List<CompMarketStall> all)
		{
			ribbons.Clear();
			ribbonAngles.Clear();
			hasMast = false;
			Building_MarketMarker marker = fair?.marker;
			if (marker == null || !marker.Spawned || marker.Map != map) return;
			hasMast = true;
			mastBase = marker.TrueCenter();
			metalMast = FairUtility.Modern(all);
			Vector3 crown = mastBase + new Vector3(0f, 0f, metalMast ? RingZ : CrownZ);
			float rx = metalMast ? RingRadiusX : CrownRadiusX;
			float rz = metalMast ? RingRadiusZ : CrownRadiusZ;
			var near = new List<CompMarketStall>(all);
			near.SortBy(s => (At(s) - mastBase).MagnitudeHorizontalSquared());
			for (int i = 0; i < near.Count && ribbons.Count < MaxRibbons; i++)
			{
				CompMarketStall stall = near[i];
				if ((At(stall) - mastBase).MagnitudeHorizontal() > RibbonRange) break;
				Vector3 end = Front(stall);
				float angle = Mathf.Atan2(end.z - crown.z, end.x - crown.x) * Mathf.Rad2Deg;
				if (Crowded(angle)) continue;
				ribbonAngles.Add(angle);
				ribbons.Add(RibbonTo(crown, rx, rz, end, ColorOf(stall) ?? Cream));
			}
		}

		private bool Crowded(float angle)
		{
			for (int i = 0; i < ribbonAngles.Count; i++)
			{
				if (Mathf.Abs(Mathf.DeltaAngle(angle, ribbonAngles[i])) < MinRibbonAngle) return true;
			}
			return false;
		}

		private Span RibbonTo(Vector3 crown, float rx, float rz, Vector3 end, Color color)
		{
			float th = Mathf.Atan2(end.z - crown.z, (end.x - crown.x) * rz / rx);
			float c = Mathf.Cos(th);
			float s = Mathf.Sin(th);
			float bow = 0f;
			if (Mathf.Abs(c) < SideCos)
			{
				float side = Mathf.Abs(c) > 0.05f ? Mathf.Sign(c) : (end.z > crown.z ? 1f : -1f);
				c = side * SideCos;
				s = s >= 0f ? SideSin : -SideSin;
				bow = side * SideBow;
			}
			return new Span
			{
				a = crown + new Vector3(rx * c, 0f, rz * s),
				b = end,
				color = color,
				lights = metalMast,
				bow = bow,
			};
		}

		private void DrawMast(float ropeAlt, float wind, float night, float time, CellRect view)
		{
			if (!hasMast) return;
			for (int i = 0; i < ribbons.Count; i++)
			{
				if (!InView(ribbons[i], view)) continue;
				if (metalMast) DrawLights(ribbons[i], ropeAlt, night, time, i + 31);
				else DrawRibbon(ribbons[i], ropeAlt, wind, time, i);
			}
			if (mastBase.x < view.minX - 1 || mastBase.x > view.maxX + 2) return;
			if (mastBase.z + MastHeight < view.minZ || mastBase.z - 1f > view.maxZ + 1) return;
			float topAlt = ropeAlt + Altitudes.AltInc * 3f;
			DrawMastPart(metalMast ? MetalMat : PoleMat, PoleCenterZ, PoleSize, AltitudeLayer.Building.AltitudeFor() + Altitudes.AltInc);
			if (metalMast) DrawMastPart(MetalTopMat, MetalTopCenterZ, MetalTopSize, topAlt);
			else DrawMastPart(CrownMat, CrownTexCenterZ, CrownSize, topAlt);
			if (metalMast && night > 0f)
				DrawMastPart(GlowMat(LampColor, night), LampZ, new Vector3(LampGlowSize, 1f, LampGlowSize), ropeAlt - Altitudes.AltInc);
		}

		private void DrawMastPart(Material mat, float z, Vector3 size, float alt)
		{
			Vector3 pos = mastBase + new Vector3(0f, 0f, z);
			pos.y = alt;
			Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, size), mat, 0);
		}

		private static void DrawRibbon(Span span, float alt, float wind, float time, int seed)
		{
			float dist = (span.b - span.a).MagnitudeHorizontal();
			if (dist < 0.5f) return;
			float breath = 1f + (0.05f + 0.1f * Mathf.Clamp01(wind)) * Mathf.Sin(time * 0.8f + seed * 1.7f);
			float sag = Mathf.Min(RibbonMaxSag, dist * RibbonSagPerCell) * breath;
			span.bow *= breath;
			int segments = Mathf.Max(2, Mathf.RoundToInt(dist / TwistLength));
			Material mat = RibbonMat(span.color);
			for (int k = 0; k < segments; k++)
			{
				Vector3 p0 = Curve(span, sag, (float)k / segments);
				Vector3 p1 = Curve(span, sag, (float)(k + 1) / segments);
				Vector3 mid = (p0 + p1) * 0.5f;
				mid.y = alt;
				Vector3 d = p1 - p0;
				float angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
				var scale = new Vector3(d.MagnitudeHorizontal() + 0.03f, 1f, RibbonWidth);
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, Quaternion.AngleAxis(-angle, Vector3.up), scale), mat, 0);
			}
		}

		private static Material RibbonMat(Color color)
		{
			if (ribbonMats.TryGetValue(color, out Material mat)) return mat;
			mat = MaterialPool.MatFrom("Things/Fair/RimMarket_Ribbon", ShaderDatabase.Cutout, color);
			ribbonMats[color] = mat;
			return mat;
		}

		private void AddChain(List<CompMarketStall> ring, bool loop)
		{
			int n = ring.Count;
			if (n < 2) return;
			int links = loop ? n : n - 1;
			for (int i = 0; i < links; i++)
			{
				CompMarketStall a = ring[i];
				CompMarketStall b = ring[(i + 1) % n];
				if ((a.parent.TrueCenter() - b.parent.TrueCenter()).MagnitudeHorizontal() > MaxSpan) continue;
				spans.Add(new Span
				{
					a = Toward(a, At(b)),
					b = Toward(b, At(a)),
					color = ColorOf(a) ?? ColorOf(b) ?? DefaultColor,
					lights = a.Props.stringLights || b.Props.stringLights,
				});
			}
		}

		private static Vector3 At(CompMarketStall s) => s.parent.TrueCenter();

		private static float Cross(Vector3 o, Vector3 a, Vector3 b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);

		private static List<CompMarketStall> PeelRing(List<CompMarketStall> remaining, out bool loop)
		{
			float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
			for (int i = 0; i < remaining.Count; i++)
			{
				Vector3 p = At(remaining[i]);
				minX = Mathf.Min(minX, p.x);
				maxX = Mathf.Max(maxX, p.x);
				minZ = Mathf.Min(minZ, p.z);
				maxZ = Mathf.Max(maxZ, p.z);
			}
			if (remaining.Count < 3 || Mathf.Min(maxX - minX, maxZ - minZ) < MinRingDepth)
			{
				bool wide = maxX - minX >= maxZ - minZ;
				var chain = new List<CompMarketStall>(remaining);
				chain.SortBy(s => wide ? At(s).x : At(s).z);
				remaining.Clear();
				loop = false;
				return chain;
			}
			var sorted = new List<CompMarketStall>(remaining);
			sorted.SortBy(s => At(s).x, s => At(s).z);
			var lower = new List<CompMarketStall>();
			for (int i = 0; i < sorted.Count; i++)
			{
				while (lower.Count >= 2 && Cross(At(lower[lower.Count - 2]), At(lower[lower.Count - 1]), At(sorted[i])) < -HullEpsilon) lower.RemoveAt(lower.Count - 1);
				lower.Add(sorted[i]);
			}
			var upper = new List<CompMarketStall>();
			for (int i = sorted.Count - 1; i >= 0; i--)
			{
				while (upper.Count >= 2 && Cross(At(upper[upper.Count - 2]), At(upper[upper.Count - 1]), At(sorted[i])) < -HullEpsilon) upper.RemoveAt(upper.Count - 1);
				upper.Add(sorted[i]);
			}
			var hull = new List<CompMarketStall>();
			for (int i = 0; i < lower.Count - 1; i++)
			{
				if (!hull.Contains(lower[i])) hull.Add(lower[i]);
			}
			for (int i = 0; i < upper.Count - 1; i++)
			{
				if (!hull.Contains(upper[i])) hull.Add(upper[i]);
			}
			remaining.RemoveAll(s => hull.Contains(s));
			loop = hull.Count >= 3;
			return hull;
		}

		private void DrawRosette(float alt, float time)
		{
			Thing winner = FairUtility.On(map)?.winnerStall;
			if (winner == null || !winner.Spawned || winner.Map != map) return;
			CompMarketStall stall = MarketStallDefs.StallOf(winner);
			if (stall == null) return;
			Vector3 pos = Front(stall) + new Vector3(0f, 0f, -RosetteDrop);
			pos.y = alt + Altitudes.AltInc * 2f;
			Quaternion rot = Quaternion.AngleAxis(Mathf.Sin(time * 1.1f) * 3f, Vector3.up);
			Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, RosetteSize), RosetteMat, 0);
		}

		private static bool InView(Span span, CellRect view)
		{
			float minX = Mathf.Min(span.a.x, span.b.x);
			float maxX = Mathf.Max(span.a.x, span.b.x);
			float minZ = Mathf.Min(span.a.z, span.b.z) - MaxSag - 1f;
			float maxZ = Mathf.Max(span.a.z, span.b.z);
			return maxX >= view.minX && minX <= view.maxX + 1 && maxZ >= view.minZ && minZ <= view.maxZ + 1;
		}

		private static bool Corners(CompMarketStall stall, out Vector3 left, out Vector3 right)
		{
			left = right = Vector3.zero;
			if (!stall.Props.frontBunting) return false;
			Rot4 rot = stall.parent.Rotation;
			Vector3 a = rot.IsHorizontal ? stall.Props.frontLeftVertical : stall.Props.frontLeft;
			Vector3 b = rot.IsHorizontal ? stall.Props.frontRightVertical : stall.Props.frontRight;
			if (rot == Rot4.West)
			{
				a.x = -a.x;
				b.x = -b.x;
			}
			Vector3 c = stall.parent.TrueCenter();
			left = c + a;
			right = c + b;
			return true;
		}

		private static Vector3 Front(CompMarketStall stall)
		{
			return Corners(stall, out Vector3 left, out Vector3 right) ? (left + right) * 0.5f : Anchor(stall);
		}

		private static Vector3 Toward(CompMarketStall stall, Vector3 target)
		{
			if (!Corners(stall, out Vector3 left, out Vector3 right)) return Anchor(stall);
			return (left - target).MagnitudeHorizontalSquared() <= (right - target).MagnitudeHorizontalSquared() ? left : right;
		}

		private static Vector3 Anchor(CompMarketStall stall)
		{
			Vector3 offset = stall.parent.Rotation.IsHorizontal ? stall.Props.buntingAnchorVertical : stall.Props.buntingAnchor;
			return stall.parent.TrueCenter() + offset;
		}

		private static Color? ColorOf(CompMarketStall stall)
		{
			StallVisit v = stall.Visit;
			if (v?.faction != null) return Festive(v.faction);
			if (ColonyStalls.IsColonyStall(stall.parent)) return Festive(Faction.OfPlayer);
			return null;
		}

		public static Color Festive(Faction faction)
		{
			Color.RGBToHSV(faction.Color, out float h, out float s, out float v);
			if (s < GreySaturation) return Palette[Mathf.Abs(faction.loadID) % Palette.Length];
			return Color.HSVToRGB(h, Mathf.Max(s, MinSaturation), Mathf.Clamp(v, MinValue, MaxValue));
		}

		private static Vector3 Curve(Span span, float sag, float t)
		{
			Vector3 p = Vector3.Lerp(span.a, span.b, t);
			float bulge = 4f * t * (1f - t);
			p.z -= sag * bulge;
			p.x += span.bow * bulge;
			return p;
		}

		private static void DrawLine(Span span, float sag, float alt, float width, Material mat)
		{
			float dist = (span.b - span.a).MagnitudeHorizontal();
			int segments = Mathf.Max(4, Mathf.CeilToInt(dist * SegmentsPerCell));
			for (int k = 0; k < segments; k++)
			{
				Vector3 p0 = Curve(span, sag, (float)k / segments);
				Vector3 p1 = Curve(span, sag, (float)(k + 1) / segments);
				Vector3 mid = (p0 + p1) * 0.5f;
				mid.y = alt;
				Vector3 d = p1 - p0;
				float angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
				var scale = new Vector3(d.MagnitudeHorizontal() + 0.02f, 1f, width);
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(mid, Quaternion.AngleAxis(-angle, Vector3.up), scale), mat, 0);
			}
		}

		private static void DrawLights(Span span, float alt, float night, float time, int seed)
		{
			float dist = (span.b - span.a).MagnitudeHorizontal();
			if (dist < 0.5f) return;
			float sag = Mathf.Min(MaxSag, dist * SagPerCell) * 0.8f * (span.tight ? TightSag : 1f);
			DrawLine(span, sag, alt, WireWidth, WireMat);
			int count = Mathf.Max(2, Mathf.FloorToInt(dist / BulbSpacing));
			bool lit = night > 0f;
			Material bulb = BulbMat(span.color, lit);
			Material glow = lit ? GlowMat(span.color, night) : null;
			for (int k = 1; k < count; k++)
			{
				Vector3 p = Curve(span, sag, (float)k / count);
				float sway = Mathf.Sin(time * 1.3f + k * 0.9f + seed) * 1.5f;
				Quaternion rot = Quaternion.AngleAxis(sway, Vector3.up);
				Vector3 pos = p + rot * new Vector3(0f, 0f, -BulbDrop);
				if (glow != null)
				{
					Vector3 gp = pos;
					gp.y = alt - Altitudes.AltInc;
					Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(gp, Quaternion.identity, new Vector3(GlowSize, 1f, GlowSize)), glow, 0);
				}
				pos.y = alt + Altitudes.AltInc;
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, BulbSize), bulb, 0);
			}
		}

		private static void DrawSpan(Span span, float alt, float wind, float time, int seed)
		{
			float dist = (span.b - span.a).MagnitudeHorizontal();
			if (dist < 0.5f) return;
			float sag = Mathf.Min(MaxSag, dist * SagPerCell) * (span.tight ? TightSag : 1f);
			DrawLine(span, sag, alt, RopeWidth, RopeMat);
			int count = Mathf.Max(2, Mathf.FloorToInt(dist / PennantSpacing));
			float swayAmp = 3f + 8f * Mathf.Clamp01(wind);
			for (int k = 1; k < count; k++)
			{
				float t = (float)k / count;
				Vector3 p = Curve(span, sag, t);
				float sway = Mathf.Sin(time * 1.7f + k * 1.3f + seed) * swayAmp;
				Quaternion rot = Quaternion.AngleAxis(sway, Vector3.up);
				Vector3 pos = p + rot * new Vector3(0f, 0f, -PennantDrop);
				pos.y = alt + Altitudes.AltInc;
				Material mat = PennantMat(k % 2 == 1 ? span.color : Cream, (k / 2) % 2 == 1);
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, PennantSize), mat, 0);
			}
		}

		private static Material BulbMat(Color color, bool lit)
		{
			Color key = lit ? color : new Color(color.r, color.g, color.b, 0.5f);
			if (bulbMats.TryGetValue(key, out Material mat)) return mat;
			Color tint = lit ? Color.Lerp(color, Color.white, 0.5f) : color * 0.75f;
			tint.a = 1f;
			var req = new MaterialRequest(BulbTex, ShaderDatabase.CutoutComplex, tint)
			{
				colorTwo = Color.white,
				maskTex = BulbMask,
			};
			mat = MaterialPool.MatFrom(req);
			bulbMats[key] = mat;
			return mat;
		}

		private static Material GlowMat(Color color, float night)
		{
			int step = Mathf.Clamp(Mathf.CeilToInt(night * GlowSteps), 1, GlowSteps);
			var key = new Color(color.r, color.g, color.b, step / (float)GlowSteps);
			if (glowMats.TryGetValue(key, out Material mat)) return mat;
			Color tint = color * (0.55f * step / GlowSteps);
			tint.a = 1f;
			mat = MaterialPool.MatFrom("Things/Fair/RimMarket_BulbGlow", ShaderDatabase.MoteGlow, tint);
			glowMats[key] = mat;
			return mat;
		}

		private static Material PennantMat(Color color, bool fork)
		{
			Dictionary<Color, Material> cache = fork ? forkMats : triangleMats;
			if (cache.TryGetValue(color, out Material mat)) return mat;
			var req = new MaterialRequest(fork ? ForkTex : TriangleTex, ShaderDatabase.CutoutComplex, color)
			{
				colorTwo = Color.white,
				maskTex = fork ? ForkMask : TriangleMask,
			};
			mat = MaterialPool.MatFrom(req);
			cache[color] = mat;
			return mat;
		}
	}
}
