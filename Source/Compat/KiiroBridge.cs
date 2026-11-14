using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMarket.Compat
{
	/// <summary>
	/// Optional bridge to Kiiro Story: Events Expanded. A Kiiro cashier serves its own
	/// customer queue through its own jobs, so RimMarket only ever touches it through
	/// reflection: the assigned merchant, the queue it drains, and the two interaction
	/// cells Kiiro uses for the merchant and the customer.
	/// </summary>
	public static class KiiroBridge
	{
		public const string PackageId = "Ancot.KiiroStoryEventsExpanded";

		private const int ErrorKey = 0x524D4B04;
		private const string CashierDefName = "Kiiro_Cashier";

		private static bool activeResolved;
		private static bool active;
		private static bool resolved;
		private static Type cashierType;
		private static ThingDef cashierDef;
		private static FieldInfo queueField;
		private static MethodInfo merchantGetter;
		private static WorkTypeDef basicWorker;

		public static bool Active
		{
			get
			{
				if (!activeResolved)
				{
					activeResolved = true;
					active = ModsConfig.IsActive(PackageId);
				}
				return active;
			}
		}

		public static bool Available
		{
			get
			{
				Resolve();
				return cashierType != null && cashierDef != null && queueField != null && merchantGetter != null;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			if (!Active) return;
			try
			{
				cashierType = AccessTools.TypeByName("Kiiro_Event.Building_Cashier");
				if (cashierType == null) return;
				cashierDef = DefDatabase<ThingDef>.GetNamedSilentFail(CashierDefName);
				queueField = cashierType.GetField("customerQueue", BindingFlags.Public | BindingFlags.Instance);
				if (queueField != null && queueField.FieldType != typeof(List<Pawn>)) queueField = null;
				merchantGetter = AccessTools.PropertyGetter(cashierType, "AssignedPawn");
				basicWorker = BasicWorker();
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Kiiro bridge unavailable: " + e.Message);
				cashierType = null;
				cashierDef = null;
				queueField = null;
				merchantGetter = null;
			}
		}

		private static WorkTypeDef BasicWorker()
		{
			try
			{
				Type defOf = AccessTools.TypeByName("Kiiro_Event.KiiroDefOf");
				return defOf?.GetField("BasicWorker", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as WorkTypeDef;
			}
			catch
			{
				return null;
			}
		}

		public static bool IsCashier(Thing t) => t != null && Available && cashierType.IsInstanceOfType(t);

		/// <summary>The pawn Kiiro assigned to this cashier, or null when nobody is posted.</summary>
		public static Pawn MerchantOf(Thing cashier)
		{
			if (!IsCashier(cashier)) return null;
			try
			{
				return merchantGetter.Invoke(cashier, null) as Pawn;
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] could not read the Kiiro cashier merchant: " + e.Message, ErrorKey);
				return null;
			}
		}

		/// <summary>Mirrors Kiiro's own gate: the merchant has to be on the work shift.</summary>
		public static bool OnDuty(Pawn merchant)
		{
			if (merchant == null || !merchant.Spawned || merchant.Dead || merchant.Downed) return false;
			if (merchant.timetable == null || merchant.timetable.CurrentAssignment != TimeAssignmentDefOf.Work) return false;
			if (merchant.workSettings == null || basicWorker == null) return true;
			return merchant.workSettings.WorkIsActive(basicWorker);
		}

		/// <summary>True when this cashier has a merchant who can take customers right now.</summary>
		public static bool CanServe(Thing cashier)
		{
			Pawn merchant = MerchantOf(cashier);
			return merchant != null && OnDuty(merchant);
		}

		public static List<Pawn> QueueOf(Thing cashier)
		{
			if (!IsCashier(cashier)) return null;
			try
			{
				return queueField.GetValue(cashier) as List<Pawn>;
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] could not read the Kiiro cashier queue: " + e.Message, ErrorKey);
				return null;
			}
		}

		/// <summary>Appends the pawn to the cashier's own queue so Kiiro serves them.</summary>
		public static bool Enqueue(Thing cashier, Pawn customer)
		{
			if (customer == null || !CanServe(cashier)) return false;
			List<Pawn> queue = QueueOf(cashier);
			if (queue == null || queue.Contains(customer)) return false;
			queue.Add(customer);
			return true;
		}

		/// <summary>Kiiro's interaction cell where the customer stands (offsets index 1).</summary>
		public static IntVec3 CustomerCell(Thing cashier)
		{
			return IsCashier(cashier) ? CellAt(cashier, 1) : IntVec3.Invalid;
		}

		private static IntVec3 CellAt(Thing cashier, int index)
		{
			List<IntVec3> cells = cashier.InteractionCells;
			if (cells == null || cells.Count == 0) return cashier.Spawned ? cashier.Position : IntVec3.Invalid;
			if (index >= cells.Count) index = cells.Count - 1;
			return cells[index];
		}

		/// <summary>Every cashier on the map that could take a customer right now.</summary>
		public static void CollectReady(Map map, List<Thing> into)
		{
			into.Clear();
			if (map == null || !Available) return;
			List<Thing> things = map.listerThings.ThingsOfDef(cashierDef);
			for (int i = 0; i < things.Count; i++)
			{
				if (!things[i].Spawned) continue;
				if (!CanServe(things[i])) continue;
				into.Add(things[i]);
			}
		}

		public static bool AnyReady(Map map)
		{
			if (map == null || !Available) return false;
			List<Thing> things = map.listerThings.ThingsOfDef(cashierDef);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i].Spawned && CanServe(things[i])) return true;
			}
			return false;
		}
	}
}
