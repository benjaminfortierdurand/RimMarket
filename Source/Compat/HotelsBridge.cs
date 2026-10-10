using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.Compat
{
	[StaticConstructorOnStartup]
	public static class HotelsBridge
	{
		public const string PackageId = "ROBTRG.HotelsAndRecreation";

		private const int ErrorKey = 0x524D4B07;
		private const string VisitStallsDef = "RimMarket_VisitStalls";
		private const string WatchAuctionDef = "RimMarket_WatchAuction";
		private const float WatchChance = 0.25f;
		private const float BidderWatchChance = 0.6f;

		private static bool activeResolved;
		private static bool active;
		private static bool resolved;
		private static Type stayType;
		private static Func<Building_Bed, bool> isGuestBed;
		private static PropertyInfo neutralGround;
		private static MethodInfo recordFor;
		private static PropertyInfo budgetLeft;
		private static FieldInfo spent;
		private static Func<Pawn, IntVec3, Map, bool> canAccess;

		static HotelsBridge()
		{
			if (Active) Hook();
		}

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
				return stayType != null && recordFor != null;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			if (!Active) return;
			try
			{
				stayType = AccessTools.TypeByName("RJWHotel.LordJob_HotelStay");
				Type beds = AccessTools.TypeByName("RJWHotel.GuestBedUtility");
				MethodInfo guestBed = beds?.GetMethod("IsGuestBed", BindingFlags.Public | BindingFlags.Static,
					null, new[] { typeof(Building_Bed) }, null);
				if (guestBed != null && guestBed.ReturnType == typeof(bool))
					isGuestBed = (Func<Building_Bed, bool>)Delegate.CreateDelegate(typeof(Func<Building_Bed, bool>), guestBed);
				neutralGround = AccessTools.TypeByName("RJWHotel.NeutralGround")?.GetProperty("Active", BindingFlags.Public | BindingFlags.Static);
				if (neutralGround != null && neutralGround.PropertyType != typeof(bool)) neutralGround = null;
				recordFor = stayType?.GetMethod("RecordFor", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Pawn) }, null);
				Type record = recordFor?.ReturnType;
				budgetLeft = record?.GetProperty("BudgetLeft", BindingFlags.Public | BindingFlags.Instance);
				spent = record?.GetField("spent", BindingFlags.Public | BindingFlags.Instance);
				if (budgetLeft == null || budgetLeft.PropertyType != typeof(float) || spent == null || spent.FieldType != typeof(float))
				{
					recordFor = null;
					budgetLeft = null;
					spent = null;
				}
				MethodInfo access = AccessTools.TypeByName("RJWHotel.GuestAccess")?.GetMethod("CanAccess", BindingFlags.Public | BindingFlags.Static,
					null, new[] { typeof(Pawn), typeof(IntVec3), typeof(Map) }, null);
				if (access != null && access.ReturnType == typeof(bool))
					canAccess = (Func<Pawn, IntVec3, Map, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, IntVec3, Map, bool>), access);
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Hotels and Recreation bridge unavailable: " + e.Message);
				stayType = null;
				isGuestBed = null;
				neutralGround = null;
				recordFor = null;
				budgetLeft = null;
				spent = null;
				canAccess = null;
			}
		}

		private static void Hook()
		{
			try
			{
				Type hooks = AccessTools.TypeByName("RJWHotel.HotelHooks");
				Type hotel = AccessTools.TypeByName("RJWHotel.MapComponent_Hotel");
				if (hooks == null || hotel == null) return;
				if (AccessTools.Field(hooks, "GuestActivities")?.GetValue(null) is HashSet<string> activities)
					activities.Add(VisitStallsDef);
				if (AccessTools.Field(hooks, "GuestGivers")?.GetValue(null) is IList givers)
				{
					Type func = typeof(Func<,,>).MakeGenericType(typeof(Pawn), hotel, typeof(Job));
					MethodInfo giver = AccessTools.Method(typeof(HotelsBridge), nameof(AuctionGiver)).MakeGenericMethod(hotel);
					givers.Add(Delegate.CreateDelegate(func, giver));
				}
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Could not hook into Hotels and Recreation: " + e.Message);
			}
		}

		private static Job AuctionGiver<T>(Pawn pawn, T hotel)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || pawn?.Map == null || !RimMarketMod.Settings.guestsShop || !mgr.AnyAuctionOn(pawn.Map)) return null;
			if (!Rand.Chance(mgr.IsBidder(pawn) ? BidderWatchChance : WatchChance)) return null;
			JoyGiverDef def = DefDatabase<JoyGiverDef>.GetNamedSilentFail(WatchAuctionDef);
			Job job = def?.Worker.TryGiveJob(pawn);
			if (job == null || !MayGo(pawn, job.targetA.Cell)) return null;
			return job;
		}

		public static bool MayGo(Pawn guest, IntVec3 cell)
		{
			if (!IsGuest(guest) || canAccess == null) return true;
			try
			{
				return canAccess(guest, cell, guest.Map);
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hotels and Recreation access check failed: " + e.Message, ErrorKey + 2);
				return true;
			}
		}

		public static bool IsGuest(Pawn p)
		{
			if (p == null || !Active) return false;
			Resolve();
			return stayType != null && stayType.IsInstanceOfType(p.GetLord()?.LordJob);
		}

		private static object RecordOf(Pawn p)
		{
			if (recordFor == null || !IsGuest(p)) return null;
			return recordFor.Invoke(p.GetLord().LordJob, new object[] { p });
		}

		public static int BudgetOf(Pawn p)
		{
			try
			{
				object rec = RecordOf(p);
				return rec == null ? 0 : Mathf.FloorToInt((float)budgetLeft.GetValue(rec));
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hotels and Recreation budget check failed: " + e.Message, ErrorKey + 3);
				return 0;
			}
		}

		public static int Spend(Pawn p, int amount)
		{
			if (amount <= 0) return 0;
			try
			{
				object rec = RecordOf(p);
				if (rec == null) return 0;
				int paid = Mathf.Min(amount, Mathf.FloorToInt((float)budgetLeft.GetValue(rec)));
				if (paid <= 0) return 0;
				spent.SetValue(rec, (float)spent.GetValue(rec) + paid);
				return paid;
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hotels and Recreation payment failed: " + e.Message, ErrorKey + 4);
				return 0;
			}
		}

		public static bool IsGuestBed(Building_Bed bed)
		{
			if (bed == null || !Active) return false;
			Resolve();
			if (isGuestBed == null) return false;
			try
			{
				return isGuestBed(bed);
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hotels and Recreation guest bed check failed: " + e.Message, ErrorKey);
				return false;
			}
		}

		public static bool PeacefulGuests(Lord lord)
		{
			if (lord == null || !Active) return false;
			Resolve();
			if (stayType == null || neutralGround == null || !stayType.IsInstanceOfType(lord.LordJob)) return false;
			try
			{
				return (bool)neutralGround.GetValue(null);
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Hotels and Recreation neutral ground check failed: " + e.Message, ErrorKey + 1);
				return false;
			}
		}
	}
}
