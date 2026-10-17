using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMarket.Routes
{
	public class TradeConvoy : WorldObject
	{
		private const int RetryTicks = 2500;

		public int routeId;
		public int orderId;
		private List<int> path = new List<int>();
		private int index;
		private float costLeft;
		private float costTotal;
		public int waitingSince = -1;
		public bool escort;
		public Faction attacker;
		public int ambushUntil = -1;
		public float plunder;
		private bool ambushedThisTrip;
		public ConvoyBattle battle;
		private List<Pawn> survivors = new List<Pawn>();
		private bool pinPending;
		private Material cachedMat;
		private Material attackedMat;

		private const string AttackedTexture = "World/WorldObjects/RimMarket_TradeConvoyAttacked";

		public bool Waiting => waitingSince >= 0;

		public bool UnderAttack => ambushUntil >= 0;

		public void BeginAmbush(Faction by, int until)
		{
			attacker = by;
			ambushUntil = until;
			ambushedThisTrip = true;
		}

		public void EndAmbush(float plundered)
		{
			ambushUntil = -1;
			plunder = Mathf.Max(plunder, plundered);
		}

		public bool AtEnd => index >= path.Count - 1;

		public bool HasSurvivors => survivors.Count > 0;

		public List<Pawn> Survivors => survivors;

		public void AddSurvivor(Pawn p)
		{
			if (p == null || survivors.Contains(p)) return;
			survivors.Add(p);
			pinPending = true;
		}

		public void ClearSurvivors()
		{
			Unpin();
			survivors.Clear();
		}

		private void Pin()
		{
			for (int i = 0; i < survivors.Count; i++)
			{
				if (survivors[i] != null && Find.WorldPawns.Contains(survivors[i])) Find.WorldPawns.ForcefullyKeptPawns.Add(survivors[i]);
			}
		}

		private void Unpin()
		{
			for (int i = 0; i < survivors.Count; i++)
			{
				if (survivors[i] != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(survivors[i]);
			}
		}

		public override void PostRemove()
		{
			base.PostRemove();
			ClearSurvivors();
		}

		public void Setup(int routeId, List<int> nodes)
		{
			this.routeId = routeId;
			path = new List<int>(nodes);
			index = 0;
			waitingSince = -1;
			NextCost();
		}

		private PlanetTile At(int i) => new PlanetTile(path[i], Tile.Layer);

		private void NextCost()
		{
			if (AtEnd)
			{
				costLeft = costTotal = 0f;
				return;
			}
			costTotal = costLeft = Caravan_PathFollower.CostToMove(CaravanTicksPerMoveUtility.DefaultTicksPerMove, At(index), At(index + 1));
		}

		public bool StepForward()
		{
			if (index + 2 >= path.Count) return false;
			index++;
			Tile = At(index);
			NextCost();
			return true;
		}

		public void JumpToEnd()
		{
			if (path.Count == 0) return;
			index = path.Count - 1;
			Tile = At(index);
			costLeft = costTotal = 0f;
		}

		public int TicksLeft
		{
			get
			{
				if (Waiting || AtEnd) return 0;
				float total = costLeft;
				for (int i = index + 1; i < path.Count - 1; i++)
					total += Caravan_PathFollower.CostToMove(CaravanTicksPerMoveUtility.DefaultTicksPerMove, At(i), At(i + 1));
				return Mathf.RoundToInt(total);
			}
		}

		protected override void Tick()
		{
			base.Tick();
			if (pinPending)
			{
				pinPending = false;
				Pin();
			}
			int now = Find.TickManager.TicksGame;
			if (UnderAttack)
			{
				if (battle == null && now >= ambushUntil) TradeRouteUtility.ResolveAmbush(this);
				return;
			}
			if (Waiting)
			{
				if ((now - waitingSince) % RetryTicks == 0) TradeRouteUtility.TryArrive(this);
				return;
			}
			if (AtEnd)
			{
				waitingSince = now;
				TradeRouteUtility.TryArrive(this);
				return;
			}
			costLeft -= 1f;
			if (costLeft > 0f) return;
			index++;
			Tile = At(index);
			if (AtEnd)
			{
				waitingSince = now;
				TradeRouteUtility.TryArrive(this);
				return;
			}
			NextCost();
			if (!ambushedThisTrip && TradeRouteUtility.RollAmbush(this, Tile, out Faction by)) TradeRouteUtility.StartAmbush(this, by);
		}

		public override Vector3 DrawPos
		{
			get
			{
				PlanetTile cur = Tile;
				Vector3 a = Find.WorldGrid.GetTileCenter(cur);
				if (Waiting || UnderAttack || AtEnd || costTotal <= 0f) return cur.Layer.Origin + a;
				Vector3 b = Find.WorldGrid.GetTileCenter(At(index + 1));
				float t = 1f - Mathf.Clamp01(costLeft / costTotal);
				return cur.Layer.Origin + Vector3.Lerp(a, b, t);
			}
		}

		public override Material Material
		{
			get
			{
				if (UnderAttack)
				{
					if (attackedMat == null) attackedMat = MaterialPool.MatFrom(AttackedTexture, ShaderDatabase.WorldOverlayTransparentLit, Color.white, 3600);
					return attackedMat;
				}
				if (cachedMat == null) cachedMat = MaterialPool.MatFrom(def.texture, ShaderDatabase.WorldOverlayTransparentLit, Color.white, 3600);
				return cachedMat;
			}
		}

		public override Color ExpandingIconColor => Color.white;

		public override Texture2D ExpandingIcon => UnderAttack && UI.RimMarketIcons.ConvoyAttacked != null ? UI.RimMarketIcons.ConvoyAttacked : base.ExpandingIcon;

		public override string GetInspectString()
		{
			string text = base.GetInspectString();
			TradeRoute route = TradeRouteUtility.RouteOf(this);
			string home = TradeRouteUtility.HomeOf(this)?.LabelCap ?? "?";
			string line;
			if (UnderAttack && battle != null)
				line = "RimMarket.ConvoyUnderAttackHelped".Translate(attacker?.Name ?? "?").ToString();
			else if (UnderAttack)
				line = "RimMarket.ConvoyUnderAttack".Translate(attacker?.Name ?? "?",
					Mathf.Max(0, ambushUntil - Find.TickManager.TicksGame).ToStringTicksToPeriod()).ToString();
			else if (Waiting) line = "RimMarket.ConvoyWaiting".Translate(home).ToString();
			else line = "RimMarket.ConvoyBound".Translate(home, TicksLeft.ToStringTicksToPeriod()).ToString();
			if (route?.kind != null) line += "\n" + "RimMarket.ConvoyCarrying".Translate(route.kind.label);
			Orders.SpecialOrder order = Orders.SpecialOrderUtility.OrderOf(this);
			if (order != null) line += "\n" + "RimMarket.ConvoyCarryingOrder".Translate(Orders.SpecialOrderUtility.What(order));
			if (escort) line += "\n" + "RimMarket.ConvoyEscorted".Translate();
			if (plunder > 0f) line += "\n" + "RimMarket.ConvoyPlunderedLine".Translate(plunder.ToStringPercent());
			return text.NullOrEmpty() ? line : text + "\n" + line;
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
		{
			foreach (FloatMenuOption o in base.GetFloatMenuOptions(caravan)) yield return o;
			if (!UnderAttack) yield break;
			foreach (FloatMenuOption o in CaravanArrivalActionUtility.GetFloatMenuOptions(() => ConvoyBattleUtility.CanHelp(this),
				() => new CaravanArrivalAction_HelpConvoy(this), "RimMarket.HelpConvoy".Translate(), caravan, Tile, this))
				yield return o;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref routeId, "routeId");
			Scribe_Values.Look(ref orderId, "orderId");
			Scribe_Collections.Look(ref path, "path", LookMode.Value);
			Scribe_Values.Look(ref index, "index");
			Scribe_Values.Look(ref costLeft, "costLeft");
			Scribe_Values.Look(ref costTotal, "costTotal");
			Scribe_Values.Look(ref waitingSince, "waitingSince", -1);
			Scribe_Values.Look(ref escort, "escort");
			Scribe_References.Look(ref attacker, "attacker");
			Scribe_Values.Look(ref ambushUntil, "ambushUntil", -1);
			Scribe_Values.Look(ref plunder, "plunder");
			Scribe_Values.Look(ref ambushedThisTrip, "ambushedThisTrip");
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				survivors.RemoveAll(p => p == null || p.Discarded);
				if (battle != null && battle.Destroyed) battle = null;
			}
			Scribe_References.Look(ref battle, "battle");
			Scribe_Collections.Look(ref survivors, "survivors", LookMode.Reference);
			Scribe_Values.Look(ref pinPending, "pinPending");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (path == null) path = new List<int>();
				if (survivors == null) survivors = new List<Pawn>();
				survivors.RemoveAll(p => p == null);
			}
		}
	}
}
