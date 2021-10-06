using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Innkeeper]'s lel
	/// </summary>
	public class ClassByPossessiveInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassByPossessiveInference);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<class>[^\]\[]+)\]’s"),
		};

		public ClassByPossessiveInference(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var @class = match.Groups["class"].Value.Singularize(false);
			statistics.Classes.Increment(@class);
			return true;
		}
	}
}