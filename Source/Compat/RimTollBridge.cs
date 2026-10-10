using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMarket.Compat
{
	public static class RimTollBridge
	{
		private static bool resolved;
		private static PropertyInfo instanceProp;
		private static MethodInfo resentmentFor;
		private static MethodInfo memoryFor;
		private static FieldInfo lastTollTick;
		private static FieldInfo suppressPatch;

		public static bool Available
		{
			get
			{
				Resolve();
				return instanceProp != null && resentmentFor != null && memoryFor != null && lastTollTick != null;
			}
		}

		private static void Resolve()
		{
			if (resolved) return;
			resolved = true;
			try
			{
				Type state = AccessTools.TypeByName("RimToll.GameComponent_TollState");
				Type memory = AccessTools.TypeByName("RimToll.FactionTollMemory");
				if (state == null || memory == null) return;
				instanceProp = state.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
				resentmentFor = state.GetMethod("ResentmentFor", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { typeof(Faction) }, null);
				memoryFor = state.GetMethod("MemoryFor", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { typeof(Faction), typeof(bool) }, null);
				lastTollTick = memory.GetField("lastTollTick", BindingFlags.Public | BindingFlags.Instance);
				suppressPatch = state.GetField("SuppressPatch", BindingFlags.Public | BindingFlags.Static);
				if (suppressPatch != null && suppressPatch.FieldType != typeof(bool)) suppressPatch = null;
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] RimToll bridge unavailable: " + e.Message);
				instanceProp = null;
				resentmentFor = null;
				memoryFor = null;
				lastTollTick = null;
			}
		}

		public static bool WithoutBooth(Func<bool> arrive)
		{
			Resolve();
			if (suppressPatch == null) return arrive();
			bool before;
			try
			{
				before = (bool)suppressPatch.GetValue(null);
				suppressPatch.SetValue(null, true);
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] RimToll booth pass failed: " + e.Message);
				suppressPatch = null;
				return arrive();
			}
			try
			{
				return arrive();
			}
			finally
			{
				try { suppressPatch?.SetValue(null, before); }
				catch { }
			}
		}

		private static object Instance()
		{
			try { return instanceProp?.GetValue(null, null); }
			catch { return null; }
		}

		public static float Resentment(Faction faction)
		{
			if (faction == null || !Available) return 0f;
			try
			{
				object inst = Instance();
				if (inst == null) return 0f;
				return (float)resentmentFor.Invoke(inst, new object[] { faction });
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] RimToll resentment read failed: " + e.Message);
				return 0f;
			}
		}

		public static int LastTollTick(Faction faction)
		{
			if (faction == null || !Available) return -1;
			try
			{
				object inst = Instance();
				if (inst == null) return -1;
				object memory = memoryFor.Invoke(inst, new object[] { faction, false });
				if (memory == null) return -1;
				return (int)lastTollTick.GetValue(memory);
			}
			catch (Exception e)
			{
				Log.Warning("[RimMarket] RimToll memory read failed: " + e.Message);
				return -1;
			}
		}
	}
}
