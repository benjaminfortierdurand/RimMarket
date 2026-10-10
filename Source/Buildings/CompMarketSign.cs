using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimMarket.Compat;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class MarketSignVariant
	{
		public string texPath;
		public string holoTexPath;
		public string holoBandTexPath;
		public List<TraderKindDef> traderKinds = new List<TraderKindDef>();
	}

	public class CompProperties_MarketSign : CompProperties
	{
		public string litTexPath;
		public string frameTexPath;
		public List<MarketSignVariant> variants = new List<MarketSignVariant>();
		public Vector2 drawSize = Vector2.one;
		public Vector3 drawOffset;
		public string holoTexPath;
		public string holoBandTexPath;
		public string holoPostsTexPath;
		public float holoSize = 1.65f;
		public Vector3 holoOffset;

		public CompProperties_MarketSign()
		{
			compClass = typeof(CompMarketSign);
		}

		public MarketSignVariant VariantFor(TraderKindDef kind)
		{
			if (kind == null || variants == null) return null;
			for (int i = 0; i < variants.Count; i++)
			{
				MarketSignVariant v = variants[i];
				if (v?.traderKinds != null && v.traderKinds.Contains(kind)) return v;
			}
			return null;
		}
	}

	public class CompMarketSign : ThingComp, IThingGlower
	{
		private const float BobAmplitude = 0.025f;

		private static readonly FieldInfo GlowColorField = AccessTools.Field(typeof(CompGlower), "glowColorOverride");

		private CompMarketStall stall;
		private CompGlower glower;
		private readonly Dictionary<string, Graphic> graphics = new Dictionary<string, Graphic>();

		public CompProperties_MarketSign Props => (CompProperties_MarketSign)props;

		private CompMarketStall Stall => stall ?? (stall = parent.GetComp<CompMarketStall>());

		private CompGlower Glower => glower ?? (glower = parent.GetComp<CompGlower>());

		private StallVisit Visit => Stall != null && MarketManager.AnyVisits ? MarketManager.Instance.VisitFor(Stall) : null;

		private bool HoloMode => !Props.holoTexPath.NullOrEmpty() && EgiHologramBridge.Active;

		public bool ShouldBeLitNow()
		{
			return Visit != null;
		}

		public bool Lit => Glower != null ? Glower.Glows : ShouldBeLitNow();

		public void Refresh()
		{
			if (parent.Spawned) Glower?.UpdateLit(parent.Map);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (Glower == null || GlowColorField == null) return;
			ColorInt? wanted = null;
			if (HoloMode)
			{
				Color c = EgiHologramBridge.HologramColor;
				wanted = new ColorInt(c) { a = 0 };
			}
			GlowColorField.SetValue(Glower, wanted);
		}

		public override void PostDraw()
		{
			if (HoloMode) DrawHologram();
			else DrawNeon();
		}

		private void DrawNeon()
		{
			Vector3 loc = parent.DrawPos + Props.drawOffset;
			loc.y = AltitudeLayer.PawnState.AltitudeFor();
			if (!Props.frameTexPath.NullOrEmpty())
				GraphicFor(Props.frameTexPath, ShaderDatabase.Cutout).Draw(loc, Rot4.North, parent);
			if (!Lit) return;
			string path = Props.VariantFor(Visit?.trader?.TraderKind)?.texPath ?? Props.litTexPath;
			if (path.NullOrEmpty()) return;
			loc.y += 0.01f;
			GraphicFor(path, ShaderDatabase.TransparentPostLight).Draw(loc, Rot4.North, parent);
		}

		private void DrawHologram()
		{
			Vector3 loc = parent.DrawPos + Props.drawOffset;
			loc.y = AltitudeLayer.PawnState.AltitudeFor();
			if (!Props.holoPostsTexPath.NullOrEmpty())
				GraphicFor(Props.holoPostsTexPath, ShaderDatabase.Cutout).Draw(loc, Rot4.North, parent);
			if (!Lit) return;
			MarketSignVariant variant = Props.VariantFor(Visit?.trader?.TraderKind);
			string band = variant?.holoBandTexPath ?? Props.holoBandTexPath;
			if (!band.NullOrEmpty())
			{
				loc.y += 0.01f;
				GraphicFor(band, ShaderDatabase.TransparentPostLight).Draw(loc, Rot4.North, parent);
			}
			Material mat = EgiHologramBridge.MaterialFor(variant?.holoTexPath ?? Props.holoTexPath);
			if (mat == null) return;
			Vector3 pos = parent.DrawPos + Props.holoOffset;
			pos.z += Mathf.Sin(Find.TickManager.TicksGame / 60f) * BobAmplitude;
			pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
			Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(Props.holoSize, 1f, Props.holoSize));
			Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
		}

		private Graphic GraphicFor(string path, Shader shader)
		{
			if (!graphics.TryGetValue(path, out Graphic g))
			{
				g = GraphicDatabase.Get<Graphic_Single>(path, shader, Props.drawSize, Color.white);
				graphics[path] = g;
			}
			return g;
		}
	}
}
