using System.Collections.Generic;
using RimMarket.Market;
using RimMarket.UI;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Away
{
	public class WorldObjectCompProperties_MarketTown : WorldObjectCompProperties
	{
		public WorldObjectCompProperties_MarketTown()
		{
			compClass = typeof(WorldObjectComp_MarketTown);
		}
	}

	public class WorldObjectComp_MarketTown : WorldObjectComp
	{
		private Settlement Settlement => parent as Settlement;

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
		{
			Settlement s = Settlement;
			if (s == null || !RimMarketMod.Settings.awayStalls || AwayStallUtility.HostileOrPlayer(s)) yield break;
			foreach (FloatMenuOption o in CaravanArrivalAction_HoldStall.GetFloatMenuOptions(caravan, s))
				yield return o;
		}

		public override string CompInspectStringExtra()
		{
			MarketManager mgr = MarketManager.Instance;
			Settlement s = Settlement;
			if (mgr == null || s == null) return null;
			AwayStall st = mgr.AwayStallAt(s);
			if (st != null) return "RimMarket.AwayTownHolding".Translate(st.caravan?.Label ?? "?");
			int wait = mgr.AwayCooldownLeft(s);
			if (wait > 0) return "RimMarket.AwayTownCooldown".Translate(wait.ToStringTicksToPeriod());
			return null;
		}
	}

	public class WorldObjectCompProperties_CaravanStall : WorldObjectCompProperties
	{
		public WorldObjectCompProperties_CaravanStall()
		{
			compClass = typeof(WorldObjectComp_CaravanStall);
		}
	}

	public class WorldObjectComp_CaravanStall : WorldObjectComp
	{
		private AwayStall Stall => MarketManager.Instance?.AwayStallFor(parent as Caravan);

		public override string CompInspectStringExtra()
		{
			AwayStall st = Stall;
			if (st == null) return null;
			string line = "RimMarket.AwayCaravanInspect".Translate(st.settlement?.LabelCap ?? "?", st.TicksLeft.ToStringTicksToPeriod(),
				st.itemsSold, UnityEngine.Mathf.RoundToInt(st.gross));
			if (st.EventActive)
				line += "\n" + (st.eventKind == AwayStall.EventFestival ? "RimMarket.AwayInspectFestival" : "RimMarket.AwayInspectRival").Translate();
			return line;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			AwayStall st = Stall;
			if (st == null) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.AwayPackUpLabel".Translate(),
				defaultDesc = "RimMarket.AwayPackUpDesc".Translate(),
				icon = RimMarketIcons.CloseMarket,
				action = () => MarketManager.Instance?.EndAwayStall(st, AwayStall.EndPackedUp),
			};
		}
	}
}
