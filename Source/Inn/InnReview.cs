using System.Collections.Generic;
using Verse;

namespace RimMarket.Inn
{
	public class InnReview : IExposable
	{
		public int tick;
		public string guest;
		public string role;
		public string faction;
		public int tier;
		public int price;
		public int stars;
		public List<string> notes = new List<string>();

		public void ExposeData()
		{
			Scribe_Values.Look(ref tick, "tick");
			Scribe_Values.Look(ref guest, "guest");
			Scribe_Values.Look(ref role, "role");
			Scribe_Values.Look(ref faction, "faction");
			Scribe_Values.Look(ref tier, "tier");
			Scribe_Values.Look(ref price, "price");
			Scribe_Values.Look(ref stars, "stars");
			Scribe_Collections.Look(ref notes, "notes", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && notes == null) notes = new List<string>();
		}
	}
}
