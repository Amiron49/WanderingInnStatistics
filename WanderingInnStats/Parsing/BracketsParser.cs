using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Parsing.IndividualStatistic.Brackets;

namespace WanderingInnStats.Parsing
{
	public class BracketsParser : ITextParser
	{
		private readonly IDestructiveParser[] _parsers;

		public BracketsParser(ILogger logger)
		{
			_parsers = new IDestructiveParser[]
			{
				new BrokenBrackets(logger),
				new ClassMorphParser(logger),
				new SkillMorphParser(logger),
				new ClassConsolidationRemoval(logger),
				new ClassObtain(logger),
				new ClassLoss(logger),
				new ClassLevelUp(logger),
				new ClassWithLevelMention(logger),
				new ClassListingInference(logger),
				new ClassInference(logger),
				new SpellObtain(logger),
				new SkillObtain(logger),
				new SkillLoss(logger),
				new SpellInference(logger),
				new SkillListingInference(logger),
				new SkillInference(logger),
				//new ClassByPossessiveInference(logger),
				new KnownBrackets(logger),
				new UnknownBrackets(logger)
			};
		}

		public void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
			string alteredContent = content;

			foreach (var parser in _parsers)
			{
				alteredContent = parser.Parse(alteredContent, statistics, wanderingInnDefinitions);
			}
		}
	}

	public class SimpleWordStats : ITextParser
	{
        private readonly Regex _wordsRegex = new(@"(?:\w+-?)+");
        private readonly Regex _characterRegex = new(@"\S");

        public void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
            var wordMatches = _wordsRegex.Matches(content);
			statistics.Words = wordMatches.Count;

			var characterMatches = _characterRegex.Matches(content);
			statistics.Characters = characterMatches.Count;
		}
	}

	public class CharacterStats : ITextParser
	{
		public void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var allMatches = new Dictionary<CharacterDefinition, List<Match>>();

			foreach (var characterDefinition in wanderingInnDefinitions.Characters)
			{
				var regex = characterDefinition.CreateRegex();

				var matches = regex.Matches(content).ToArray();

				if (!matches.Any()) 
					continue;
				
				allMatches.AddOrCreate(characterDefinition, matches);
			}

			var grouped = GroupByOverlap(allMatches);
			var cleaned = AttemptToClean(grouped);

			foreach (var clean in cleaned)
				statistics.CharacterMentions.Increment(clean.Key);
			
			//CheckForCollisions(allMatches);
		}

		private static IEnumerable<List<(CharacterDefinition Key, Match individualMatch)>> GroupByOverlap(Dictionary<CharacterDefinition, List<Match>> allMatches)
		{
			var grouped = allMatches
				.SelectMany(pair => pair.Value.Select(individualMatch => (pair.Key, individualMatch)))
				.ToList()
				.GroupByCustom(
					x => x.OrderByDescending(y => y.individualMatch.Length).First(),
					x =>
					{
						var (a, b) = x;

						return Overlaps(a.individualMatch, b.individualMatch);
					});

			return grouped;
		}

		/// <summary>
		/// Whether two matches share any text. Identical spans count: "Mrsha" matched by both "Mrsha" and "Mrsha du Marquin"
		/// (via its first name) used to be counted for both characters.
		/// </summary>
		internal static bool Overlaps(Match a, Match b) =>
			a.Index < b.Index + b.Length && b.Index < a.Index + a.Length;

		/// <summary>How directly a match names the character: its own name beats an alias, which beats a part of its name.</summary>
		internal static int MatchPriority(CharacterDefinition character, Match match)
		{
			if (match.Value == character.Name)
				return 2;
			if (character.Aliases?.Contains(match.Value) == true)
				return 1;
			return 0;
		}
		
		private static IEnumerable<(CharacterDefinition Key, Match individualMatch)> AttemptToClean(IEnumerable<List<(CharacterDefinition Key, Match individualMatch)>> duplicateRidden)
		{
			foreach (var overlapList in duplicateRidden)
			{
				if (overlapList.Count == 1)
				{
					yield return overlapList.First();
					continue;
				}

				// The longest match wins; on equal length, the character whose own name (then alias) was matched:
				// "Mrsha" goes to Mrsha, not also to "Mrsha du Marquin" (Root) via its first name.
				var orderByDescending = overlapList
					.OrderByDescending(x => x.individualMatch.Length)
					.ThenByDescending(x => MatchPriority(x.Key, x.individualMatch))
					.ToList();

                var first = orderByDescending.First();
                var firstPriority = MatchPriority(first.Key, first.individualMatch);
                var tied = orderByDescending
	                .TakeWhile(x => x.individualMatch.Length == first.individualMatch.Length && MatchPriority(x.Key, x.individualMatch) == firstPriority)
	                .ToList();

                // The same text matched equally well for several characters, e.g. a bare family name ("Solstice" for Erin and
                // Gregori Solstice): counted for each of them, as before.
                if (tied.Count > 1 && tied.All(x => x.individualMatch.Index == first.individualMatch.Index))
                {
	                foreach (var each in tied)
		                yield return each;
	                continue;
                }

                if (tied.Count > 1)
                {
	                var second = tied[1];
					throw new InvalidOperationException(
						$"Can't decide between overlapping character mentions '{first.Key.Name}' (\"{first.individualMatch.Value}\" at {first.individualMatch.Index:N0}) " +
						$"and '{second.Key.Name}' (\"{second.individualMatch.Value}\" at {second.individualMatch.Index:N0}): both match the same length. " +
						"Exclude one of the characters on the Wiki page, or give one a distinguishing alias.");
                }

				yield return first;
			}
		}

		private static void CheckForCollisions(Dictionary<CharacterDefinition, List<Match>> allMatches)
		{
			var collisions = allMatches
				.SelectMany(pair => pair.Value.Select(individualMatch => (pair.Key, individualMatch)))
				.ToList()
				.GroupByCustom(
					x => x.OrderByDescending(y => y.individualMatch.Length).First(),
					x =>
					{
						var (a, b) = x;

						var aStart = a.individualMatch.Index;
						var aEnd = aStart + a.individualMatch.Length;
						var bStart = b.individualMatch.Index;
						var bEnd = bStart + b.individualMatch.Length;

						var aWithinB = (aStart > bStart && aStart < bEnd) || (aEnd > bStart && aEnd < bEnd);
						var bWithinA = (bStart > aStart && bStart < aEnd) || (bEnd > aStart && bEnd < aEnd);

						return aWithinB || bWithinA;
					});

			if (collisions.Any())
			{
				throw new InvalidOperationException("Got character mention collisions in a chapter: " +
				                                    string.Join("; ", collisions.Where(x => x.Count > 1).Select(x => string.Join(" ~ ", x.Select(y => $"{y.Key.Name} (\"{y.individualMatch.Value}\")")))));
			}
		}
	}

	public static class HateGroupBy
	{
		public static IEnumerable<List<T>> GroupByCustom<T>(this IList<T> input, Func<List<T>, T> mostSignificantChooser, Func<(T A, T B), bool> comparer)
		{
			var resultList = new List<List<T>>();

			foreach (var thingy in input)
			{
				var resultToAddTo = resultList.FirstOrDefault(x =>
				{
					var mostSignificant = mostSignificantChooser(x);
					var belongTogether = comparer((thingy, mostSignificant));
					return belongTogether;
				});

				if (resultToAddTo == null)
					resultList.Add(new List<T> {thingy});
				else
					resultToAddTo.Add(thingy);
			}

			return resultList.ToList();
		}
	}
}