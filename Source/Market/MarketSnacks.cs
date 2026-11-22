using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimMarket.Market
{
	public static class MarketSnacks
	{
		private const float SnackBelow = 0.75f;
		private const float CravingBelow = 0.5f;

		public static Thing Carried(Pawn pawn, ThingDef def)
		{
			ThingOwner carried = pawn?.inventory?.innerContainer;
			if (carried == null || def == null) return null;
			for (int i = 0; i < carried.Count; i++)
			{
				if (carried[i].def == def) return carried[i];
			}
			return null;
		}

		public static Job UseJob(Pawn pawn, Thing item)
		{
			ThingDef def = item.def;
			Job use = JobMaker.MakeJob(JobDefOf.Ingest, item);
			use.count = def.IsDrug ? 1 : Mathf.Clamp(FoodUtility.WillIngestStackCountOf(pawn, def, FoodUtility.GetNutrition(pawn, item, def)), 1, item.stackCount);
			return use;
		}

		public static bool WantsNow(Pawn pawn, Thing item)
		{
			ThingDef def = item.def;
			if (def.ingestible == null) return false;
			if (def.IsDrug)
			{
				if (!pawn.CanTakeDrug(def)) return false;
				ChemicalDef chemical = item.TryGetComp<CompDrug>()?.Props.chemical;
				if (chemical != null && AddictionUtility.IsAddicted(pawn, chemical))
				{
					Need craving = chemical.addictionHediff?.chemicalNeed == null ? null : pawn.needs?.TryGetNeed(chemical.addictionHediff.chemicalNeed);
					return craving != null && craving.CurLevelPercentage < CravingBelow;
				}
				if (def.ingestible.drugCategory != DrugCategory.Social) return false;
				List<IngestionOutcomeDoer> effects = def.ingestible.outcomeDoers;
				if (effects == null) return true;
				for (int i = 0; i < effects.Count; i++)
				{
					if (effects[i] is IngestionOutcomeDoer_GiveHediff high && high.hediffDef != null && pawn.health.hediffSet.HasHediff(high.hediffDef)) return false;
				}
				return true;
			}
			if (!def.IsNutritionGivingIngestible || def.ingestible.preferability < FoodPreferability.RawTasty) return false;
			Need_Food food = pawn.needs?.food;
			return food != null && food.CurLevelPercentage < SnackBelow && pawn.WillEat(item);
		}

		public static Job EveningDrink(Pawn pawn)
		{
			if (pawn.IsColonist || !pawn.RaceProps.Humanlike) return null;
			ThingOwner carried = pawn.inventory?.innerContainer;
			if (carried == null) return null;
			for (int i = 0; i < carried.Count; i++)
			{
				Thing t = carried[i];
				if (t.def.IsDrug && WantsNow(pawn, t)) return UseJob(pawn, t);
			}
			return null;
		}
	}
}
