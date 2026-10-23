using System.Collections.Generic;
using RimMarket.AI;
using RimMarket.Market;
using RimMarket.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class CompProperties_MarketStall : CompProperties
	{
		public bool drawWares = true;
		public Vector3 waresOffset = Vector3.zero;
		public float waresScale = 0.62f;
		public float waresSpacing = 1f;
		public int verticalWares = 3;
		public Vector3 verticalWaresOffset = Vector3.zero;
		public float verticalWaresScale = 0.62f;
		public string awningTexPath;
		public string depthTexPath;
		public bool merchantAtSide;
		public int customerReachVertical = 1;
		public FloatRange canvasX = FloatRange.Zero;
		public FloatRange canvasZ = FloatRange.Zero;
		public FloatRange canvasXVertical = FloatRange.Zero;
		public FloatRange canvasZVertical = FloatRange.Zero;
		public Vector3 buntingAnchor = new Vector3(0f, 0f, 1.9f);
		public Vector3 buntingAnchorVertical = new Vector3(0f, 0f, 2f);
		public bool stringLights;
		public bool paintableRoof;
		public bool frontBunting;
		public Vector3 frontLeft = new Vector3(-1.62f, 0f, 0.02f);
		public Vector3 frontRight = new Vector3(1.62f, 0f, 0.02f);
		public Vector3 frontLeftVertical = new Vector3(0.95f, 0f, 1.55f);
		public Vector3 frontRightVertical = new Vector3(0.95f, 0f, -1.2f);
		public Vector3 bannerOffset = new Vector3(1.58f, 0f, -0.2f);
		public Vector3 bannerOffsetVertical = new Vector3(0.9f, 0f, -1.2f);

		public CompProperties_MarketStall()
		{
			compClass = typeof(CompMarketStall);
		}
	}

	public class CompMarketStall : ThingComp
	{
		private const int DisplayCount = 3;
		private const int DisplayRefreshTicks = 2500;
		private const int ReachCheckTicks = 120;

		private int reachCheckTick = -99999;
		private bool reachesEdge;

		public bool flipped;

		private readonly List<Thing> display = new List<Thing>();
		private bool displayDirty = true;
		private int lastDisplayTick = -99999;

		public CompProperties_MarketStall Props => (CompProperties_MarketStall)props;

		public bool Modern => Props.stringLights || parent.Stuff?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic) == true;

		public IntVec3 Position => parent.Position;

		public Map Map => parent.Map;

		public bool Spawned => parent.Spawned;

		public bool Destroyed => parent.Destroyed;

		public Vector3 DrawPos => parent.DrawPos;

		public Faction Faction => parent.Faction;

		public IntVec3 Back => BackFor(parent.Rotation);

		public static IntVec3 BackFor(Rot4 rot) => rot.IsHorizontal ? rot.Opposite.FacingCell : IntVec3.North;

		public static IntVec3 SideFor(Rot4 rot) => rot.IsHorizontal ? IntVec3.South : IntVec3.East;

		public static IntVec3 MerchantCellFor(ThingDef def, IntVec3 pos, Rot4 rot) => MerchantCellFor(def, pos, rot, false);

		public static IntVec3 MerchantCellFor(ThingDef def, IntVec3 pos, Rot4 rot, bool flipped)
		{
			CompProperties_MarketStall p = def?.GetCompProperties<CompProperties_MarketStall>();
			if (p != null && p.merchantAtSide) return pos + SideFor(rot) * ((flipped ? -1 : 1) * (def.size.x / 2 + 1));
			return pos + BackFor(rot);
		}

		public static bool FlippedOf(Thing t)
		{
			Thing inner = t is MinifiedThing m ? m.InnerThing : t;
			return MarketStallDefs.StallOf(inner)?.flipped == true;
		}

		public IntVec3 Side => SideFor(parent.Rotation);

		public IntVec3 MerchantSide => Side * (flipped ? -1 : 1);

		public IntVec3 MerchantCell => MerchantCellFor(parent.def, Position, parent.Rotation, flipped);

		public static IntVec3 CustomerCellFor(ThingDef def, IntVec3 pos, Rot4 rot)
		{
			IntVec3 back = BackFor(rot);
			return pos - back * CustomerReachFor(def, rot);
		}

		private static int CustomerReachFor(ThingDef def, Rot4 rot)
		{
			CompProperties_MarketStall p = def?.GetCompProperties<CompProperties_MarketStall>();
			return rot.IsHorizontal && p != null ? Mathf.Max(1, p.customerReachVertical) : 1;
		}

		public int CustomerReach => CustomerReachFor(parent.def, parent.Rotation);

		public IntVec3 CustomerCell => CustomerCellFor(parent.def, Position, parent.Rotation);

		public IntVec3 Front
		{
			get
			{
				IntVec3 back = Back;
				return new IntVec3(-back.x, 0, -back.z);
			}
		}

		public IntVec3 MerchantLook => MerchantCell + Front * 4;

		public bool CanvasCovers(IntVec3 c)
		{
			Rot4 rot = parent.Rotation;
			FloatRange xr = rot.IsHorizontal ? Props.canvasXVertical : Props.canvasX;
			FloatRange zr = rot.IsHorizontal ? Props.canvasZVertical : Props.canvasZ;
			if (xr.max <= xr.min || zr.max <= zr.min) return false;
			Vector3 rel = c.ToVector3Shifted() - parent.TrueCenter();
			float x = rot == Rot4.West ? -rel.x : rel.x;
			return x > xr.min && x < xr.max && rel.z > zr.min && rel.z < zr.max;
		}

		public StallVisit Visit => MarketManager.AnyVisits ? MarketManager.Instance.VisitFor(this) : null;

		public List<IntVec3> ParkCells()
		{
			IntVec3 m = MerchantCell;
			IntVec3 b = Back;
			IntVec3 s = Side;
			return new List<IntVec3>
			{
				m + s, m - s, m + b + s, m + b - s, m + b, m + b + b + s, m + b + b - s, m + b + b,
			};
		}

		public void Notify_VisitStarted()
		{
			displayDirty = true;
			parent.GetComp<CompMarketSign>()?.Refresh();
		}

		public void Notify_VisitEnded()
		{
			display.Clear();
			displayDirty = true;
			parent.GetComp<CompMarketSign>()?.Refresh();
		}

		public void MarkDisplayDirty()
		{
			displayDirty = true;
		}

		private void RefreshDisplay(StallVisit v)
		{
			Pawn_TraderTracker tracker = v.trader?.trader;
			if (tracker == null || !v.trader.Spawned)
			{
				display.Clear();
				displayDirty = false;
				lastDisplayTick = Find.TickManager.TicksGame;
				return;
			}
			RefreshDisplay(tracker.Goods);
		}

		private void RefreshDisplay(IEnumerable<Thing> goods)
		{
			display.Clear();
			displayDirty = false;
			lastDisplayTick = Find.TickManager.TicksGame;
			var candidates = new List<Thing>();
			foreach (Thing t in goods)
			{
				if (t == null || t is Pawn || t.Destroyed) continue;
				if (t.def.category != ThingCategory.Item || (t.def.graphicData == null && !(t is MinifiedThing))) continue;
				if (t.def == ThingDefOf.Silver) continue;
				candidates.Add(t);
			}
			candidates.SortByDescending(t => t.MarketValue * t.stackCount);
			for (int i = 0; i < candidates.Count && display.Count < DisplayCount; i++)
			{
				Thing t = candidates[i];
				bool dup = false;
				for (int j = 0; j < display.Count; j++)
				{
					if (ColonyStalls.StockDef(display[j]) == ColonyStalls.StockDef(t)) { dup = true; break; }
				}
				if (!dup) display.Add(t);
			}
		}

		public override void PostDraw()
		{
			Building_MarketStall painted = Props.paintableRoof ? parent as Building_MarketStall : null;
			if (painted != null) StallRoof.Draw(painted);
			else DrawOverlay(ref depth, Props.depthTexPath, AltitudeLayer.PawnState.AltitudeFor());
			if (MarketManager.AnyVisits)
			{
				Faction banner = Visit?.faction;
				if (banner != null && !banner.IsPlayer) StallBanner.Draw(this, banner);
			}
			if (!Props.drawWares) return;
			Building_MarketStall colony = parent as Building_MarketStall;
			bool colonyStall = colony != null && colony.ColonyStall;
			if (!colonyStall && !MarketManager.AnyVisits) return;
			bool vertical = parent.Rotation.IsHorizontal;
			int firstSlot = vertical ? DisplayCount - Mathf.Clamp(Props.verticalWares, 0, DisplayCount) : 0;
			if (firstSlot >= DisplayCount) return;
			bool refresh = displayDirty || DisplayStale() || Find.TickManager.TicksGame - lastDisplayTick > DisplayRefreshTicks;
			if (colonyStall)
			{
				if (refresh) RefreshDisplay(colony.Stock);
			}
			else
			{
				StallVisit v = Visit;
				if (v == null || v.trader == null) return;
				if (refresh) RefreshDisplay(v);
			}
			if (display.Count == 0) return;
			float alt = AltitudeLayer.BuildingOnTop.AltitudeFor();
			IntVec3 side = Side;
			Vector3 offset = vertical ? Props.verticalWaresOffset : Props.waresOffset;
			float scale = vertical ? Props.verticalWaresScale : Props.waresScale;
			float spacing = vertical ? 1f : Props.waresSpacing;
			var size = new Vector3(scale, 1f, scale);
			int ware = 0;
			for (int slot = firstSlot; slot < DisplayCount && ware < display.Count; slot++)
			{
				Material mat = NextWareMaterial(ref ware);
				if (mat == null) break;
				Vector3 pos = Position.ToVector3Shifted() + side.ToVector3() * ((slot - 1) * spacing) + offset;
				pos.y = alt;
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, size), mat, 0);
			}
			if (painted == null && (vertical || Props.depthTexPath.NullOrEmpty()))
				DrawOverlay(ref awning, Props.awningTexPath, AltitudeLayer.BuildingOnTop.AltitudeFor() + Altitudes.AltInc);
		}

		private Graphic awning;
		private Graphic depth;
		private Color overlayColor;
		private Color overlayColorTwo;

		private void DrawOverlay(ref Graphic cache, string path, float altitude)
		{
			if (path.NullOrEmpty()) return;
			GraphicData data = parent.def.graphicData;
			if (data == null) return;
			Color one = parent.DrawColor;
			Color two = parent.DrawColorTwo;
			if (one != overlayColor || two != overlayColorTwo)
			{
				overlayColor = one;
				overlayColorTwo = two;
				awning = null;
				depth = null;
			}
			if (cache == null) cache = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.CutoutComplex, data.drawSize, one, two);
			Rot4 rot = parent.Rotation;
			Vector3 pos = parent.TrueCenter() + data.DrawOffsetForRot(rot);
			pos.y = altitude;
			Graphics.DrawMesh(cache.MeshAt(rot), pos, Quaternion.identity, cache.MatAt(rot), 0);
		}

		private static bool OnDisplay(Thing t)
		{
			return t != null && !t.Destroyed && !t.Spawned && t.stackCount > 0 && t.ParentHolder != null;
		}

		private bool DisplayStale()
		{
			for (int i = 0; i < display.Count; i++)
			{
				if (!OnDisplay(display[i])) return true;
			}
			return false;
		}

		private Material NextWareMaterial(ref int index)
		{
			while (index < display.Count)
			{
				Thing t = display[index++];
				if (!OnDisplay(t)) continue;
				Material mat = t.Graphic?.MatSingleFor(t);
				if (mat != null) return mat;
			}
			return null;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref flipped, "merchantFlipped");
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
			MarketManager.Instance?.Notify_StallDespawned(this);
			display.Clear();
		}

		public override string CompInspectStringExtra()
		{
			if (parent is Building_MarketStall colony && colony.ColonyStall) return ColonyStalls.InspectText(colony);
			var lines = new List<string>();
			StallVisit v = Visit;
			if (v == null)
			{
				lines.Add("RimMarket.StallFree".Translate());
				if (Spawned)
				{
					Building_MarketMarker marker = MarketUtility.MarkerFor(this);
					if (marker == null) lines.Add("RimMarket.StallNoMarker".Translate());
					else if (!marker.Open) lines.Add("RimMarket.StallMarketClosed".Translate());
					if (!MerchantCell.InBounds(Map) || !MerchantCell.Standable(Map)) lines.Add("RimMarket.StallBlocked".Translate());
					else if (!MerchantReachesEdge()) lines.Add("RimMarket.StallUnreachable".Translate());
				}
				return string.Join("\n", lines);
			}
			lines.Add("RimMarket.StallTaken".Translate(v.trader?.LabelShortCap ?? "?", v.faction?.Name ?? "?",
				v.trader?.TraderKind?.label ?? "?"));
			lines.Add("RimMarket.StallTimeLeft".Translate((v.TicksLeft / 2500f).ToString("F1")));
			if (v.orderId > 0) lines.Add("RimMarket.StallOrderLine".Translate());
			if (v.feeMode == RimMarketSettings.FeeArrival)
			{
				if (v.feePaid) lines.Add("RimMarket.StallFeePaid".Translate(v.feeAmount));
				else if (v.feeShort) lines.Add("RimMarket.StallFeeUnpaid".Translate());
				else if (v.feeAmount > 0) lines.Add("RimMarket.StallFeeDue".Translate(v.feeAmount));
				else lines.Add("RimMarket.StallFeeWaived".Translate());
			}
			else if (v.feeMode == RimMarketSettings.FeeShare)
			{
				lines.Add("RimMarket.StallShare".Translate(v.feeCollected, Mathf.RoundToInt(v.shareOwed)));
			}
			if (RimMarketMod.Settings.haggleEnabled)
			{
				if (!v.haggleUsed) lines.Add("RimMarket.StallHaggleOpen".Translate());
				else lines.Add(v.haggleWon ? "RimMarket.StallHaggleWon".Translate() : "RimMarket.StallHaggleLost".Translate());
			}
			string watch = GuardWatch.InspectLine(v);
			if (watch != null) lines.Add(watch);
			if (v.silverEarned > 0) lines.Add("RimMarket.StallGuestSales".Translate(v.silverEarned));
			if (v.repPriceBonus > 0f)
				lines.Add("RimMarket.StallRepBetter".Translate((v.repPriceBonus * 100f).ToString("F0")));
			else if (v.repPriceBonus < 0f)
				lines.Add("RimMarket.StallRepWorse".Translate((-v.repPriceBonus * 100f).ToString("F0")));
			return string.Join("\n", lines);
		}

		private void SwapSide()
		{
			string why = PlaceWorker_MarketStall.SwapConflict(this);
			if (why != null)
			{
				Messages.Message(why, parent, MessageTypeDefOf.RejectInput, false);
				return;
			}
			flipped = !flipped;
			reachCheckTick = -99999;
		}

		private bool MerchantReachesEdge()
		{
			int now = Find.TickManager.TicksGame;
			if (now - reachCheckTick >= ReachCheckTicks || now < reachCheckTick)
			{
				reachCheckTick = now;
				reachesEdge = Map.reachability.CanReachMapEdge(MerchantCell, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly));
			}
			return reachesEdge;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
			StallVisit v = Visit;
			if (Props.merchantAtSide && parent.Faction == Faction.OfPlayer)
			{
				var swap = new Command_Action
				{
					defaultLabel = "RimMarket.SwapSideLabel".Translate(),
					defaultDesc = "RimMarket.SwapSideDesc".Translate(),
					icon = RimMarketIcons.SwapSide,
					action = SwapSide,
				};
				if (v != null) swap.Disable("RimMarket.SwapSideBusy".Translate());
				yield return swap;
			}
			if (v == null) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.TradeGizmoLabel".Translate(),
				defaultDesc = "RimMarket.TradeGizmoDesc".Translate(v.trader?.LabelShortCap ?? "?"),
				icon = RimMarketIcons.Trade,
				action = () => MarketUtility.OpenTradeMenu(this),
			};
		}
	}
}
