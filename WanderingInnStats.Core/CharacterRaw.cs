using System.Collections.Generic;

namespace WanderingInnStats.Core
{
	public class CharacterRaw
	{
		public string Name { get; init; }
		public string[] Aliases { get; init; }

		public string? Gender { get; init; }
		public string? Species { get; init; }
		public string? Age { get; init; }
		public Dictionary<string, string[]> Affiliations { get; init; }
		public string[] FamilyMembers { get; init; }
		public string[] Occupations { get; init; }
		public string? Residence { get; init; }
		public string WikiUrl { get; init; }
	}
}