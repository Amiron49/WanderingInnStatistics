using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// a Level 27 [Pirate]
	/// </summary>
	public class ClassWithLevelMention : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassWithLevelMention);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"Level (?<level>\d+) \[(?<class>[^\]\[]+)\]")
		};

		public ClassWithLevelMention(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var className = match.Groups["class"].Value;
			var level = int.Parse(match.Groups["level"].Value.Singularize(false));
			
			statistics.ClassWithLevels.Increment(new ClassWithLevel(className, level));

			return true;
		}
	}
}