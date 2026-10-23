using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Compat
{
	public static class HegemonyBridge
	{
		public const string PackageId = "jaeger972.factionterritories2";

		private const int ErrorKey = 0x524D4B23;
		private const float MinScarcity = 0.5f;
		private const float MaxScarcity = 3f;

		private static bool activeResolved;
		private static bool active;
		private static bool resolved;
		private static bool failed;
		private static MethodInfo tryGetState;
		private static MethodInfo tryGetPrice;
		private static MethodInfo tryCredit;
		private static MethodInfo recordTrade;
		private static PropertyInfo componentCurrent;
		private static PropertyInfo territoryProp;
		private static MethodInfo tryGetClaim;
		private static PropertyInfo claimOwner;
		private static PropertyInfo claimRunnerUp;
		private static PropertyInfo claimContested;

		public static bool Active
		{
			get
			{
				if (!activeResolved)
				{
					activeResolved = true;
					active = ModsConfig.IsActive(PackageId);
				}
				return active && !failed;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			try
			{
				Type api = AccessTools.TypeByName("FactionTerritories2.HegemonyApi");
				Type state = AccessTools.TypeByName("FactionTerritories2.SettlementState");
				if (api != null && state != null)
				{
					tryGetState = AccessTools.Method(api, "TryGetSettlementState", new[] { typeof(Settlement), state.MakeByRefType() });
					tryGetPrice = AccessTools.Method(api, "TryGetPrice", new[] { state, typeof(ThingDef), typeof(double).MakeByRefType() });
					tryCredit = AccessTools.Method(api, "TryCredit", new[] { state, typeof(Thing), typeof(string).MakeByRefType() });
					recordTrade = AccessTools.Method(api, "RecordTrade", new[] { typeof(Faction), typeof(Faction), typeof(double) });
				}
				else Log.Warning("[RimMarket] Hegemony found without its public API, update Hegemony to beta 0.95 or later for prices, stock and trade goodwill.");
				Type component = AccessTools.TypeByName("FactionTerritories2.Component");
				componentCurrent = component == null ? null : AccessTools.Property(component, "Current");
				territoryProp = component == null ? null : AccessTools.Property(component, "Territory");
				Type snapshot = territoryProp?.PropertyType;
				tryGetClaim = snapshot == null ? null : AccessTools.Method(snapshot, "TryGet");
				Type claim = tryGetClaim?.GetParameters()[1].ParameterType.GetElementType();
				if (claim != null)
				{
					claimOwner = AccessTools.Property(claim, "owner");
					claimRunnerUp = AccessTools.Property(claim, "runnerUp");
					claimContested = AccessTools.Property(claim, "contested");
				}
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Hegemony found but its API could not be read: " + e.Message);
				failed = true;
			}
		}

		private static void Fail(string what, Exception e)
		{
			failed = true;
			Log.ErrorOnce("[RimMarket] Hegemony " + what + " failed, the Hegemony bridge is now off: " + e, ErrorKey);
		}

		private static object StateOf(Settlement settlement)
		{
			if (settlement == null || tryGetState == null) return null;
			object[] args = { settlement, null };
			return (bool)tryGetState.Invoke(null, args) ? args[1] : null;
		}

		public static float ScarcityFactor(Settlement settlement, ThingDef def)
		{
			if (!Active || def == null || def.BaseMarketValue <= 0f) return 1f;
			Resolve();
			if (tryGetPrice == null) return 1f;
			try
			{
				object state = StateOf(settlement);
				if (state == null) return 1f;
				object[] args = { state, def, 0d };
				if (!(bool)tryGetPrice.Invoke(null, args)) return 1f;
				double local = (double)args[2];
				if (double.IsNaN(local) || double.IsInfinity(local) || local <= 0d) return 1f;
				return Mathf.Clamp((float)(local / def.BaseMarketValue), MinScarcity, MaxScarcity);
			}
			catch (Exception e)
			{
				Fail("price lookup", e);
				return 1f;
			}
		}

		public static bool Credit(Settlement settlement, Thing thing)
		{
			if (!Active || thing == null || thing.Destroyed) return false;
			Resolve();
			if (tryCredit == null) return false;
			try
			{
				object state = StateOf(settlement);
				if (state == null) return false;
				object[] args = { state, thing, null };
				return (bool)tryCredit.Invoke(null, args) && thing.Destroyed;
			}
			catch (Exception e)
			{
				Fail("stock credit", e);
				return false;
			}
		}

		public static void RecordTrade(Faction other, float value)
		{
			if (!Active || other == null || other.IsPlayer || value <= 0f) return;
			Resolve();
			if (recordTrade == null) return;
			try
			{
				recordTrade.Invoke(null, new object[] { Faction.OfPlayer, other, (double)value });
			}
			catch (Exception e)
			{
				Fail("trade goodwill", e);
			}
		}

		public static Faction HostileClaimant(PlanetTile tile, Faction traveller)
		{
			if (!Active || traveller == null || !tile.Valid) return null;
			Resolve();
			if (componentCurrent == null || territoryProp == null || tryGetClaim == null || claimOwner == null) return null;
			try
			{
				object comp = componentCurrent.GetValue(null);
				object territory = comp == null ? null : territoryProp.GetValue(comp);
				if (territory == null) return null;
				object[] args = { tile, null };
				if (!(bool)tryGetClaim.Invoke(territory, args) || args[1] == null) return null;
				Faction owner = claimOwner.GetValue(args[1]) as Faction;
				if (HostileTo(owner, traveller)) return owner;
				if (claimContested != null && claimRunnerUp != null && (bool)claimContested.GetValue(args[1]))
				{
					Faction runnerUp = claimRunnerUp.GetValue(args[1]) as Faction;
					if (HostileTo(runnerUp, traveller)) return runnerUp;
				}
			}
			catch (Exception e)
			{
				Fail("territory lookup", e);
			}
			return null;
		}

		private static bool HostileTo(Faction f, Faction traveller)
		{
			return f != null && f != traveller && !f.IsPlayer && !f.defeated && f.HostileTo(traveller);
		}
	}
}
