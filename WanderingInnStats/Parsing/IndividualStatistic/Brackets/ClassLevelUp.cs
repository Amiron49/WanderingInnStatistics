using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Innkeeper Level 1!]
	/// </summary>
	public class ClassLevelUp : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassLevelUp);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<class>[^\]\[]+) Level (?<level>\d+)!\]"),
			new(@"\[Level (?<level>\d+) (?<class>[^\]\[]+)!\]"),
		};

		public ClassLevelUp(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var className = match.Groups["class"].Value.Singularize(false);
			var level = int.Parse(match.Groups["level"].Value);
			
			statistics.ClassLevelUps.Increment(new ClassWithLevel(className, level));

			return true;
		}
	}
}