using System;
using System.Reflection;
using HarmonyLib;
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
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Hospitality bridge unavailable: " + e.Message);
				isGuest = null;
				isArrived = null;
			}
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
