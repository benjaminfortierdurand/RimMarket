using System.Collections.Generic;
using RimMarket.Routes;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Orders
{
	public class SpecialOrder : IExposable
	{
		public int id;
		public Faction faction;
		public Settlement settlement;
		public MapParent home;
		public TraderKindDef kind;
		public string family;
		public bool large;
		public List<ThingDefCountClass> goods = new List<ThingDefCountClass>();
		public int paid;
		public int placedTick;
		public int departTick;
		public bool departed;
		public bool delivered;
		public TradeConvoy convoy;
		public int arrivedLordId = -1;
		public int pathFailures;
		public int quality = -1;
		public List<ThingDefCountClass> barter = new List<ThingDefCountClass>();
		public int barterFallback;
		public int arrivedTick = -1;
		public bool handedOver;
		public Pawn courier;
		public List<Thing> parcel = new List<Thing>();

		public bool Specific => family.NullOrEmpty();

		public string SettlementName => settlement?.LabelCap ?? "?";

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref settlement, "settlement");
			Scribe_References.Look(ref home, "home");
			Scribe_Defs.Look(ref kind, "kind");
			Scribe_Values.Look(ref family, "family");
			Scribe_Values.Look(ref large, "large");
			Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
			Scribe_Values.Look(ref paid, "paid");
			Scribe_Values.Look(ref placedTick, "placedTick");
			Scribe_Values.Look(ref departTick, "departTick");
			Scribe_Values.Look(ref departed, "departed");
			Scribe_Values.Look(ref delivered, "delivered");
			Scribe_References.Look(ref convoy, "convoy");
			Scribe_Values.Look(ref arrivedLordId, "arrivedLordId", -1);
			Scribe_Values.Look(ref pathFailures, "pathFailures");
			Scribe_Values.Look(ref quality, "quality", -1);
			Scribe_Collections.Look(ref barter, "barter", LookMode.Deep);
			Scribe_Values.Look(ref barterFallback, "barterFallback");
			Scribe_Values.Look(ref arrivedTick, "arrivedTick", -1);
			Scribe_Values.Look(ref handedOver, "handedOver");
			Scribe_References.Look(ref courier, "courier");
			Scribe_Collections.Look(ref parcel, "parcel", LookMode.Reference);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (goods == null) goods = new List<ThingDefCountClass>();
				goods.RemoveAll(g => g == null || g.thingDef == null || g.count <= 0);
				if (parcel == null) parcel = new List<Thing>();
				parcel.RemoveAll(t => t == null);
				if (barter == null) barter = new List<ThingDefCountClass>();
				barter.RemoveAll(g => g == null || g.thingDef == null || g.count <= 0);
			}
		}
	}
}
