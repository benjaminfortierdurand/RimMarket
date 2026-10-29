using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimMarket.Auction
{
	public static class AuctionChant
	{
		private const int StrongLevel = 6;
		private const int WeakLevel = 3;

		private static readonly string[] Openers = { "RimMarket.ChantOpen1", "RimMarket.ChantOpen2", "RimMarket.ChantOpen3", "RimMarket.ChantOpen4" };
		private static readonly string[] More = { "RimMarket.ChantMore1", "RimMarket.ChantMore2", "RimMarket.ChantMore3" };
		private static readonly string[] Weak = { "RimMarket.ChantWeak1", "RimMarket.ChantWeak2", "RimMarket.ChantWeak3" };
		private static readonly string[] Nudges = { "RimMarket.ChantNudge1", "RimMarket.ChantNudge2", "RimMarket.ChantNudge3" };
		private static readonly string[] Bids = { "RimMarket.ChantBid1", "RimMarket.ChantBid2", "RimMarket.ChantBid3", "RimMarket.ChantBid4" };
		private static readonly string[] Solds = { "RimMarket.ChantSold1", "RimMarket.ChantSold2", "RimMarket.ChantSold3" };
		private static readonly string[] Nobodies = { "RimMarket.ChantNoTakers1", "RimMarket.ChantNoTakers2" };

		private static string Any(string[] keys) => keys.RandomElement();

		private static List<SkillRecord> Ranked(Pawn p)
		{
			if (p?.skills?.skills == null) return new List<SkillRecord>();
			return p.skills.skills.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).ThenByDescending(s => (int)s.passion).ToList();
		}

		public static string Opening(Pawn p, int lot)
		{
			string name = p?.LabelShortCap ?? "?";
			List<SkillRecord> skills = Ranked(p);
			if (skills.Count == 0 || skills[0].Level < StrongLevel) return "RimMarket.ChantOpenPlain".Translate(lot, name);
			SkillRecord best = skills[0];
			string key = best.passion == Passion.Major ? "RimMarket.ChantOpenPassion" : Any(Openers);
			return key.Translate(lot, name, best.def.skillLabel, best.Level);
		}

		public static string Pitch(Pawn p)
		{
			string name = p?.LabelShortCap ?? "?";
			List<SkillRecord> skills = Ranked(p);
			SkillRecord second = skills.Count > 1 && skills[1].Level >= StrongLevel ? skills[1] : null;
			SkillRecord worst = skills.Count > 2 && skills[skills.Count - 1].Level <= WeakLevel ? skills[skills.Count - 1] : null;
			if (worst != null && (second == null || Rand.Bool)) return Any(Weak).Translate(name, worst.def.skillLabel);
			if (second != null) return Any(More).Translate(name, second.def.skillLabel, second.Level);
			return "RimMarket.ChantMorePlain".Translate(name);
		}

		public static string Nudge(int price) => Any(Nudges).Translate(price);

		public static string Bid(int price, int next) => Any(Bids).Translate(price, next);

		public static string GoingOnce(int price) => "RimMarket.ChantGoingOnce".Translate(price);

		public static string GoingTwice(int price) => "RimMarket.ChantGoingTwice".Translate(price);

		public static string LastCall(int price) => "RimMarket.ChantLastCall".Translate(price);

		public static string Sold(int price) => Any(Solds).Translate(price);

		public static string Nobody() => Any(Nobodies).Translate();
	}
}
