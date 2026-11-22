using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMarket.Compat
{
	public static class HospitalityBridge
	{
		public const string PackageId = "Orion.Hospitality";

		private const int ErrorKey = 0x524D4B02;

		private static bool activeResolved;
		private static bool active;
		private static bool resolved;
		private static Func<Pawn, bool> isGuest;
		private static Func<Pawn, bool> isArrived;
		private static Type guestBedType;
		private static MethodInfo rentalFee;
		private const int FeeErrorKey = 0x524D4B03;
		private const int FeeCacheTicks = 250;
		private static Map feeMap;
		private static int feeTick = -99999;
		private static int feeValue;

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
				return isGuest != null && isArrived != null;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			if (!Active) return;
			try
			{
				Type utility = AccessTools.TypeByName("Hospitality.Utilities.GuestUtility");
				if (utility == null) return;
				MethodInfo guest = utility.GetMethod("IsGuest", BindingFlags.Public | BindingFlags.Static,
					null, new[] { typeof(Pawn) }, null);
				MethodInfo arrived = utility.GetMethod("IsArrived", BindingFlags.Public | BindingFlags.Static,
					null, new[] { typeof(Pawn) }, null);
				if (guest == null || arrived == null) return;
				if (guest.ReturnType != typeof(bool) || arrived.ReturnType != typeof(bool)) return;
				isGuest = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), guest);
				isArrived = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), arrived);
				Type bed = AccessTools.TypeByName("Hospitality.Building_GuestBed");
				MethodInfo fee = bed?.GetMethod("GetRentalFee", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (fee != null && fee.ReturnType == typeof(int))
				{
					guestBedType = bed;
					rentalFee = fee;
				}
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Hospitality bridge unavailable: " + e.Message);
				isGuest = null;
				isArrived = null;
			}
		}

		public static int BedReserve(Pawn p, int silver)
		{
			if (p?.Map == null || silver <= 0 || !IsArrivedGuest(p) || rentalFee == null) return 0;
			if (p.ownership?.OwnedBed != null) return 0;
			int fee = CheapestFreeBed(p.Map);
			return fee > silver ? 0 : fee;
		}

		private static int CheapestFreeBed(Map map)
		{
			int now = Find.TickManager.TicksGame;
			if (map == feeMap && now >= feeTick && now - feeTick < FeeCacheTicks) return feeValue;
			feeMap = map;
			feeTick = now;
			feeValue = 0;
			try
			{
				int best = int.MaxValue;
				List<Building> all = map.listerBuildings.allBuildingsColonist;
				for (int i = 0; i < all.Count; i++)
				{
					if (!(all[i] is Building_Bed bed) || !guestBedType.IsInstanceOfType(bed) || !bed.AnyUnownedSleepingSlot) continue;
					int fee = (int)rentalFee.Invoke(bed, null);
					if (fee < best) best = fee;
				}
				if (best != int.MaxValue && best > 0) feeValue = best;
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hospitality bed fee check failed: " + e.Message, FeeErrorKey);
				rentalFee = null;
			}
			return feeValue;
		}

		public static bool IsArrivedGuest(Pawn p)
		{
			if (p == null || !Available) return false;
			try
			{
				return isGuest(p) && isArrived(p);
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hospitality guest check failed: " + e.Message, ErrorKey);
				return false;
			}
		}
	}
}
