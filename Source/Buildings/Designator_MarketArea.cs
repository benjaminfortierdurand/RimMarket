using System.Collections.Generic;
using RimMarket.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class Designator_MarketArea : Designator_Cells
	{
		private const float BadgeSize = 0.46f;

		private readonly CompMarketQuarter quarter;
		private readonly bool add;
		private readonly Texture2D badge;

		public override bool DragDrawMeasurements => true;

		public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

		public Designator_MarketArea(CompMarketQuarter quarter, bool add)
		{
			this.quarter = quarter;
			this.add = add;
			string kind = quarter?.AreaKey ?? "Market";
			defaultLabel = ((add ? "RimMarket.AreaPaint" : "RimMarket.AreaErase") + kind).Translate();
			defaultDesc = ((add ? "RimMarket.AreaPaintDesc" : "RimMarket.AreaEraseDesc") + kind).Translate();
			icon = add ? RimMarketIcons.AreaExpand : RimMarketIcons.AreaClear;
			badge = quarter?.parent?.def?.uiIcon;
			useMouseIcon = true;
			soundDragSustain = add ? SoundDefOf.Designate_DragAreaAdd : SoundDefOf.Designate_DragAreaDelete;
			soundDragChanged = add ? SoundDefOf.Designate_DragZone_Changed : null;
			soundSucceeded = add ? SoundDefOf.Designate_ZoneAdd_AllowedArea : SoundDefOf.Designate_ZoneDelete;
		}

		private bool QuarterOk => quarter != null && quarter.parent.Spawned && quarter.parent.Map == Map;

		public override AcceptanceReport CanDesignateCell(IntVec3 c)
		{
			if (!QuarterOk || !c.InBounds(Map)) return false;
			if (add)
			{
				if (c.Fogged(Map)) return false;
				if (!quarter.InReach(c)) return "RimMarket.AreaTooFar".Translate(Mathf.RoundToInt(CompMarketQuarter.MaxRadius));
				return !quarter.HasRegion || !quarter.InQuarter(c);
			}
			if (quarter.IsMarkerCell(c)) return ("RimMarket.AreaKeep" + quarter.AreaKey).Translate();
			return quarter.InQuarter(c);
		}

		public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
		{
			var picked = new List<IntVec3>();
			foreach (IntVec3 c in cells)
			{
				if (CanDesignateCell(c).Accepted) picked.Add(c);
			}
			if (picked.Count > 0) quarter.Paint(picked, add);
			Finalize(picked.Count > 0);
		}

		public override void DesignateSingleCell(IntVec3 c)
		{
			quarter.Paint(new List<IntVec3> { c }, add);
		}

		public override bool CanRemainSelected() => QuarterOk;

		public override void SelectedUpdate()
		{
			GenUI.RenderMouseoverBracket();
			if (QuarterOk) quarter.DrawArea();
		}

		public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
		{
			base.DrawIcon(rect, buttonMat, parms);
			float size = rect.width * BadgeSize;
			var spot = new Rect(rect.xMax - size - rect.width * 0.04f, rect.yMax - size - rect.height * 0.04f, size, size);
			if (badge == null) return;
			GUI.color = parms.lowLight ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
			GUI.DrawTexture(spot, RimMarketIcons.AreaBadge);
			GUI.DrawTexture(spot, badge);
			GUI.color = Color.white;
		}
	}
}
