using System;
using System.Collections.Generic;
using System.Linq;
using RimMarket.Buildings;
using RimMarket.Compat;
using RimMarket.Inn;
using RimMarket.Market;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimMarket.UI
{
	public enum GuideLine
	{
		Required,
		Optional,
		Info,
		Problem,
	}

	public class GuideCheck
	{
		public string label;
		public bool done;
		public GuideLine kind;

		public GuideCheck(string label, bool done, GuideLine kind)
		{
			this.label = label;
			this.done = done;
			this.kind = kind;
		}
	}

	public class GuideAction
	{
		public string label;
		public Action<Map> act;
		public Func<Map, bool> enabled;
	}

	public class GuideChapter
	{
		public string key;
		public Texture2D art;
		public bool advanced;
		public bool diagnostic;
		public Func<Map, List<GuideCheck>> checks;
		public List<GuideAction> actions = new List<GuideAction>();

		public string Title => ("RimMarket.Guide." + key + "Title").Translate();
		public string Body => ("RimMarket.Guide." + key + "Body").Translate();
	}

	[StaticConstructorOnStartup]
	public static class MarketGuide
	{
		private static readonly List<CompMarketStall> buffer = new List<CompMarketStall>();

		public static readonly List<GuideChapter> Chapters = new List<GuideChapter>
		{
			new GuideChapter
			{
				key = "Marker", art = Art("Marker"), checks = MarkerChecks,
				actions = { Place("RimMarket.Guide.PlaceMarker", () => RimMarketDefOf.RimMarket_MarketMarker), Show("RimMarket.Guide.ShowMarker", Markers) },
			},
			new GuideChapter
			{
				key = "Stalls", art = Art("Stalls"), checks = StallChecks,
				actions = { Place("RimMarket.Guide.PlaceStall", () => RimMarketDefOf.RimMarket_Stall), Show("RimMarket.Guide.ShowStalls", AllStalls) },
			},
			new GuideChapter
			{
				key = "Spots", art = Art("Spots"), checks = SpotChecks,
				actions = { Place("RimMarket.Guide.PlaceCamp", () => RimMarketDefOf.RimMarket_CaravanCamp), Place("RimMarket.Guide.PlaceRest", () => RimMarketDefOf.RimMarket_GuardRestSpot) },
			},
			new GuideChapter
			{
				key = "Fees", art = Art("Fees"), checks = FeeLines,
				actions = { new GuideAction { label = "RimMarket.Guide.OpenSettings", act = OpenSettings, enabled = m => true } },
			},
			new GuideChapter
			{
				key = "ColonyStall", art = Art("ColonyStall"), checks = ColonyChecks,
				actions = { Show("RimMarket.Guide.ShowStalls", AllStalls) },
			},
			new GuideChapter { key = "Inn", art = Art("Inn"), checks = InnChecks },
			new GuideChapter
			{
				key = "Fair", art = Art("Fair"), advanced = true, checks = FairChecks,
				actions = { Show("RimMarket.Guide.ShowMarker", Markers) },
			},
			new GuideChapter
			{
				key = "Auction", art = Art("Auction"), advanced = true, checks = AuctionChecks,
				actions = { Place("RimMarket.Guide.PlaceAuction", () => RimMarketDefOf.RimMarket_AuctionBlock) },
			},
			new GuideChapter { key = "Routes", art = Art("Wanted"), advanced = true },
			new GuideChapter { key = "Diag", diagnostic = true, checks = Diagnose },
		};

		static MarketGuide()
		{
			if (!HegemonyBridge.Installed) return;
			Chapters.Insert(Chapters.Count - 1, new GuideChapter
			{
				key = "Hegemony", advanced = true, checks = HegemonyLines,
				actions = { new GuideAction { label = "RimMarket.Guide.OpenSettings", act = OpenSettings, enabled = m => true } },
			});
		}

		private static Texture2D Art(string name) => ContentFinder<Texture2D>.Get("UI/Guide/RimMarket_Guide" + name, false);

		private static GuideAction Place(string label, Func<ThingDef> def)
		{
			return new GuideAction
			{
				label = label,
				enabled = m => m != null && BuildCopyCommandUtility.FindAllowedDesignator(def()) != null,
				act = m =>
				{
					Designator_Build des = BuildCopyCommandUtility.FindAllowedDesignator(def());
					if (des == null) return;
					Find.WindowStack.TryRemove(typeof(Window_MarketGuide));
					Find.DesignatorManager.Select(des);
				},
			};
		}

		private static GuideAction Show(string label, Func<Map, List<Thing>> things)
		{
			return new GuideAction
			{
				label = label,
				enabled = m => m != null && things(m).Count > 0,
				act = m =>
				{
					List<Thing> list = things(m);
					if (list.Count == 0) return;
					Find.WindowStack.TryRemove(typeof(Window_MarketGuide));
					Find.Selector.ClearSelection();
					for (int i = 0; i < list.Count; i++) Find.Selector.Select(list[i], playSound: false, forceDesignatorDeselect: false);
					CameraJumper.TryJump(list[0]);
				},
			};
		}

		private static void OpenSettings(Map map)
		{
			Mod mod = LoadedModManager.GetMod<RimMarketMod>();
			if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
		}

		private static List<Thing> Markers(Map map)
		{
			if (map == null) return new List<Thing>();
			return map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_MarketMarker).Where(t => t.Spawned && t.Faction == Faction.OfPlayer).ToList();
		}

		private static List<CompMarketStall> Stalls(Map map)
		{
			var list = new List<CompMarketStall>();
			if (map == null) return list;
			MarketUtility.CollectStalls(map, buffer);
			for (int i = 0; i < buffer.Count; i++)
			{
				if (buffer[i].Spawned && buffer[i].Faction == Faction.OfPlayer) list.Add(buffer[i]);
			}
			return list;
		}

		private static List<Thing> AllStalls(Map map) => Stalls(map).Select(s => (Thing)s.parent).ToList();

		private static bool SpotFree(CompMarketStall s) => s.MerchantCell.InBounds(s.Map) && s.MerchantCell.Standable(s.Map);

		private static bool FromEdge(CompMarketStall s)
		{
			if (!SpotFree(s)) return false;
			return s.Map.reachability.CanReachMapEdge(s.MerchantCell, TraverseParms.For(TraverseMode.PassDoors, Danger.Some));
		}

		private static string L(string key) => ("RimMarket.Guide." + key).Translate();

		private static List<GuideCheck> MarkerChecks(Map map)
		{
			List<Thing> markers = Markers(map);
			return new List<GuideCheck>
			{
				new GuideCheck(L("MarkerPlaced"), markers.Count > 0, GuideLine.Required),
				new GuideCheck(L("MarkerOpen"), markers.Any(t => t is Building_MarketMarker m && m.Open), GuideLine.Required),
			};
		}

		private static List<GuideCheck> StallChecks(Map map)
		{
			List<CompMarketStall> all = Stalls(map).Where(s => !ColonyStalls.IsColonyStall(s.parent)).ToList();
			List<CompMarketStall> inside = all.Where(s => MarketUtility.MarkerFor(s) != null).ToList();
			return new List<GuideCheck>
			{
				new GuideCheck(L("StallInside"), inside.Count > 0, GuideLine.Required),
				new GuideCheck(L("StallNoneOutside"), all.Count > 0 && inside.Count == all.Count, GuideLine.Required),
				new GuideCheck(L("StallSpotFree"), inside.Count > 0 && inside.All(SpotFree), GuideLine.Required),
				new GuideCheck(L("StallFromEdge"), inside.Count > 0 && inside.All(FromEdge), GuideLine.Required),
				new GuideCheck(L("StallThree"), inside.Count >= Fair.FairUtility.MinStalls, GuideLine.Optional),
			};
		}

		private static List<GuideCheck> SpotChecks(Map map)
		{
			return new List<GuideCheck>
			{
				new GuideCheck(L("SpotCamp"), map != null && map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_CaravanCamp).Count > 0, GuideLine.Optional),
				new GuideCheck(L("SpotRest"), map != null && map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_GuardRestSpot).Count > 0, GuideLine.Optional),
			};
		}

		private static List<GuideCheck> FeeLines(Map map)
		{
			RimMarketSettings s = RimMarketMod.Settings;
			string fee = RimMarketSettings.FeeModeLabel(s.FeeModeSafe);
			string haggle = (s.haggleEnabled ? "RimMarket.Guide.On" : "RimMarket.Guide.Off").Translate();
			return new List<GuideCheck>
			{
				new GuideCheck("RimMarket.Guide.FeeDifficulty".Translate(MarketDifficulty.Label(MarketDifficulty.Level)), true, GuideLine.Info),
				new GuideCheck("RimMarket.Guide.FeeMode".Translate(fee), true, GuideLine.Info),
				new GuideCheck("RimMarket.Guide.FeeHaggle".Translate(haggle), true, GuideLine.Info),
				new GuideCheck("RimMarket.Guide.FeeDay".Translate("RimMarket.Settings.Days".Translate(s.marketDays.ToString("0.##"))), true, GuideLine.Info),
			};
		}

		private static List<GuideCheck> HegemonyLines(Map map)
		{
			RimMarketSettings s = RimMarketMod.Settings;
			return new List<GuideCheck>
			{
				OnOff("HegemonyPrices", s.hegemonyPrices),
				OnOff("HegemonyStock", s.hegemonyStock),
				OnOff("HegemonyGoodwill", s.hegemonyGoodwill),
				OnOff("HegemonyTerritory", s.hegemonyTerritory),
			};
		}

		private static GuideCheck OnOff(string key, bool on)
		{
			string state = (on ? "RimMarket.Guide.On" : "RimMarket.Guide.Off").Translate();
			return new GuideCheck("RimMarket.Guide.SettingState".Translate(("RimMarket.Settings." + key).Translate(), state), on, GuideLine.Info);
		}

		private static List<GuideCheck> ColonyChecks(Map map)
		{
			List<Building_MarketStall> colony = Stalls(map).Select(s => s.parent as Building_MarketStall).Where(b => b != null && b.ColonyStall).ToList();
			return new List<GuideCheck>
			{
				new GuideCheck(L("ColonyStallBuilt"), colony.Count > 0, GuideLine.Optional),
				new GuideCheck(L("ColonyStallStocked"), colony.Any(b => b.Orders.Count > 0), GuideLine.Optional),
			};
		}

		private static List<GuideCheck> InnChecks(Map map)
		{
			return new List<GuideCheck> { new GuideCheck(L("InnBed"), map != null && InnBeds.On(map).Count > 0, GuideLine.Optional) };
		}

		private static List<GuideCheck> FairChecks(Map map)
		{
			int n = Stalls(map).Count(s => !ColonyStalls.IsColonyStall(s.parent) && MarketUtility.MarkerFor(s) != null);
			return new List<GuideCheck> { new GuideCheck(L("StallThree"), n >= Fair.FairUtility.MinStalls, GuideLine.Optional) };
		}

		private static List<GuideCheck> AuctionChecks(Map map)
		{
			return new List<GuideCheck> { new GuideCheck(L("AuctionPost"), map != null && map.listerThings.ThingsOfDef(RimMarketDefOf.RimMarket_AuctionBlock).Count > 0, GuideLine.Optional) };
		}

		private static List<GuideCheck> Diagnose(Map map)
		{
			var lines = new List<GuideCheck>();
			if (map == null) return lines;
			MarketManager mgr = MarketManager.Instance;
			RimMarketSettings s = RimMarketMod.Settings;
			lines.Add(new GuideCheck(L("DiagOpen"), mgr != null && mgr.HasOpenMarket(map), GuideLine.Problem));
			List<CompMarketStall> free = Stalls(map).Where(c => !ColonyStalls.IsColonyStall(c.parent) && MarketUtility.InOpenMarket(c)
				&& (mgr == null || mgr.VisitFor(c) == null) && SpotFree(c)).ToList();
			lines.Add(new GuideCheck(L("DiagFreeStall"), free.Count > 0, GuideLine.Problem));
			lines.Add(new GuideCheck(L("DiagReach"), free.Any(FromEdge), GuideLine.Problem));
			int busy = mgr?.ActiveVisits(map) ?? 0;
			lines.Add(new GuideCheck("RimMarket.Guide.DiagRoom".Translate(busy, s.maxMerchants), busy < s.maxMerchants, GuideLine.Problem));
			List<Faction> traders = Reputation.ReputationUtility.TraderFactions().Where(f => !f.HostileTo(Faction.OfPlayer)).ToList();
			lines.Add(new GuideCheck("RimMarket.Guide.DiagFactions".Translate(traders.Count), traders.Count > 0, GuideLine.Problem));
			List<string> refusing = mgr == null ? new List<string>() : traders.Where(f => mgr.RefusalChance(f) > 0f).Select(f => f.Name).ToList();
			lines.Add(refusing.Count == 0
				? new GuideCheck(L("DiagNoRefuse"), true, GuideLine.Problem)
				: new GuideCheck("RimMarket.Guide.DiagRefuse".Translate(refusing.ToCommaList(useAnd: true)), false, GuideLine.Problem));
			if (s.turnAwayIfBroke && s.FeeModeSafe == RimMarketSettings.FeeArrival)
				lines.Add(new GuideCheck(L("DiagBroke"), true, GuideLine.Info));
			if (mgr != null)
			{
				List<Lord> lords = map.lordManager.lords;
				for (int i = 0; i < lords.Count; i++)
				{
					if (!(lords[i].LordJob is LordJob_TradeWithColony) || !mgr.WaitingForStall(lords[i], out string key) || key.NullOrEmpty()) continue;
					lines.Add(new GuideCheck("RimMarket.Guide.DiagWaiting".Translate(lords[i].faction?.Name ?? "?", key.Translate()), false, GuideLine.Problem));
				}
			}
			return lines;
		}
	}
}
