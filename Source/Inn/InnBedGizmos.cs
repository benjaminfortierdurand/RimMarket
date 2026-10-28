using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Inn
{
	public class Command_InnToggle : Command_Toggle
	{
	}

	[StaticConstructorOnStartup]
	public class Command_BedOwnerInn : Command
	{
		private static readonly Texture2D ForColonistsTex = ContentFinder<Texture2D>.Get("UI/Commands/ForColonists");
		private static readonly Texture2D ForSlavesTex = ContentFinder<Texture2D>.Get("UI/Commands/ForSlaves");
		private static readonly Texture2D ForPrisonersTex = ContentFinder<Texture2D>.Get("UI/Commands/ForPrisoners");
		private static readonly Texture2D InnTex = ContentFinder<Texture2D>.Get("UI/Commands/RimMarket_AsInn");

		private readonly Building_Bed bed;

		public Command_BedOwnerInn(Building_Bed bed, Command vanilla)
		{
			this.bed = bed;
			bool inn = InnBeds.IsInn(bed);
			defaultLabel = inn ? "RimMarket.CommandInnBed".Translate().ToString() : vanilla.defaultLabel;
			icon = inn ? InnTex : vanilla.icon;
			defaultDesc = vanilla.defaultDesc + "\n\n" + "RimMarket.CommandInnBedDesc".Translate();
			hotKey = vanilla.hotKey;
		}

		public override void ProcessInput(Event ev)
		{
			base.ProcessInput(ev);
			var list = new List<FloatMenuOption>
			{
				new FloatMenuOption("CommandBedSetForColonistsLabel".Translate(), () => SetOwner(BedOwnerType.Colonist), ForColonistsTex, Color.white),
				new FloatMenuOption("CommandBedSetForPrisonersLabel".Translate(), () => SetOwner(BedOwnerType.Prisoner), ForPrisonersTex, Color.white),
				new FloatMenuOption("CommandBedSetForSlavesLabel".Translate(), () => SetOwner(BedOwnerType.Slave), ForSlavesTex, Color.white),
			};
			string why = InnBlocker(bed);
			string label = "RimMarket.CommandInnBed".Translate();
			list.Add(why == null
				? new FloatMenuOption(label, SetInn, InnTex, Color.white)
				: new FloatMenuOption(label + " (" + why + ")", null, InnTex, Color.white));
			Find.WindowStack.Add(new FloatMenu(list));
		}

		private static List<Building_Bed> Selected() => Find.Selector.SelectedObjects.OfType<Building_Bed>().ToList();

		private void SetOwner(BedOwnerType type)
		{
			if (type == BedOwnerType.Prisoner && !Building_Bed.RoomCanBePrisonCell(bed.GetRoom()) && !bed.ForPrisoners)
			{
				Messages.Message("CommandBedSetForPrisonersFailOutdoors".Translate(), bed, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			List<Building_Bed> beds = Selected();
			for (int i = 0; i < beds.Count; i++) InnBeds.CompOf(beds[i])?.SetInn(false);
			bed.SetBedOwnerTypeByInterface(type);
		}

		private void SetInn()
		{
			List<Building_Bed> beds = Selected();
			for (int i = 0; i < beds.Count; i++)
			{
				if (InnBlocker(beds[i]) == null) InnBeds.CompOf(beds[i])?.SetInn(true);
			}
		}

		public static string InnBlocker(Building_Bed bed)
		{
			CompInnBed comp = InnBeds.CompOf(bed);
			if (comp == null || !comp.Supported) return "RimMarket.InnBedUnsupported".Translate();
			if (comp.inn) return null;
			Room room = bed.GetRoom();
			if (room != null && room.IsPrisonCell) return "RimMarket.InnBedPrisonCell".Translate();
			if (Compat.HotelsBridge.IsGuestBed(bed)) return "RimMarket.InnBedHotelGuest".Translate();
			return null;
		}
	}

	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetGizmos))]
	public static class Patch_BedGizmos_InnOwner
	{
		public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
		{
			List<Gizmo> list = __result.ToList();
			int owner = list.FindIndex(g => g is Command_SetBedOwnerType);
			CompInnBed comp = InnBeds.CompOf(__instance);
			bool merge = owner >= 0 && comp != null && comp.Supported && __instance.Faction == Faction.OfPlayer;
			for (int i = 0; i < list.Count; i++)
			{
				if (merge && list[i] is Command_InnToggle) continue;
				if (merge && i == owner) yield return new Command_BedOwnerInn(__instance, (Command)list[i]);
				else yield return list[i];
			}
		}
	}
}
