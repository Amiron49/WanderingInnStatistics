using System;

namespace WanderingInnStats.Core
{
	/// <summary>The kinds of ground-truth lists taken from the wiki, besides characters.</summary>
	public enum WikiEntityKind
	{
		Spell,
		Class,
		Species,
		Monster,
		Location
	}

	/// <summary>One entry of a wiki list (a spell, class, species, monster or location).</summary>
	public record WikiEntityRaw
	{
		public WikiEntityKind Kind { get; init; }

		/// <summary>The name without brackets, e.g. "Flashbang" for [Flashbang].</summary>
		public string Name { get; init; } = "";

		public string[] Aliases { get; init; } = Array.Empty<string>();

		/// <summary>The entry this one is nested under, e.g. "Dragons" for "Frost Dragons".</summary>
		public string? Parent { get; init; }

		public string? Description { get; init; }

		/// <summary>Spell tier as written on the wiki ("0", "1 or 2", ...).</summary>
		public string? Tier { get; init; }

		/// <summary>Characters the wiki lists as having this class.</summary>
		public string[] KnownCharacters { get; init; } = Array.Empty<string>();

		/// <summary>Classes listed as hypothetical/fake, or spells without a [box].</summary>
		public bool Hypothetical { get; init; }

		public string? WikiUrl { get; init; }
	}
}
