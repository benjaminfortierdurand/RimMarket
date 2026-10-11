using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Orders
{
	public class WantedOffer : IExposable
	{
		public int id;
		public Faction faction;
		public Settlement settlement;
		public List<ThingDefCountClass> goods = new List<ThingDefCountClass>();
		public int quality = -1;
		public int price;
		public List<ThingDefCountClass> barter = new List<ThingDefCountClass>();
		public int barterFallback;
		public int tiles;
		public int travelTicks;
		public float risk;
		public Faction threat;
		public bool partial;
		public int expiresTick;
		public bool haggled;

		public bool Barter => barter.Count > 0;

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref settlement, "settlement");
			Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
			Scribe_Values.Look(ref quality, "quality", -1);
			Scribe_Values.Look(ref price, "price");
			Scribe_Collections.Look(ref barter, "barter", LookMode.Deep);
			Scribe_Values.Look(ref barterFallback, "barterFallback");
			Scribe_Values.Look(ref tiles, "tiles");
			Scribe_Values.Look(ref travelTicks, "travelTicks");
			Scribe_Values.Look(ref risk, "risk");
			Scribe_References.Look(ref threat, "threat");
			Scribe_Values.Look(ref partial, "partial");
			Scribe_Values.Look(ref expiresTick, "expiresTick");
			Scribe_Values.Look(ref haggled, "haggled");
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (goods == null) goods = new List<ThingDefCountClass>();
			goods.RemoveAll(g => g == null || g.thingDef == null || g.count <= 0);
			if (barter == null) barter = new List<ThingDefCountClass>();
			barter.RemoveAll(g => g == null || g.thingDef == null || g.count <= 0);
		}
	}

	public class WantedNotice : IExposable
	{
		public MapParent home;
		public List<ThingDefCountClass> goods = new List<ThingDefCountClass>();
		public int postedTick;
		public int repliesEndTick;
		public List<Faction> pending = new List<Faction>();
		public List<int> pendingTicks = new List<int>();
		public List<WantedOffer> offers = new List<WantedOffer>();
		public int nextOfferId = 1;

		public void ExposeData()
		{
			Scribe_References.Look(ref home, "home");
			Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
			Scribe_Values.Look(ref postedTick, "postedTick");
			Scribe_Values.Look(ref repliesEndTick, "repliesEndTick");
			Scribe_Collections.Look(ref pending, "pending", LookMode.Reference);
			Scribe_Collections.Look(ref pendingTicks, "pendingTicks", LookMode.Value);
			Scribe_Collections.Look(ref offers, "offers", LookMode.Deep);
			Scribe_Values.Look(ref nextOfferId, "nextOfferId", 1);
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (goods == null) goods = new List<ThingDefCountClass>();
			goods.RemoveAll(g => g == null || g.thingDef == null || g.count <= 0);
			if (pending == null) pending = new List<Faction>();
			if (pendingTicks == null) pendingTicks = new List<int>();
			if (pending.Count != pendingTicks.Count)
			{
				pending.Clear();
				pendingTicks.Clear();
			}
			for (int i = pending.Count - 1; i >= 0; i--)
			{
				if (pending[i] != null) continue;
				pending.RemoveAt(i);
				pendingTicks.RemoveAt(i);
			}
			if (offers == null) offers = new List<WantedOffer>();
			offers.RemoveAll(o => o == null || o.faction == null || o.goods.Count == 0);
		}
	}
}
