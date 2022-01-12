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
				new ClassByPossessiveInference(logger),
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
		public void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var words = content.Split(new[] {' ', '\n', '\r', '"'}, StringSplitOptions.RemoveEmptyEntries);
			statistics.Words = words.Length;

			var characters = words.Sum(x => x.Length);
			statistics.Characters = characters;
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

						var aStart = a.individualMatch.Index;
						var aEnd = aStart + a.individualMatch.Length;
						var bStart = b.individualMatch.Index;
						var bEnd = bStart + b.individualMatch.Length;

						var aWithinB = (aStart > bStart && aStart < bEnd) || (aEnd > bStart && aEnd < bEnd);
						var bWithinA = (bStart > aStart && bStart < aEnd) || (bEnd > aStart && bEnd < aEnd);

						return aWithinB || bWithinA;
					});

			return grouped;	
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

				var orderByDescending = overlapList.OrderByDescending(x => x.individualMatch.Length).ToList();

                var first = orderByDescending.First();
                var second = orderByDescending.Skip(1).First();
                
                if (first.individualMatch.Length == second.individualMatch.Length && first.individualMatch.Value.Replace(" ", "").Length == second.individualMatch.Value.Replace(" ", "").Length)
					throw new Exception("Can't decide");
				
				var mostSignificant = first;

				yield return mostSignificant;
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
				throw new Exception("Got char collisions in a chapter");
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