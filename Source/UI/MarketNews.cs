using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.UI
{
	public static class MarketNews
	{
		public const int Latest = 2;

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
		private const float ArtHeight = 260f;
		private const float Gap = 12f;
		private const float HeadlineHeight = 34f;
		private const float ButtonHeight = 36f;
		private const float ButtonWidth = 140f;
		private const string ArtPath = "UI/News/RimMarket_News";

		private Texture2D art;
		private bool artLooked;

		public override Vector2 InitialSize => new Vector2(Mathf.Min(680f, Verse.UI.screenWidth - 32f), Mathf.Min(710f, Verse.UI.screenHeight - 48f));

		public Dialog_MarketNews()
		{
			doCloseX = true;
			draggable = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		private static string L(string key) => ("RimMarket.News" + MarketNews.Latest + "." + key).Translate();

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, TitleHeight), "RimMarket.News.Title".Translate());
			float y = TitleHeight + 6f;
			float bottom = inRect.height - ButtonHeight - 12f;
			if (!artLooked)
			{
				artLooked = true;
				art = ContentFinder<Texture2D>.Get(ArtPath + MarketNews.Latest, false);
			}
			if (art != null)
			{
				var frame = new Rect(0f, y, inRect.width, Mathf.Min(ArtHeight, inRect.width * art.height / art.width));
				GUI.DrawTexture(frame, art, ScaleMode.ScaleAndCrop);
				Widgets.DrawBox(frame);
				y = frame.yMax + Gap;
			}
			Widgets.Label(new Rect(0f, y, inRect.width, HeadlineHeight), L("Headline"));
			y += HeadlineHeight;
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(0f, y, inRect.width, bottom - y), L("Body") + "\n\n" + L("Also"));
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
