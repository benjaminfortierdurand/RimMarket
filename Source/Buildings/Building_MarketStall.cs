using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class StallStock : ThingOwner<Thing>
	{
		public StallStock()
		{
		}

		public StallStock(IThingHolder owner) : base(owner, oneStackOnly: false)
		{
		}

		public void Unlimit()
		{
			maxStacks = 999999;
		}

		public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
		{
			int count = base.GetCountCanAccept(item, canMergeWithExistingStacks);
			if (item != null && Owner is Building_MarketStall stall) count = Mathf.Min(count, stall.RoomFor(ColonyStalls.StockDef(item)));
			return count;
		}
	}

	public class StallOrder : IExposable
	{
		public ThingDef def;
		public int toSell;
		public float priceFactor = ColonyStalls.DefaultPriceFactor;
		public int sold;
		public QualityRange quality = QualityRange.All;

		public bool Allows(Thing t)
		{
			return !t.TryGetQuality(out QualityCategory q) || quality.Includes(q);
		}

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref toSell, "toSell");
			Scribe_Values.Look(ref priceFactor, "priceFactor", ColonyStalls.DefaultPriceFactor);
			Scribe_Values.Look(ref sold, "sold");
			Scribe_Values.Look(ref quality, "quality", QualityRange.All);
		}
	}

	public class Building_MarketStall : Building, IThingHolderEvents<Thing>, IHaulEnroute, IHaulDestination, IStoreSettingsParent, IHaulSource, IThingHolder, IApparelSource
	{
		private bool colonyStall;
		private StallStock stock;
		private StorageSettings settings;
		private List<StallOrder> orders = new List<StallOrder>();
		public int takings;
		public int sales;
		public Pawn keeper;
		public int keeperTick = -99999;
		private int roofShape = StallRoof.DefaultShape;
		private int roofPattern;
		private Color canvasColor = StallRoof.DefaultCanvas;
		private Color stripeColor = StallRoof.DefaultStripe;
		private StallRoof.RoofLook look;

		public Building_MarketStall()
		{
			stock = new StallStock(this);
		}

		public bool ColonyStall => colonyStall;

		public bool PaintableRoof => Stall?.Props.paintableRoof == true;

		public int RoofShape => roofShape;

		public int RoofPattern => roofPattern;

		public Color CanvasColor => canvasColor;

		public Color StripeColor => stripeColor;

		public StallRoof.RoofLook Look
		{
			get
			{
				Color stuff = DrawColor;
				if (look == null || look.stuff != stuff) look = StallRoof.Build(roofShape, roofPattern, stuff, canvasColor, stripeColor);
				return look;
			}
		}

		public void SetRoof(int shape, int pattern, Color canvas, Color stripe)
		{
			roofShape = StallRoof.ClampShape(shape);
			roofPattern = StallRoof.ClampPattern(pattern);
			canvasColor = canvas;
			stripeColor = stripe;
			look = null;
		}

		public override void Print(SectionLayer layer)
		{
			if (PaintableRoof) return;
			base.Print(layer);
		}

		public IReadOnlyList<Thing> Stock => stock.InnerListForReading;

		public ThingOwner StockOwner => stock;

		public IReadOnlyList<StallOrder> Orders => orders;

		public CompMarketStall Stall => GetComp<CompMarketStall>();

		public bool StorageTabVisible => false;

		public bool HaulDestinationEnabled => colonyStall && orders.Count > 0;

		public bool HaulSourceEnabled => true;

		public bool ApparelSourceEnabled => false;

		public bool RemoveApparel(Apparel apparel) => stock.Remove(apparel);

		public StorageSettings GetStoreSettings() => settings;

		public StorageSettings GetParentStoreSettings() => def.building?.fixedStorageSettings;

		public void Notify_SettingsChanged()
		{
			if (Spawned) MapHeld.listerHaulables.Notify_HaulSourceChanged(this);
		}

		public StallOrder OrderFor(ThingDef def)
		{
			if (def == null) return null;
			for (int i = 0; i < orders.Count; i++)
			{
				if (orders[i].def == def) return orders[i];
			}
			return null;
		}

		public bool Accepts(Thing t)
		{
			if (!colonyStall || t == null) return false;
			StallOrder order = OrderFor(ColonyStalls.StockDef(t));
			if (order == null) return false;
			if (stock.Contains(t)) return true;
			return order.Allows(t) && stock.CanAcceptAnyOf(t);
		}

		public int HeldOf(ThingDef def)
		{
			int n = 0;
			for (int i = 0; i < stock.Count; i++)
			{
				if (ColonyStalls.StockDef(stock[i]) == def) n += stock[i].stackCount;
			}
			return n;
		}

		public int RoomFor(ThingDef def)
		{
			StallOrder order = OrderFor(def);
			if (order == null) return 0;
			return Mathf.Max(0, order.toSell - HeldOf(def));
		}

		public int SpaceRemainingFor(ThingDef stuff)
		{
			if (!colonyStall || stuff == null) return 0;
			if (stuff.thingClass == typeof(MinifiedThing))
			{
				int room = 0;
				for (int i = 0; i < orders.Count; i++)
				{
					if (ColonyStalls.IsArt(orders[i].def)) room += RoomFor(orders[i].def);
				}
				return room;
			}
			return RoomFor(stuff);
		}

		public void SetOrder(ThingDef def, int toSell, float priceFactor, QualityRange quality)
		{
			if (def == null) return;
			StallOrder order = OrderFor(def);
			if (toSell <= 0)
			{
				if (order != null) orders.Remove(order);
			}
			else
			{
				if (order == null)
				{
					order = new StallOrder { def = def };
					orders.Add(order);
				}
				order.toSell = toSell;
				order.priceFactor = priceFactor;
				order.quality = quality;
			}
			TrimStock();
			if (Spawned) MapHeld.listerHaulables.Notify_HaulSourceChanged(this);
		}

		public void Notify_Sold(ThingDef def, int count)
		{
			StallOrder order = OrderFor(def);
			if (order == null) return;
			order.toSell -= count;
			order.sold += count;
			if (order.toSell <= 0) orders.Remove(order);
		}

		private void TrimStock()
		{
			if (!Spawned) return;
			var kept = new Dictionary<ThingDef, int>();
			for (int i = stock.Count - 1; i >= 0; i--)
			{
				Thing t = stock[i];
				ThingDef sold = ColonyStalls.StockDef(t);
				kept.TryGetValue(sold, out int already);
				StallOrder order = OrderFor(sold);
				int keep = order != null && order.Allows(t) ? Mathf.Clamp(order.toSell - already, 0, t.stackCount) : 0;
				kept[sold] = already + keep;
				int excess = t.stackCount - keep;
				if (excess > 0) stock.TryDrop(t, DropCell(), Map, ThingPlaceMode.Near, excess, out _);
			}
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings() => stock;

		public void Notify_ItemAdded(Thing item)
		{
			if (Spawned) MapHeld.listerHaulables.Notify_AddedThing(item);
			Stall?.MarkDisplayDirty();
		}

		public void Notify_ItemRemoved(Thing item)
		{
			if (Spawned) MapHeld.listerHaulables.Notify_DeSpawned(item);
			Stall?.MarkDisplayDirty();
		}

		private void MakeSettings()
		{
			settings = new StorageSettings(this);
			if (def.building?.defaultStorageSettings != null) settings.CopyFrom(def.building.defaultStorageSettings);
			settings.Priority = StoragePriority.Critical;
		}

		public override void PostMake()
		{
			base.PostMake();
			MakeSettings();
		}

		public void SetColonyStall(bool on)
		{
			if (colonyStall == on) return;
			colonyStall = on;
			keeper = null;
			if (!on)
			{
				orders.Clear();
				if (Spawned) DropStock();
			}
			Stall?.MarkDisplayDirty();
			if (Spawned) MapHeld.listerHaulables.Notify_HaulSourceChanged(this);
		}

		private IntVec3 DropCell()
		{
			CompMarketStall s = Stall;
			return s != null && s.CustomerCell.InBounds(Map) ? s.CustomerCell : Position;
		}

		private void DropStock()
		{
			stock.TryDropAll(DropCell(), Map, ThingPlaceMode.Near);
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			if (mode != DestroyMode.WillReplace && Spawned) DropStock();
			base.DeSpawn(mode);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			if (Faction != Faction.OfPlayer) yield break;
			LessonAutoActivator.TeachOpportunity(RimMarketDefOf.RimMarket_Concept_Stall, OpportunityType.GoodToKnow);
			StallVisit visit = Stall?.Visit;
			var toggle = new Command_Toggle
			{
				defaultLabel = "RimMarket.ColonyStallToggle".Translate(),
				defaultDesc = "RimMarket.ColonyStallToggleDesc".Translate(),
				icon = RimMarketIcons.ColonyStall,
				isActive = () => colonyStall,
				toggleAction = delegate
				{
					SetColonyStall(!colonyStall);
					if (colonyStall) Find.WindowStack.Add(new Dialog_ColonyStall(this));
				},
			};
			if (!colonyStall && visit != null) toggle.Disable("RimMarket.ColonyStallBusy".Translate(visit.trader?.LabelShortCap ?? "?"));
			yield return toggle;
			if (PaintableRoof)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimMarket.PaintAwning".Translate(),
					defaultDesc = "RimMarket.PaintAwningDesc".Translate(),
					icon = RimMarketIcons.PaintAwning,
					action = () => Find.WindowStack.Add(new Dialog_StallAwning(this)),
				};
			}
			if (!colonyStall) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.ColonyStallSetup".Translate(),
				defaultDesc = "RimMarket.ColonyStallSetupDesc".Translate(),
				icon = RimMarketIcons.StallSetup,
				action = () => Find.WindowStack.Add(new Dialog_ColonyStall(this)),
			};
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref colonyStall, "colonyStall");
			Scribe_Deep.Look(ref stock, "stock", this);
			Scribe_Deep.Look(ref settings, "settings", this);
			Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
			Scribe_Values.Look(ref takings, "takings");
			Scribe_Values.Look(ref sales, "sales");
			Scribe_Values.Look(ref roofShape, "roofShape", StallRoof.DefaultShape);
			Scribe_Values.Look(ref roofPattern, "roofPattern");
			Scribe_Values.Look(ref canvasColor, "canvasColor", StallRoof.DefaultCanvas);
			Scribe_Values.Look(ref stripeColor, "stripeColor", StallRoof.DefaultStripe);
			if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
			if (stock == null) stock = new StallStock(this);
			stock.Unlimit();
			if (settings == null) MakeSettings();
			else settings.Priority = StoragePriority.Critical;
			if (orders == null) orders = new List<StallOrder>();
			orders.RemoveAll(o => o == null || o.def == null || o.toSell <= 0 || !ColonyStalls.CanSell(o.def));
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (respawningAfterLoad) LongEventHandler.ExecuteWhenFinished(TrimStock);
		}
	}
}
