using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Compat
{
	public static class TerritoriesBridge
	{
		private const int ErrorKey = 0x524D4B21;

		private static bool resolved;
		private static MethodInfo claimants;
		private static bool takesInt;
		private static readonly List<int> ids = new List<int>();
		private static readonly object[] args = new object[2];

		public static bool Active
		{
			get
			{
				Resolve();
				return claimants != null;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			try
			{
				Type cache = AccessTools.TypeByName("FactionTerritories.TerritoryOwnershipCache");
				MethodInfo m = cache == null ? null : AccessTools.Method(cache, "TryGetClaimingFactions");
				ParameterInfo[] ps = m?.GetParameters();
				if (ps == null || ps.Length != 2 || ps[1].ParameterType != typeof(List<int>)) return;
				takesInt = ps[0].ParameterType == typeof(int);
				if (!takesInt && ps[0].ParameterType != typeof(PlanetTile)) return;
				claimants = m;
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] Faction Territories found but its territory lookup could not be read: " + e.Message);
				claimants = null;
			}
		}

		public static Faction HostileClaimant(PlanetTile tile, Faction traveller)
		{
			Resolve();
			if (claimants == null || traveller == null || !tile.Valid) return null;
			try
			{
				ids.Clear();
				args[0] = takesInt ? (object)tile.tileId : tile;
				args[1] = ids;
				if (!(bool)claimants.Invoke(null, args)) return null;
				List<Faction> all = Find.FactionManager.AllFactionsListForReading;
				for (int i = 0; i < ids.Count; i++)
				{
					for (int j = 0; j < all.Count; j++)
					{
						Faction f = all[j];
						if (f.loadID != ids[i]) continue;
						if (f != traveller && !f.IsPlayer && !f.defeated && f.HostileTo(traveller)) return f;
						break;
					}
				}
			}
			catch (Exception e)
			{
				Log.ErrorOnce("[RimMarket] Faction Territories lookup failed, trade routes ignore territories from now on: " + e, ErrorKey);
				claimants = null;
			}
			return null;
		}
	}
}
