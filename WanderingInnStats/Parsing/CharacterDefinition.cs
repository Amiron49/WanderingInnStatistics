using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace WanderingInnStats.Parsing
{
	[DebuggerDisplay("{" + nameof(Name) + "}")]
	public class CharacterDefinition : IEquatable<CharacterDefinition>, IJsonMe
	{
		public string Name { get; init; }
		public string[] NameParts { get; init; }
		public string[] Aliases { get; init; }

		public string? Gender { get; init; }
		public string? Species { get; init; }
		public string? Age { get; init; }
		public string[] Affiliations { get; init; }
		public string[] FamilyMembers { get; init; }
		public string[] Occupations { get; init; }
		public string? Residence { get; init; }
		public string WikiUrl { get; init; }

		public CharacterDefinition(string wikiUrl, string rawName)
		{
			NameParts = rawName.Split(" ").ToArray();
			Name = rawName;
			WikiUrl = wikiUrl;
		}


		public bool Equals(CharacterDefinition? other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Name == other.Name;
		}

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((CharacterDefinition) obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Name);
		}

		public static bool operator ==(CharacterDefinition? left, CharacterDefinition? right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(CharacterDefinition? left, CharacterDefinition? right)
		{
			return !Equals(left, right);
		}

		public MentionMatch ContainsMention(string[] input)
		{
			var requiredForPartialMatch = (int) Math.Ceiling(NameParts.Length / 2d);

			if (string.Join(" ", input).Equals(Name, StringComparison.OrdinalIgnoreCase))
				return MentionMatch.FullName;

			var aliasContain = input.Any(x => Aliases.Any(y => x == y));

			if (aliasContain)
				return MentionMatch.Alias;

			var matches = 0;

			for (var i = 0; i < input.Length && i < NameParts.Length; i++)
			{
				var inputPart = input[i];
				var namePart = NameParts[i];

				if (inputPart == namePart)
					matches++;
				else
				{
					if (matches == NameParts.Length)
						return MentionMatch.FullName;
					if (matches == requiredForPartialMatch)
						return MentionMatch.PartialName;
					if (matches == input.Length)
						return MentionMatch.KindaContainsIt;
					if (matches > 0)
						return MentionMatch.None;
				}
			}

			return MentionMatch.None;
		}

		public Regex CreateRegex()
		{
			var regexes = new List<string>()
			{
				$@"\b{Name.RegexSafe()}\b"
			};
			var aliasRegex = CreateAliasRegex();
			var namePartsRegex = CreateNamePartsRegex();

			if (aliasRegex != null)
				regexes.Add(aliasRegex);
			if (namePartsRegex != null)
				regexes.Add(namePartsRegex);


			var join = string.Join(")|(", regexes);
			return new Regex($"({@join})");
		}
		
		private string? CreateNamePartsRegex()
		{
			if (NameParts.Length <= 1)
				return null;

			var namePartsProgressive = new List<string>();
			for (var i = 1; i < NameParts.Length; i++)
			{
				var front = string.Join(" ", NameParts.Select(FuckingRegex.RegexSafe).Take(i));
				namePartsProgressive.Add(front);
				var back = string.Join(" ", NameParts.Select(FuckingRegex.RegexSafe).Skip(i));
				namePartsProgressive.Add(back);
			}

			var namePartsRegex = $@"\b({string.Join(")|(", namePartsProgressive)})\b";

			return namePartsRegex;
		}

		private string? CreateAliasRegex()
		{
			if (!Aliases.Any())
				return null;

			return $@"\b({string.Join(")|(", Aliases.Select(FuckingRegex.RegexSafe))})\b";
		}

        public string JsonEquivalent => Name;
    }

	public static class FuckingRegex
	{
		public static string RegexSafe(this string input)
		{
			return input
				.Replace("]", @"\]")
				.Replace("[", @"\[");
		}
	}

	public enum MentionMatch
	{
		None,
		FullName,
		PartialName,
		KindaContainsIt,
		Alias
	}

    public interface IJsonMe
    {
        public string JsonEquivalent { get; }
    }
}