using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [something something Learne—
	/// [something something…
	/// [s—
	/// </summary>
	public class BrokenBrackets : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(BrokenBrackets);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<content>[^…—\]\[]+(—|…))")
		};

		public BrokenBrackets(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["content"].Value;
			statistics.CancelledBrackets.Increment(value);
			return true;
		}
	}
}