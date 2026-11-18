using UnityEngine;
using Verse;

namespace RimMarket.Buildings
{
	public class StallFreezerExtension : DefModExtension
	{
		public float temperature = -10f;
		public Vector3 waresShift = Vector3.zero;
		public Vector3 verticalWaresShift = Vector3.zero;
		public float verticalWaresScale = -1f;
	}
}
