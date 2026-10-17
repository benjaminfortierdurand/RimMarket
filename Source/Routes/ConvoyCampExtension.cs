using RimWorld;
using Verse;

namespace RimMarket.Routes
{
	public class ConvoyCampExtension : DefModExtension
	{
		public TechLevel minTech = TechLevel.Undefined;
		public TechLevel maxTech = TechLevel.Archotech;
		public int priority;

		public bool Fits(TechLevel tech) => tech >= minTech && tech <= maxTech;
	}
}
