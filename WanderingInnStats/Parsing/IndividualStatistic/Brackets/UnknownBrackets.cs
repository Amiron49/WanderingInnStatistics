using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	public class UnknownBrackets : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(UnknownBrackets);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<content>[^\]\[]+)\]")
		};

		public UnknownBrackets(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var bracketContent = match.Groups["content"].Value.Singularize(false);
			Logger.LogDebug("Unknown Bracket '{bracket}': {context}", bracketContent, match.Context(original));
			statistics.UnknownBrackets.Increment(bracketContent);
			return true;
		}
	}
}