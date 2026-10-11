using System.IO;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Debug
{
	public static class PawnExport
	{
		private const int Size = 1024;
		private const int Supersample = 2;
		private const int Margin = 8;
		private const float HumanExtent = 1.2f;
		private const float HumanLift = 0.2f;
		private const float AnimalPadding = 0.15f;

		[DebugAction("RimMarket", "Export pawn as PNG (front)",
			actionType = DebugActionType.ToolMapForPawns,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ExportFront(Pawn p)
		{
			string path = Export(p, Rot4.South);
			if (path != null) Done(path);
		}

		[DebugAction("RimMarket", "Export pawn as PNG (four sides)",
			actionType = DebugActionType.ToolMapForPawns,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ExportAllSides(Pawn p)
		{
			string path = null;
			for (int i = 0; i < 4; i++) path = Export(p, new Rot4(i)) ?? path;
			if (path != null) Done(path);
		}

		private static string Export(Pawn p, Rot4 rot)
		{
			if (p?.Drawer?.renderer == null || p.RaceProps == null) return null;
			float extent = HumanExtent;
			Vector3 offset = new Vector3(0f, 0f, HumanLift);
			if (!p.RaceProps.Humanlike)
			{
				Vector2 draw = p.Drawer.renderer.BodyGraphic?.drawSize ?? Vector2.one;
				extent = Mathf.Max(draw.x, draw.y) / 2f + AnimalPadding;
				offset = Vector3.zero;
			}
			RenderTexture big = RenderTexture.GetTemporary(Size * Supersample, Size * Supersample, 24, RenderTextureFormat.ARGB32);
			RenderTexture small = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
			big.filterMode = FilterMode.Bilinear;
			Find.PawnCacheRenderer.RenderPawn(p, big, offset, 1f / extent, 0f, rot, p.health.hediffSet.HasHead);
			Graphics.Blit(big, small);
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = small;
			var full = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
			full.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
			full.Apply();
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(big);
			RenderTexture.ReleaseTemporary(small);
			Texture2D cut = Trim(full);
			byte[] png = cut.EncodeToPNG();
			if (cut != full) Object.Destroy(cut);
			Object.Destroy(full);
			string folder = Folder;
			Directory.CreateDirectory(folder);
			string name = GenFile.SanitizedFileName(p.LabelShortCap + "_" + p.thingIDNumber + "_" + rot.ToStringHuman()) + ".png";
			string path = Path.Combine(folder, name);
			File.WriteAllBytes(path, png);
			return path;
		}

		private static string Folder => Path.Combine(GenFilePaths.SaveDataFolderPath, "PawnExports");

		private static Texture2D Trim(Texture2D tex)
		{
			Color32[] px = tex.GetPixels32();
			int w = tex.width;
			int h = tex.height;
			int minX = w, minY = h, maxX = -1, maxY = -1;
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					if (px[y * w + x].a == 0) continue;
					if (x < minX) minX = x;
					if (x > maxX) maxX = x;
					if (y < minY) minY = y;
					if (y > maxY) maxY = y;
				}
			}
			if (maxX < 0) return tex;
			minX = Mathf.Max(0, minX - Margin);
			minY = Mathf.Max(0, minY - Margin);
			maxX = Mathf.Min(w - 1, maxX + Margin);
			maxY = Mathf.Min(h - 1, maxY + Margin);
			int cw = maxX - minX + 1;
			int ch = maxY - minY + 1;
			var cut = new Texture2D(cw, ch, TextureFormat.ARGB32, false);
			cut.SetPixels(tex.GetPixels(minX, minY, cw, ch));
			cut.Apply();
			return cut;
		}

		private static void Done(string path)
		{
			Messages.Message("RimMarket: pawn saved to " + path, MessageTypeDefOf.NeutralEvent, historical: false);
			Application.OpenURL("file:///" + Path.GetDirectoryName(path).Replace('\\', '/'));
		}
	}
}
