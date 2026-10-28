using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Inn
{
	public class CompProperties_InnBed : CompProperties
	{
		public CompProperties_InnBed()
		{
			compClass = typeof(CompInnBed);
		}
	}

	[StaticConstructorOnStartup]
	public class CompInnBed : ThingComp
	{
		private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Commands/RimMarket_AsInn");

		public bool inn;
		public int nights;
		public int earned;

		public Building_Bed Bed => parent as Building_Bed;

		public bool Supported
		{
			get
			{
				Building_Bed bed = Bed;
				return bed != null && bed.def.building != null && bed.def.building.bed_humanlike && !bed.ForHumanBabies
					&& bed.GetType().Namespace != "Hospitality";
			}
		}

		public void SetInn(bool value)
		{
			Building_Bed bed = Bed;
			if (bed == null || value == inn || (value && !Supported)) return;
			if (value)
			{
				if (bed.Medical) bed.Medical = false;
				if (bed.ForOwnerType != BedOwnerType.Colonist) bed.ForOwnerType = BedOwnerType.Colonist;
			}
			inn = value;
			InnUtility.EvictAll(bed);
			if (!bed.Spawned) return;
			if (inn) InnBeds.Register(bed);
			else InnBeds.Unregister(bed, bed.Map);
			bed.Notify_ColorChanged();
			bed.Map.mapDrawer.MapMeshDirty(bed.Position, MapMeshFlagDefOf.Things);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (inn && !Supported) inn = false;
			if (inn) InnBeds.Register(Bed);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
			InnBeds.Unregister(Bed, map);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!Supported || parent.Faction != Faction.OfPlayer) yield break;
			var toggle = new Command_InnToggle
			{
				defaultLabel = "RimMarket.CommandInnBed".Translate(),
				defaultDesc = "RimMarket.CommandInnBedDesc".Translate(),
				icon = Icon,
				isActive = () => inn,
				toggleAction = () => SetInn(!inn),
			};
			Room room = parent.GetRoom();
			if (!inn && room != null && room.IsPrisonCell) toggle.Disable("RimMarket.InnBedPrisonCell".Translate());
			else if (!inn && Compat.HotelsBridge.IsGuestBed(Bed)) toggle.Disable("RimMarket.InnBedHotelGuest".Translate());
			yield return toggle;
			if (!inn) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.InnRegisterLabel".Translate(),
				defaultDesc = "RimMarket.InnRegisterDesc".Translate(),
				icon = UI.RimMarketIcons.InnRegister,
				action = UI.Dialog_InnRegister.Open,
			};
		}

		public override string CompInspectStringExtra()
		{
			if (!inn) return null;
			Building_Bed bed = Bed;
			int price = InnUtility.PriceFor(bed);
			string room = InnUtility.TierLabel(InnUtility.Tier(bed));
			return price > 0 ? "RimMarket.InnBedInspect".Translate(price, room).ToString() : "RimMarket.InnBedInspectFree".Translate(room).ToString();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref inn, "rimMarketInn");
			Scribe_Values.Look(ref nights, "rimMarketInnNights");
			Scribe_Values.Look(ref earned, "rimMarketInnEarned");
		}
	}

	[StaticConstructorOnStartup]
	public static class InnBedInjector
	{
		static InnBedInjector()
		{
			List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < defs.Count; i++)
			{
				ThingDef def = defs[i];
				if (def.thingClass == null || !typeof(Building_Bed).IsAssignableFrom(def.thingClass)) continue;
				if (def.building == null || !def.building.bed_humanlike || def.defName == "DeathrestCasket") continue;
				if (!def.HasAssignableCompFrom(typeof(CompAssignableToPawn_Bed))) continue;
				if (def.comps == null) def.comps = new List<CompProperties>();
				bool has = false;
				for (int j = 0; j < def.comps.Count; j++)
				{
					if (def.comps[j] is CompProperties_InnBed) has = true;
				}
				if (!has) def.comps.Add(new CompProperties_InnBed());
			}
		}
	}
}
