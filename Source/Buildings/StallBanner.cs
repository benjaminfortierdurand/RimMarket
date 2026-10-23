using System.Collections.Generic;
using RimMarket.Fair;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	[StaticConstructorOnStartup]
	public static class StallBanner
	{
		private static readonly Vector3 Size = new Vector3(0.5f, 1f, 0.75f);
		private static readonly Vector3 IconSize = new Vector3(0.24f, 1f, 0.24f);
		private const float IconRise = 0.03f;
		private const float BaseSway = 1.5f;
		private const float WindSway = 3f;
		private const float DarkIconLuma = 0.62f;
		private static readonly Color LightIcon = new Color(0.97f, 0.93f, 0.84f);
		private static readonly Color DarkIcon = new Color(0.16f, 0.12f, 0.1f);

		private static readonly Texture2D ClothTex = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_Banner");
		private static readonly Texture2D ClothMask = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_Banner_m");
		private static readonly Texture2D ModernTex = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_BannerModern");
		private static readonly Texture2D ModernMask = ContentFinder<Texture2D>.Get("Things/Fair/RimMarket_BannerModern_m");
		private static readonly Dictionary<Color, Material> clothMats = new Dictionary<Color, Material>();
		private static readonly Dictionary<Color, Material> modernMats = new Dictionary<Color, Material>();
		private static readonly Dictionary<Texture2D, Material> lightIcons = new Dictionary<Texture2D, Material>();
		private static readonly Dictionary<Texture2D, Material> darkIcons = new Dictionary<Texture2D, Material>();

		public static void Draw(CompMarketStall stall, Faction faction)
		{
			Color color = MapComponent_FairBunting.Festive(faction);
			Rot4 rot = stall.parent.Rotation;
			Vector3 offset = rot.IsHorizontal ? stall.Props.bannerOffsetVertical : stall.Props.bannerOffset;
			if (rot == Rot4.West) offset.x = -offset.x;
			if (stall.flipped)
			{
				if (rot.IsHorizontal) offset.z = -offset.z;
				else offset.x = -offset.x;
			}
			Vector3 top = stall.parent.TrueCenter() + offset + new Vector3(0f, 0f, Size.z / 2f);
			float wind = stall.Map?.windManager.WindSpeed ?? 0f;
			float sway = Mathf.Sin(Time.realtimeSinceStartup * 1.3f + stall.parent.thingIDNumber) * (BaseSway + WindSway * Mathf.Clamp01(wind));
			Quaternion q = Quaternion.AngleAxis(sway, Vector3.up);
			Vector3 pos = top + q * new Vector3(0f, 0f, -Size.z / 2f);
			pos.y = AltitudeLayer.PawnState.AltitudeFor() + Altitudes.AltInc;
			Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, q, Size), ClothMat(color, stall.Modern), 0);
			Material icon = IconMat(faction, color);
			if (icon == null) return;
			Vector3 ip = pos + q * new Vector3(0f, 0f, IconRise);
			ip.y += Altitudes.AltInc * 0.5f;
			Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(ip, q, IconSize), icon, 0);
		}

		private static Material ClothMat(Color color, bool modern)
		{
			Dictionary<Color, Material> cache = modern ? modernMats : clothMats;
			if (cache.TryGetValue(color, out Material mat)) return mat;
			var req = new MaterialRequest(modern ? ModernTex : ClothTex, ShaderDatabase.CutoutComplex, color)
			{
				colorTwo = Color.white,
				maskTex = modern ? ModernMask : ClothMask,
			};
			mat = MaterialPool.MatFrom(req);
			cache[color] = mat;
			return mat;
		}

		private static Material IconMat(Faction faction, Color cloth)
		{
			Texture2D tex = faction.def?.FactionIcon;
			if (tex == null || tex == BaseContent.BadTex) return null;
			bool dark = cloth.r * 0.299f + cloth.g * 0.587f + cloth.b * 0.114f > DarkIconLuma;
			Dictionary<Texture2D, Material> cache = dark ? darkIcons : lightIcons;
			if (cache.TryGetValue(tex, out Material mat)) return mat;
			mat = MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.Transparent, dark ? DarkIcon : LightIcon));
			cache[tex] = mat;
			return mat;
		}
	}
}
