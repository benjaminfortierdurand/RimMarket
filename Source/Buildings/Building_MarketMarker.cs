using System.Collections.Generic;
using RimMarket.Compat;
using RimMarket.Fair;
using RimMarket.Market;
using RimMarket.Reputation;
using RimMarket.UI;
using RimWorld;
using Verse;

namespace RimMarket.Buildings
{
	public class Building_MarketMarker : Building
	{
		public const int FeeModeGlobal = -1;

		private bool markerHidden;
		private bool open = true;
		private int feeMode = FeeModeGlobal;
		private CompMarketQuarter quarter;

		public bool Open => open;

		public void SetOpen(bool on) => open = on;

		public CompMarketQuarter Quarter => quarter ?? (quarter = GetComp<CompMarketQuarter>());

		public int EffectiveFeeMode => feeMode == FeeModeGlobal ? RimMarketMod.Settings.FeeModeSafe : RimMarketSettings.Sanitize(feeMode);

		private static int NextFeeMode(int mode)
		{
			if (mode == FeeModeGlobal) return RimMarketSettings.FeeNone;
			int next = mode + 1;
			if (next == RimMarketSettings.FeeShare && !RimMarketSettings.ShareModeEnabled) next++;
			return next > RimMarketSettings.FeeShare ? FeeModeGlobal : next;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			if (def.MadeFromStuff && Stuff == null) SetStuffDirect(GenStuff.DefaultStuffFor(def));
			base.SpawnSetup(map, respawningAfterLoad);
			quarter = GetComp<CompMarketQuarter>();
			if (!respawningAfterLoad && Faction == Faction.OfPlayer) MarketManager.Instance?.OfferGuide(this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref markerHidden, "markerHidden");
			Scribe_Values.Look(ref open, "open", true);
			Scribe_Values.Look(ref feeMode, "feeMode", FeeModeGlobal);
		}

		public override void Print(SectionLayer layer)
		{
			if (markerHidden || FairUtility.MastOn(Map) == this) return;
			base.Print(layer);
		}

		private string FeeModeLabel(bool shortForm)
		{
			if (feeMode != FeeModeGlobal) return RimMarketSettings.FeeModeLabel(RimMarketSettings.Sanitize(feeMode));
			string mode = RimMarketSettings.FeeModeLabel(RimMarketMod.Settings.FeeModeSafe);
			return (shortForm ? "RimMarket.FeeModeGlobalShort" : "RimMarket.FeeModeGlobal").Translate(mode).ToString();
		}

		private static string ReputationLine(MarketReputation rep)
		{
			var words = new List<string>();
			for (int i = 0; i < MarketReputation.AxisCount; i++)
			{
				string w = rep.StageWord(i);
				if (w != null) words.Add(w);
			}
			string head = "RimMarket.MarkerReputation".Translate(rep.HeadlineLabel);
			return words.Count == 0 ? head : head + " (" + string.Join(", ", words) + ")";
		}

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			var lines = new List<string>();
			if (!s.NullOrEmpty()) lines.Add(s);
			lines.Add(open ? "RimMarket.MarketOpen".Translate() : "RimMarket.MarketClosed".Translate());
			MarketManager mgr0 = MarketManager.Instance;
			int total = MarketUtility.CountStalls(this, out int free, out int busy);
			lines.Add("RimMarket.MarkerStalls".Translate(total, free, busy));
			if (mgr0 != null) lines.Add(ReputationLine(mgr0.Reputation));
			lines.Add("RimMarket.MarkerFee".Translate(FeeModeLabel(shortForm: false)));
			MarketManager mgr = MarketManager.Instance;
			if (mgr != null) lines.Add("RimMarket.MarkerCollected".Translate(mgr.TotalFees));
			if (mgr != null && (HospitalityBridge.Active || HotelsBridge.Active)) lines.Add("RimMarket.MarkerGuestSales".Translate(mgr.TotalGuestSales));
			if (mgr != null && mgr.TotalColonySales > 0) lines.Add("RimMarket.MarkerColonySales".Translate(mgr.TotalColonySales));
			if (mgr != null && mgr.ThievesCaught + mgr.ThievesEscaped + mgr.ThievesDeterred > 0)
				lines.Add("RimMarket.MarkerSecurity".Translate(mgr.ThievesCaught, mgr.ThievesEscaped, mgr.ThievesDeterred));
			if (Spawned)
			{
				GreatFair fair = FairUtility.On(Map);
				string fairLine = fair != null && fair.marker == this ? FairUtility.InspectLine(fair) : null;
				if (fairLine != null) lines.Add(fairLine);
			}
			if (markerHidden) lines.Add("RimMarket.MarkerHidden".Translate());
			if (Spawned && TradingControlBridge.SpotOnMap(Map)) lines.Add("RimMarket.TradingControlNote".Translate());
			return string.Join("\n", lines);
		}

		private IEnumerable<Gizmo> NoticeGizmos()
		{
			if (!Spawned || Faction != Faction.OfPlayer) yield break;
			Orders.WantedNotice notice = MarketManager.Instance?.Notice;
			if (notice == null || notice.home != Map.Parent) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimMarket.WantedGizmo".Translate(notice.offers.Count),
				defaultDesc = "RimMarket.WantedGizmoDesc".Translate(),
				icon = RimMarketIcons.WantedNotice,
				action = () => Find.WindowStack.Add(new Orders.Dialog_WantedBoard()),
			};
		}

		private IEnumerable<Gizmo> FairGizmos()
		{
			if (!RimMarketMod.Settings.fairEnabled || !Spawned || Faction != Faction.OfPlayer) yield break;
			GreatFair fair = FairUtility.On(Map);
			if (fair != null)
			{
				if (fair.marker != this || fair.state != GreatFair.Announced) yield break;
				yield return new Command_Action
				{
					defaultLabel = "RimMarket.FairCancelLabel".Translate(),
					defaultDesc = "RimMarket.FairCancelDesc".Translate(),
					icon = RimMarketIcons.GreatFair,
					action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"RimMarket.FairCancelConfirm".Translate(),
						() => FairUtility.Cancel(fair, "RimMarket.FairCancelled"),
						destructive: true)),
				};
				yield break;
			}
			int cost = RimMarketMod.Settings.fairCost;
			var announce = new Command_Action
			{
				defaultLabel = "RimMarket.FairAnnounceLabel".Translate(),
				defaultDesc = "RimMarket.FairAnnounceDesc".Translate(cost, FairUtility.LeadDays, FairUtility.MaxCaravans),
				icon = RimMarketIcons.GreatFair,
				action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"RimMarket.FairAnnounceConfirm".Translate(FairUtility.DateOf(FairUtility.StartTickFor(Map), Map), cost),
					() => FairUtility.Announce(this))),
			};
			AcceptanceReport can = FairUtility.CanAnnounce(this);
			if (!can.Accepted) announce.Disable(can.Reason);
			yield return announce;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			if (Faction == Faction.OfPlayer) LessonAutoActivator.TeachOpportunity(RimMarketDefOf.RimMarket_Concept_Guide, OpportunityType.GoodToKnow);

			yield return new Command_Action
			{
				defaultLabel = "RimMarket.Guide.GizmoLabel".Translate(),
				defaultDesc = "RimMarket.Guide.GizmoDesc".Translate(),
				icon = TexButton.Info,
				action = () => Window_MarketGuide.Open(),
			};

			yield return new Command_Toggle
			{
				defaultLabel = "RimMarket.ShowMarkerLabel".Translate(),
				defaultDesc = "RimMarket.ShowMarkerDesc".Translate(),
				icon = RimMarketIcons.ShowMarker,
				isActive = () => !markerHidden,
				toggleAction = delegate
				{
					markerHidden = !markerHidden;
					if (Spawned) DirtyMapMesh(Map);
				},
			};

			yield return new Command_Toggle
			{
				defaultLabel = "RimMarket.OpenMarketLabel".Translate(),
				defaultDesc = "RimMarket.OpenMarketDesc".Translate(),
				icon = RimMarketIcons.OpenMarket,
				isActive = () => open,
				toggleAction = delegate { open = !open; },
			};

			yield return new Command_Action
			{
				defaultLabel = "RimMarket.FeeModeLabel".Translate(FeeModeLabel(shortForm: true)),
				defaultDesc = RimMarketSettings.ShareModeEnabled
					? "RimMarket.FeeModeDesc".Translate() + "\n\n" + "RimMarket.FeeModeDescShare".Translate()
					: "RimMarket.FeeModeDesc".Translate(),
				icon = RimMarketIcons.FeeMode,
				action = delegate { feeMode = NextFeeMode(feeMode); },
			};

			yield return new Command_Action
			{
				defaultLabel = "RimMarket.ReputationLabel".Translate(),
				defaultDesc = "RimMarket.ReputationDesc".Translate(),
				icon = RimMarketIcons.Reputation,
				action = Window_MarketReputation.Open,
			};

			foreach (Gizmo g in FairGizmos()) yield return g;

			MarketManager mgr = MarketManager.Instance;
			if (mgr != null && Spawned && mgr.ActiveVisits(Map) > 0)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimMarket.CloseMarketLabel".Translate(),
					defaultDesc = "RimMarket.CloseMarketDesc".Translate(),
					icon = RimMarketIcons.CloseMarket,
					action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"RimMarket.CloseMarketConfirm".Translate(mgr.ActiveVisits(Map)),
						() => mgr.CloseMarket(Map),
						destructive: true)),
				};
			}
		}
	}
}
