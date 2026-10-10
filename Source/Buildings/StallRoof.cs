using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	[StaticConstructorOnStartup]
	public static class StallRoof
	{
		public const int Shapes = 22;
		public const int Patterns = 13;
		public const int DefaultShape = 1;
		private const string Dir = "Things/Building/Stall/";
		private static readonly Vector2 Size = new Vector2(4f, 4f);
		private const float SouthZ = 0.5f;
		private const float SideZ = -0.05f;
		private const float DepthTopPx = 14f;
		private const float DepthBottomPx = 100f;
		private const float TexPx = 512f;
		private static readonly Rect RoofCoords = new Rect(0.07f, 0.35f, 0.86f, 0.49f);
		private static readonly Vector2 RoofProportions = new Vector2(440f, 251f);

		public static readonly Color DefaultCanvas = new Color(0.64f, 0.6f, 0.49f);
		public static readonly Color DefaultStripe = new Color(0.94f, 0.91f, 0.84f);

		public static readonly List<Color> Palette = new List<Color>
		{
			new Color(0.94f, 0.91f, 0.84f), new Color(0.64f, 0.6f, 0.49f), new Color(0.97f, 0.97f, 0.97f),
			new Color(0.55f, 0.55f, 0.55f), new Color(0.25f, 0.25f, 0.27f), new Color(0.78f, 0.22f, 0.19f),
			new Color(0.5f, 0.12f, 0.12f), new Color(0.88f, 0.5f, 0.18f), new Color(0.9f, 0.75f, 0.25f),
			new Color(0.5f, 0.52f, 0.25f), new Color(0.3f, 0.55f, 0.25f), new Color(0.16f, 0.33f, 0.2f),
			new Color(0.2f, 0.55f, 0.55f), new Color(0.45f, 0.65f, 0.85f), new Color(0.2f, 0.36f, 0.68f),
			new Color(0.13f, 0.18f, 0.38f), new Color(0.5f, 0.28f, 0.6f), new Color(0.9f, 0.55f, 0.65f),
			new Color(0.45f, 0.3f, 0.2f),
		};

		private static readonly Texture2D[] BaseSouth = new Texture2D[Shapes];
		private static readonly Texture2D[] RoofSouth = new Texture2D[Shapes];
		private static readonly Texture2D[] PatternMasks = new Texture2D[Patterns];
		private static readonly Texture2D[] RoofSide = new Texture2D[Patterns];
		private static readonly Texture2D[] RoofSideMasks = new Texture2D[Patterns];
		private static readonly Texture2D BaseSouthMask;
		private static readonly Texture2D BaseSide;
		private static readonly Texture2D BaseSideMask;
		private static readonly Mesh DepthMesh;
		private static readonly Mesh DepthMeshFlip;

		public sealed class RoofLook
		{
			public Color stuff;
			public Material baseSouth;
			public Material roofSouth;
			public Material baseSide;
			public Material roofSide;
		}

		static StallRoof()
		{
			for (int i = 0; i < Shapes; i++)
			{
				BaseSouth[i] = Tex("RimMarket_StallBase" + (i + 1) + "_south");
				RoofSouth[i] = Tex("RimMarket_StallRoof" + (i + 1));
			}
			for (int k = 0; k < Patterns; k++)
			{
				PatternMasks[k] = Tex("RimMarket_StallPattern" + k);
				RoofSide[k] = Tex("RimMarket_StallRoofWest" + k);
				RoofSideMasks[k] = Tex("RimMarket_StallRoofWest" + k + "_m");
			}
			BaseSouthMask = Tex("RimMarket_StallBase1_southm");
			BaseSide = Tex("RimMarket_StallBase1_west");
			BaseSideMask = Tex("RimMarket_StallBase1_westm");
			DepthMesh = StripMesh(false);
			DepthMeshFlip = StripMesh(true);
		}

		private static Texture2D Tex(string name) => ContentFinder<Texture2D>.Get(Dir + name);

		private static Mesh StripMesh(bool flip)
		{
			float half = Size.x / 2f;
			float top = Size.y / 2f - DepthTopPx / TexPx * Size.y;
			float bottom = Size.y / 2f - DepthBottomPx / TexPx * Size.y;
			float vTop = 1f - DepthTopPx / TexPx;
			float vBottom = 1f - DepthBottomPx / TexPx;
			float u0 = flip ? 1f : 0f;
			float u1 = flip ? 0f : 1f;
			var mesh = new Mesh
			{
				name = "RimMarket_StallDepth",
				vertices = new[] { new Vector3(-half, 0f, bottom), new Vector3(-half, 0f, top), new Vector3(half, 0f, top), new Vector3(half, 0f, bottom) },
				uv = new[] { new Vector2(u0, vBottom), new Vector2(u0, vTop), new Vector2(u1, vTop), new Vector2(u1, vBottom) },
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
			};
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		public static int ClampShape(int shape) => Mathf.Clamp(shape, 1, Shapes);

		public static int ClampPattern(int pattern) => Mathf.Clamp(pattern, 0, Patterns - 1);

		private static Material Mat(Texture2D tex, Texture2D mask, Color one, Color two)
		{
			var req = new MaterialRequest(tex, ShaderDatabase.CutoutComplex, one)
			{
				colorTwo = two,
				maskTex = mask,
			};
			return MaterialPool.MatFrom(req);
		}

		public static RoofLook Build(int shape, int pattern, Color stuff, Color canvas, Color stripe)
		{
			int s = ClampShape(shape) - 1;
			int p = ClampPattern(pattern);
			return new RoofLook
			{
				stuff = stuff,
				baseSouth = Mat(BaseSouth[s], BaseSouthMask, stuff, canvas),
				roofSouth = Mat(RoofSouth[s], PatternMasks[p], stripe, canvas),
				baseSide = Mat(BaseSide, BaseSideMask, stuff, canvas),
				roofSide = Mat(RoofSide[p], RoofSideMasks[p], stripe, canvas),
			};
		}

		public static void Draw(Building_MarketStall stall)
		{
			RoofLook look = stall.Look;
			Rot4 rot = stall.Rotation;
			bool side = rot.IsHorizontal;
			bool flip = rot == Rot4.East;
			Vector3 pos = stall.TrueCenter() + new Vector3(0f, 0f, side ? SideZ : SouthZ);
			Mesh plane = flip ? MeshPool.GridPlaneFlip(Size) : MeshPool.GridPlane(Size);
			pos.y = AltitudeLayer.Building.AltitudeFor();
			Graphics.DrawMesh(plane, pos, Quaternion.identity, side ? look.baseSide : look.baseSouth, 0);
			if (side)
			{
				pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + Altitudes.AltInc;
				Graphics.DrawMesh(plane, pos, Quaternion.identity, look.roofSide, 0);
				pos.y = AltitudeLayer.PawnState.AltitudeFor();
				Graphics.DrawMesh(flip ? DepthMeshFlip : DepthMesh, pos, Quaternion.identity, look.roofSide, 0);
				return;
			}
			pos.y = AltitudeLayer.PawnState.AltitudeFor();
			Graphics.DrawMesh(plane, pos, Quaternion.identity, look.roofSouth, 0);
		}

		public static void DrawPreview(Rect rect, RoofLook look, int shape, int pattern, bool side)
		{
			if (side)
			{
				Widgets.DrawTextureFitted(rect, BaseSide, 1f, look.baseSide);
				Widgets.DrawTextureFitted(rect, RoofSide[ClampPattern(pattern)], 1f, look.roofSide);
				return;
			}
			Widgets.DrawTextureFitted(rect, BaseSouth[ClampShape(shape) - 1], 1f, look.baseSouth);
			Widgets.DrawTextureFitted(rect, RoofSouth[ClampShape(shape) - 1], 1f, look.roofSouth);
		}

		public static void DrawRoofThumb(Rect rect, int shape, int pattern, Color canvas, Color stripe)
		{
			Texture2D tex = RoofSouth[ClampShape(shape) - 1];
			Material mat = Mat(tex, PatternMasks[ClampPattern(pattern)], stripe, canvas);
			Widgets.DrawTextureFitted(rect, tex, 0.9f, RoofProportions, RoofCoords, 0f, mat);
		}
	}
}
