using System.Collections.Generic;
using System.Linq;
using RimMarket.Market;
using RimMarket.Reputation;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RimMarket.Inn
{
	public static class InnBeds
	{
		private static readonly Dictionary<Map, List<Building_Bed>> byMap = new Dictionary<Map, List<Building_Bed>>();
		private static readonly List<Building_Bed> Empty = new List<Building_Bed>();
		private static int count;

		public static bool Any => count > 0;

		public static void Reset()
		{
			byMap.Clear();
			count = 0;
		}

		public static void Register(Building_Bed bed)
		{
			Map map = bed?.Map;
			if (map == null) return;
			if (!byMap.TryGetValue(map, out List<Building_Bed> list))
			{
				list = new List<Building_Bed>();
				byMap[map] = list;
			}
			if (list.Contains(bed)) return;
			list.Add(bed);
			count++;
		}

		public static void Unregister(Building_Bed bed, Map map)
		{
			if (bed == null || map == null || !byMap.TryGetValue(map, out List<Building_Bed> list)) return;
			if (list.Remove(bed)) count = Mathf.Max(0, count - 1);
		}

		public static List<Building_Bed> On(Map map)
		{
			if (map == null || !byMap.TryGetValue(map, out List<Building_Bed> list)) return Empty;
			int removed = list.RemoveAll(b => b == null || !b.Spawned || b.Map != map || !IsInn(b));
			if (removed > 0) count = Mathf.Max(0, count - removed);
			return list;
		}

		public static CompInnBed CompOf(Thing bed) => (bed as ThingWithComps)?.GetComp<CompInnBed>();

		public static bool IsInn(Thing bed)
		{
			if (!(bed is Building_Bed)) return false;
			CompInnBed comp = CompOf(bed);
			return comp != null && comp.inn;
		}
	}

	public static class InnUtility
	{
		public const int PaidSpan = 45000;
		private const float TiredLevel = 0.3f;
		private const float NightLevel = 0.75f;
		private const int NightStart = 22;
		private const int NightEnd = 6;
		private const float PoorImpressiveness = 15f;
		private const float FineImpressiveness = 45f;

		public static readonly Color SheetColor = new Color32(150, 60, 95, 255);

		public static int Price => Mathf.Max(0, RimMarketMod.Settings.innPrice);

		public static string BedLabel
		{
			get
			{
				int price = Price;
				return price > 0 ? "RimMarket.InnBedLabel".Translate(price).ToString() : "RimMarket.InnBedLabelFree".Translate().ToString();
			}
		}

		public static bool MayLodge(Pawn p)
		{
			if (p == null || p.Dead || !p.Spawned || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
			if (p.Faction == null || p.Faction.IsPlayer || p.Faction.HostileTo(Faction.OfPlayer)) return false;
			if (p.IsPrisoner || p.IsSlave || p.HostFaction != null) return false;
			if (TraderCaravanUtility.GetTraderCaravanRole(p) == TraderCaravanRole.Chattel) return false;
			Lord lord = p.GetLord();
			if (lord == null) return false;
			if (lord.LordJob is LordJob_MarketShoppers) return true;
			StallVisit v = MarketManager.Instance?.VisitFor(lord);
			return v != null && !v.closed;
		}

		public static bool WantsSleep(Pawn p)
		{
			Need_Rest rest = p.needs?.rest;
			if (rest == null || p.Downed || p.InMentalState) return false;
			if (Find.TickManager.TicksGame < p.mindState.canSleepTick) return false;
			if (!RestUtility.CanFallAsleep(p) || RestUtility.DisturbancePreventsLyingDown(p)) return false;
			LordToil toil = p.GetLord()?.CurLordToil;
			if (toil != null && (!toil.AllowSatisfyLongNeeds || !toil.AllowRestingInBed)) return false;
			if (rest.CurLevel < TiredLevel) return true;
			if (p.mindState.duty?.def == RimMarketDefOf.RimMarket_GuardPost) return false;
			int hour = GenLocalDate.HourOfDay(p);
			return (hour >= NightStart || hour < NightEnd) && rest.CurLevel < NightLevel;
		}

		public static List<Pawn> Payers(Pawn p)
		{
			Lord lord = p.GetLord();
			if (lord != null && !(lord.LordJob is LordJob_MarketShoppers))
			{
				StallVisit v = MarketManager.Instance?.VisitFor(lord);
				if (v != null) return lord.ownedPawns;
			}
			return new List<Pawn> { p };
		}

		public static int SilverAmong(List<Pawn> pawns)
		{
			int n = 0;
			for (int i = 0; i < pawns.Count; i++) n += MarketUtility.SilverOf(pawns[i]);
			return n;
		}

		public static bool Paid(Pawn p)
		{
			MarketManager mgr = MarketManager.Instance;
			return mgr != null && mgr.InnPaidUntil(p) > Find.TickManager.TicksGame;
		}

		public static bool CanAfford(Pawn p)
		{
			int price = Price;
			return price <= 0 || Paid(p) || SilverAmong(Payers(p)) >= price;
		}

		public static bool CanUse(Building_Bed bed, Pawn p)
		{
			if (bed == null || !bed.Spawned || bed.Map != p.Map || bed.Faction != Faction.OfPlayer || !InnBeds.IsInn(bed)) return false;
			if (!RestUtility.CanUseBedNow(bed, p, checkSocialProperness: true)) return false;
			if (bed.Position.GetDangerFor(p, p.Map) == Danger.Deadly) return false;
			if (!p.CanReserve(bed, bed.SleepingSlotsCount, 0)) return false;
			return p.CanReach(bed, PathEndMode.OnCell, Danger.Some);
		}

		public static Building_Bed FindBed(Pawn p)
		{
			Building_Bed owned = p.ownership?.OwnedBed;
			if (owned != null && CanUse(owned, p)) return owned;
			List<Building_Bed> beds = InnBeds.On(p.Map);
			Building_Bed best = null;
			float bestDist = float.MaxValue;
			for (int i = 0; i < beds.Count; i++)
			{
				Building_Bed bed = beds[i];
				if (bed == owned) continue;
				float d = bed.Position.DistanceToSquared(p.Position);
				if (d >= bestDist || !CanUse(bed, p)) continue;
				best = bed;
				bestDist = d;
			}
			return best;
		}

		public static bool Charge(Pawn p, Building_Bed bed)
		{
			MarketManager mgr = MarketManager.Instance;
			if (mgr == null || p == null || bed == null || !bed.Spawned) return false;
			int now = Find.TickManager.TicksGame;
			if (mgr.InnPaidUntil(p) > now) return true;
			int price = Price;
			int got = 0;
			if (price > 0)
			{
				List<Pawn> payers = Payers(p);
				if (SilverAmong(payers) < price) return false;
				List<Thing> taken = MarketUtility.TakePayment(payers, price, false);
				for (int i = 0; i < taken.Count; i++)
				{
					got += taken[i].stackCount;
					GenPlace.TryPlaceThing(taken[i], p.Position, bed.Map, ThingPlaceMode.Near);
				}
				if (got > 0)
				{
					RimMarketDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(p.Position, bed.Map));
					MoteMaker.ThrowText(bed.DrawPos, bed.Map, "+" + got, 3.9f);
				}
			}
			mgr.SetInnPaid(p, now + PaidSpan);
			Rate(p, bed, got, price);
			return true;
		}

		public static int Comfort(Building_Bed bed)
		{
			Room room = bed?.GetRoom();
			if (room == null || room.PsychologicallyOutdoors) return -1;
			float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
			if (impressiveness < PoorImpressiveness) return -1;
			int beds = 0;
			foreach (Building_Bed b in room.ContainedBeds)
			{
				if (!b.Medical) beds++;
			}
			return beds <= 1 && impressiveness >= FineImpressiveness ? 1 : 0;
		}

		public static bool Steep(int comfort, int price)
		{
			if (comfort < 0) return price >= 20;
			if (comfort == 0) return price >= 45;
			return false;
		}

		private static void Rate(Pawn p, Building_Bed bed, int silver, int price)
		{
			MarketManager mgr = MarketManager.Instance;
			int comfort = Comfort(bed);
			bool steep = Steep(comfort, price);
			Lord lord = p.GetLord();
			StallVisit v = mgr.VisitFor(lord);
			if (v != null)
			{
				v.innEarned += silver;
				v.innNights++;
				v.innComfort += comfort;
				if (steep) v.innSteep = true;
				return;
			}
			if (!(lord?.LordJob is LordJob_MarketShoppers shoppers) || shoppers.innRated) return;
			shoppers.innRated = true;
			if (comfort > 0) mgr.Record(null, p.Faction, MarketReputation.Appeal, 1f, "RimMarket.ReasonInnFine");
			else if (comfort < 0) mgr.Record(null, p.Faction, MarketReputation.Appeal, -1f, "RimMarket.ReasonInnPoor");
			if (steep) mgr.Record(null, p.Faction, MarketReputation.Fairness, -1f, "RimMarket.ReasonInnSteep");
		}

		public static bool AnyLodging(Lord lord)
		{
			if (lord == null) return false;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].CurJobDef == RimMarketDefOf.RimMarket_SleepAtInn) return true;
			}
			return false;
		}

		public static void EvictAll(Building_Bed bed)
		{
			if (bed?.CompAssignableToPawn == null) return;
			List<Pawn> owners = bed.OwnersForReading.ToList();
			for (int i = 0; i < owners.Count; i++)
			{
				Pawn o = owners[i];
				if (o.ownership != null && o.ownership.OwnedBed == bed) o.ownership.UnclaimBed();
				else bed.CompAssignableToPawn.ForceRemovePawn(o);
			}
		}

		public static void CheckOut(Pawn p)
		{
			Building_Bed bed = p?.ownership?.OwnedBed;
			if (bed != null && InnBeds.IsInn(bed)) p.ownership.UnclaimBed();
		}

		public static void Notify_PurposeChanged(Building_Bed bed)
		{
			CompInnBed comp = InnBeds.CompOf(bed);
			if (comp == null || !comp.inn) return;
			if (bed.Medical || bed.ForOwnerType != BedOwnerType.Colonist) comp.SetInn(false);
		}

		public static void Tidy()
		{
			List<Map> maps = Find.Maps;
			for (int m = 0; m < maps.Count; m++)
			{
				List<Building_Bed> beds = InnBeds.On(maps[m]);
				for (int i = 0; i < beds.Count; i++)
				{
					Building_Bed bed = beds[i];
					List<Pawn> owners = bed.OwnersForReading;
					for (int j = owners.Count - 1; j >= 0; j--)
					{
						Pawn o = owners[j];
						if (o == null || (o.Spawned && o.Map == bed.Map && MayLodge(o))) continue;
						if (o.ownership != null && o.ownership.OwnedBed == bed) o.ownership.UnclaimBed();
						else bed.CompAssignableToPawn.ForceRemovePawn(o);
					}
				}
			}
		}

		public static void Tire(Map map)
		{
			IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (MayLodge(p) && p.needs?.rest != null) p.needs.rest.CurLevel = 0.2f;
			}
		}
	}
}
