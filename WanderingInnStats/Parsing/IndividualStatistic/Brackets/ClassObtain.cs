using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Innkeeper Class Obtained!]
	/// </summary>
	public class ClassObtain : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassObtain);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<class>[^\]\[]+) Class Obtained!\]"),
		};

		public ClassObtain(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var @class = match.Groups["class"].Value.Singularize(false);
			statistics.ClassObtains.Increment(@class);
			return true;
		}
	}
}