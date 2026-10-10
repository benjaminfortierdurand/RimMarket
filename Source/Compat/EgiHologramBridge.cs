using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimMarket.Compat
{
	public static class EgiHologramBridge
	{
		public const string PackageId = "vesper.egihologramsandprojectors";
		private const string ProjectionPrefix = "GenericProjections/";

		private static readonly Color DefaultColor = new Color(0f, 2f / 3f, 1f);
		private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

		private static bool resolved;
		private static bool active;
		private static Shader shader;
		private static FieldInfo colorField;

		public static bool Active
		{
			get
			{
				Resolve();
				return active;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			ModContentPack pack = LoadedModManager.RunningModsListForReading.Find(m => m.PackageId == PackageId);
			if (pack == null) return;
			active = true;
			try
			{
				List<AssetBundle> bundles = pack.assetBundles?.loadedAssetBundles;
				if (bundles != null)
				{
					for (int i = 0; i < bundles.Count && shader == null; i++)
						shader = bundles[i]?.LoadAsset<Shader>("s_Hologram");
				}
				if (shader != null && !shader.isSupported) shader = null;
				Type settings = AccessTools.TypeByName("EGIHologramsAndProjectors.EGISettings");
				if (settings != null) colorField = AccessTools.Field(settings, "BaseHologramColour");
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] EGI hologram shader unavailable, falling back to a plain glow: " + e.Message);
				shader = null;
			}
		}

		public static Color HologramColor
		{
			get
			{
				Resolve();
				try
				{
					if (colorField != null) return (Color)colorField.GetValue(null);
				}
				catch
				{
				}
				return DefaultColor;
			}
		}

		public static Material MaterialFor(string texPath)
		{
			if (texPath.NullOrEmpty() || !Active) return null;
			if (materials.TryGetValue(texPath, out Material cached)) return cached;
			Texture2D tex = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
			if (tex == null)
			{
				Log.Warning("[RimMarket] EGI hologram texture not found: " + texPath);
				materials[texPath] = null;
				return null;
			}
			var mat = new Material(shader ?? ShaderDatabase.MoteGlow);
			mat.SetTexture("_MainTex", tex);
			if (shader != null)
			{
				SetMask(mat, "_Mask1", "ProjectionMasks25", texPath);
				SetMask(mat, "_Mask2", "ProjectionMasks50", texPath);
				SetMask(mat, "_Mask3", "ProjectionMasks75", texPath);
			}
			mat.color = HologramColor;
			materials[texPath] = mat;
			return mat;
		}

		private static void SetMask(Material mat, string property, string folder, string texPath)
		{
			if (!texPath.StartsWith(ProjectionPrefix)) return;
			Texture2D mask = ContentFinder<Texture2D>.Get(folder + "/" + texPath.Substring(ProjectionPrefix.Length), reportFailure: false);
			if (mask != null) mat.SetTexture(property, mask);
		}
	}
}
