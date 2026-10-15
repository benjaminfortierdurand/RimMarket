using HarmonyLib;
using RimMarket.Market;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimMarket.HarmonyPatches
{
	[HarmonyPatch(typeof(Pawn_TraderTracker), nameof(Pawn_TraderTracker.GiveSoldThingToPlayer))]
	public static class Patch_TraderTracker_GiveSoldThingToPlayer
	{
		public static bool Prefix(Thing toGive, int countToGive, Pawn playerNegotiator, Pawn ___pawn)
		{
			if (toGive == null || toGive is Pawn || !MarketManager.AnyVisits) return true;
			if (playerNegotiator == null || !playerNegotiator.Spawned || MarketManager.Instance.VisitFor(___pawn) == null) return true;
			Map map = playerNegotiator.Map;
			if (toGive.MapHeld != map) return true;
			IntVec3 counter = playerNegotiator.Position;
			IntVec3 held = toGive.PositionHeld;
			Thing thing = toGive.SplitOff(countToGive);
			thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, ___pawn);
			if (GenPlace.TryPlaceThing(thing, counter, map, ThingPlaceMode.Near) || GenPlace.TryPlaceThing(thing, held, map, ThingPlaceMode.Near))
			{
				___pawn.GetLord()?.extraForbiddenThings.Add(thing);
				return false;
			}
			Log.Error("[RimMarket] Could not place bought thing " + thing + " near " + counter);
			thing.Destroy();
			return false;
		}
	}
}
