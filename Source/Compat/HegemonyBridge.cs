using System;
using System.Collections;
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
		private static PropertyInfo componentCurrent;
		private static PropertyInfo statesProp;
		private static FieldInfo stateSettlement;
		private static PropertyInfo territoryProp;
		private static MethodInfo tryGetClaim;
		private static PropertyInfo claimOwner;
		private static PropertyInfo claimRunnerUp;
		private static PropertyInfo claimContested;
		private static MethodInfo priceMethod;
		private static MethodInfo creditMethod;
		private static PropertyInfo ledgerCurrent;
		private static MethodInfo recordTrade;

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
				Type component = AccessTools.TypeByName("FactionTerritories2.Component");
				Type state = AccessTools.TypeByName("FactionTerritories2.SettlementState");
				if (component == null || state == null) return;
				componentCurrent = AccessTools.Property(component, "Current");
				statesProp = AccessTools.Property(component, "States");
				stateSettlement = AccessTools.Field(state, "settlement");
				territoryProp = AccessTools.Property(component, "Territory");
				Type snapshot = territoryProp?.PropertyType;
				tryGetClaim = snapshot == null ? null : AccessTools.Method(snapshot, "TryGet");
				Type claim = tryGetClaim?.GetParameters()[1].ParameterType.GetElementType();
				if (claim != null)
				{
					claimOwner = AccessTools.Property(claim, "owner");
					claimRunnerUp = AccessTools.Property(claim, "runnerUp");
					claimContested = AccessTools.Property(claim, "contested");
				}
				Type economy = AccessTools.TypeByName("FactionTerritories2.LivingEconomy");
				priceMethod = economy == null ? null : AccessTools.Method(economy, "Price", new[] { state, typeof(ThingDef) });
				Type donations = AccessTools.TypeByName("FactionTerritories2.SupplyDonations");
				creditMethod = donations == null ? null : AccessTools.Method(donations, "Credit", new[] { state, typeof(Thing) });
				Type ledger = AccessTools.TypeByName("FactionTerritories2.DiplomacyLedger");
				ledgerCurrent = ledger == null ? null : AccessTools.Property(ledger, "Current");
				recordTrade = ledger == null ? null : AccessTools.Method(ledger, "RecordTrade", new[] { typeof(Faction), typeof(Faction), typeof(double) });
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Hegemony found but its economy could not be read: " + e.Message);
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
			if (settlement == null || componentCurrent == null || statesProp == null || stateSettlement == null) return null;
			object comp = componentCurrent.GetValue(null);
			if (!(comp != null && statesProp.GetValue(comp) is IEnumerable states)) return null;
			foreach (object s in states)
			{
				if (s != null && stateSettlement.GetValue(s) == settlement) return s;
			}
			return null;
		}

		public static float ScarcityFactor(Settlement settlement, ThingDef def)
		{
			if (!Active || def == null || def.BaseMarketValue <= 0f) return 1f;
			Resolve();
			if (priceMethod == null) return 1f;
			try
			{
				object state = StateOf(settlement);
				if (state == null) return 1f;
				double local = (double)priceMethod.Invoke(null, new[] { state, def });
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
			if (creditMethod == null) return false;
			try
			{
				object state = StateOf(settlement);
				if (state == null) return false;
				creditMethod.Invoke(null, new[] { state, thing });
				return thing.Destroyed;
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
			if (ledgerCurrent == null || recordTrade == null) return;
			try
			{
				object ledger = ledgerCurrent.GetValue(null);
				if (ledger != null) recordTrade.Invoke(ledger, new object[] { Faction.OfPlayer, other, (double)value });
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
