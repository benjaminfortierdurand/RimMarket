using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Fair
{
	[StaticConstructorOnStartup]
	public class Window_GreatFair : Window
	{
		private const float MaxWidth = 860f;
		private const float MaxHeight = 780f;
		private const float TitleHeight = 40f;
		private const float Gap = 12f;
		private const float ButtonHeight = 38f;
		private const float ButtonWidth = 220f;

		private static readonly Texture2D OpenArt = ContentFinder<Texture2D>.Get("UI/Events/RimMarket_FairOpen", false);
		private static readonly Texture2D CloseArt = ContentFinder<Texture2D>.Get("UI/Events/RimMarket_FairClose", false);

		private readonly string title;
		private readonly string body;
		private readonly Texture2D art;
		private readonly string lookLabel;
		private readonly GlobalTargetInfo look;
		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(Mathf.Min(MaxWidth, Verse.UI.screenWidth - 32f), Mathf.Min(MaxHeight, Verse.UI.screenHeight - 48f));

		private Window_GreatFair(string title, string body, Texture2D art, string lookLabel, GlobalTargetInfo look)
		{
			this.title = title;
			this.body = body;
			this.art = art;
			this.lookLabel = lookLabel;
			this.look = look;
			forcePause = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = false;
			closeOnAccept = false;
			closeOnCancel = true;
			doCloseX = false;
			doCloseButton = false;
			soundAppear = SoundDefOf.LetterArrive;
		}

		public static void ShowOpening(GreatFair fair, string body)
		{
			Find.WindowStack.Add(new Window_GreatFair("RimMarket.FairOpenTitle".Translate(), body, OpenArt,
				"RimMarket.FairGoToSquare".Translate(), Target(fair.marker)));
		}

		public static void ShowClosing(GreatFair fair, string label, string body, LetterDef def)
		{
			Thing winner = fair.winnerStall != null && fair.winnerStall.Spawned ? fair.winnerStall : null;
			string lookLabel = winner != null ? "RimMarket.FairGoToWinner".Translate() : "RimMarket.FairGoToSquare".Translate();
			Find.WindowStack.Add(new Window_GreatFair(label, body, CloseArt, lookLabel, Target(winner ?? fair.marker)));
			Letter letter = LetterMaker.MakeLetter(label, body, def, new LookTargets(fair.marker));
			letter.arrivalTick = Find.TickManager.TicksGame;
			Find.Archive.Add(letter);
		}

		private static GlobalTargetInfo Target(Thing t) => t != null && t.Spawned ? new GlobalTargetInfo(t) : GlobalTargetInfo.Invalid;

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), title);
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			float y = TitleHeight + Gap / 2f;
			if (art != null)
			{
				float h = Mathf.Min(inRect.width * art.height / art.width, (inRect.height - y - ButtonHeight - Gap * 3f) * 0.62f);
				var picture = new Rect(0f, y, inRect.width, h);
				GUI.DrawTexture(picture, art, ScaleMode.ScaleAndCrop);
				Widgets.DrawBox(picture, 2);
				y += h + Gap;
			}
			var text = new Rect(0f, y, inRect.width, inRect.height - y - ButtonHeight - Gap);
			Widgets.LabelScrollable(text, body, ref scroll);
			float by = inRect.height - ButtonHeight;
			bool canLook = look.IsValid;
			float total = canLook ? ButtonWidth * 2f + Gap : ButtonWidth;
			float x = (inRect.width - total) / 2f;
			if (canLook && Widgets.ButtonText(new Rect(x, by, ButtonWidth, ButtonHeight), lookLabel))
			{
				Close();
				CameraJumper.TryJumpAndSelect(look);
			}
			if (canLook) x += ButtonWidth + Gap;
			if (Widgets.ButtonText(new Rect(x, by, ButtonWidth, ButtonHeight), "RimMarket.FairContinue".Translate())) Close();
		}
	}
}
