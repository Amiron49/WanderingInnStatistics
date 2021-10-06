using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Class Consolidation: Scavenger removed.]
	/// [Class Consolidation: Tinkerer removed.]
	/// </summary>
	public class ClassConsolidationRemoval : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassConsolidationRemoval);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Class Consolidation: (?<class>[^\]\[]+) removed\.\]"),
		};

		public ClassConsolidationRemoval(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var @class = match.Groups["class"].Value.Singularize(false);
			statistics.ClassConsolidationRemovals.Increment(@class);
			return true;
		}
	}
}