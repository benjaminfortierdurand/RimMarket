using RimMarket.Buildings;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public static class MarketNews
	{
		public const int Latest = 1;

		public static bool Unseen(MarketManager mgr)
		{
			RimMarketSettings s = RimMarketMod.Settings;
			if (mgr == null || s == null || !s.showNews) return false;
			return mgr.newsSeen < Latest && s.newsSeen < Latest;
		}

		public static void Show()
		{
			if (!Find.WindowStack.IsOpen<Dialog_MarketNews>()) Find.WindowStack.Add(new Dialog_MarketNews());
		}

		public static void MarkSeen()
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr != null) mgr.newsSeen = Latest;
			RimMarketSettings s = RimMarketMod.Settings;
			if (s == null) return;
			s.newsSeen = Latest;
			LoadedModManager.GetMod<RimMarketMod>()?.WriteSettings();
		}
	}

	public class Dialog_MarketNews : Window
	{
		private const float TitleHeight = 38f;
		private const float ArtSize = 240f;
		private const float Gap = 18f;
		private const float SubtitleHeight = 34f;
		private const float ButtonHeight = 36f;
		private const float ButtonWidth = 140f;

		private StallRoof.RoofLook look;

		public override Vector2 InitialSize => new Vector2(Mathf.Min(720f, Verse.UI.screenWidth - 32f), Mathf.Min(420f, Verse.UI.screenHeight - 48f));

		public Dialog_MarketNews()
		{
			doCloseX = true;
			draggable = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, TitleHeight), "RimMarket.News.Title".Translate());
			float top = TitleHeight + 6f;
			float bottom = inRect.height - ButtonHeight - 12f;
			var art = new Rect(0f, top, ArtSize, Mathf.Min(ArtSize, bottom - top));
			Widgets.DrawMenuSection(art);
			if (look == null)
			{
				Color steel = ThingDefOf.Steel.stuffProps != null ? ThingDefOf.Steel.stuffProps.color : Color.gray;
				look = StallRoof.Build(StallRoof.DefaultShape, 0, steel, StallRoof.DefaultCanvas, StallRoof.DefaultStripe);
			}
			float side = Mathf.Min(art.width, art.height) - 8f;
			StallRoof.DrawPreview(new Rect(art.center.x - side / 2f, art.center.y - side / 2f, side, side), look, StallRoof.DefaultShape, 0, false, glass: true);
			float x = ArtSize + Gap;
			Widgets.Label(new Rect(x, top, inRect.width - x, SubtitleHeight), "RimMarket.News.FreezerTitle".Translate());
			Text.Font = GameFont.Small;
			string body = "RimMarket.News.FreezerBody".Translate() + "\n\n" + "RimMarket.News.Also".Translate();
			Widgets.Label(new Rect(x, top + SubtitleHeight, inRect.width - x, bottom - top - SubtitleHeight), body);
			RimMarketSettings s = RimMarketMod.Settings;
			bool show = s.showNews;
			Widgets.CheckboxLabeled(new Rect(0f, inRect.height - ButtonHeight + 4f, 280f, 28f), "RimMarket.News.KeepShowing".Translate(), ref show);
			s.showNews = show;
			if (Widgets.ButtonText(new Rect(inRect.width - ButtonWidth, inRect.height - ButtonHeight, ButtonWidth, ButtonHeight), "OK".Translate())) Close();
		}

		public override void PreClose()
		{
			base.PreClose();
			MarketNews.MarkSeen();
		}
	}
}
