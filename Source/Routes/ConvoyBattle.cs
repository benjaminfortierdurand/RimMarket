using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMarket.Routes
{
	public class ConvoyBattle : MapParent
	{
		private const int CheckInterval = 120;

		public TradeConvoy convoy;
		public Pawn trader;
		public List<Pawn> convoyPawns = new List<Pawn>();
		public IntVec3 campCenter = IntVec3.Invalid;
		public IntVec3 enemyEntry = IntVec3.Invalid;
		public bool resolved;
		public bool won;

		public override bool CanReformFoggedEnemies => true;

		public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
		{
			alsoRemoveWorldObject = false;
			if (Map.mapPawns.AnyPawnBlockingMapRemoval) return false;
			if (TransporterUtility.IncomingTransporterPreventingMapRemoval(Map)) return false;
			alsoRemoveWorldObject = true;
			return true;
		}

		protected override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if (HasMap && !resolved && this.IsHashIntervalTick(CheckInterval, delta)) ConvoyBattleUtility.Check(this);
		}

		public void Notify_PawnLeft(Pawn p)
		{
			if (convoy != null && !convoy.Destroyed && convoyPawns.Contains(p)) convoy.AddSurvivor(p);
		}

		public override void Notify_MyMapAboutToBeRemoved()
		{
			base.Notify_MyMapAboutToBeRemoved();
			ConvoyBattleUtility.Notify_MapRemoved(this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				convoyPawns.RemoveAll(p => p == null || p.Discarded);
				if (trader != null && trader.Discarded) trader = null;
				if (convoy != null && convoy.Destroyed) convoy = null;
			}
			Scribe_References.Look(ref convoy, "convoy");
			Scribe_References.Look(ref trader, "trader");
			Scribe_Collections.Look(ref convoyPawns, "convoyPawns", LookMode.Reference);
			Scribe_Values.Look(ref campCenter, "campCenter", IntVec3.Invalid);
			Scribe_Values.Look(ref enemyEntry, "enemyEntry", IntVec3.Invalid);
			Scribe_Values.Look(ref resolved, "resolved");
			Scribe_Values.Look(ref won, "won");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (convoyPawns == null) convoyPawns = new List<Pawn>();
				convoyPawns.RemoveAll(p => p == null);
			}
		}
	}
}
