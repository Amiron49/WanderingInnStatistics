using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Skill – Quick Growth Lost.]
	/// </summary>
	public class SkillLoss : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SkillLoss);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Skill – (?<skill>[^\]\[]+) (L|l)ost\.\]"),
		};

		public SkillLoss(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["skill"].Value.Singularize(false);
			statistics.SkillLoss.Increment(value);
			return true;
		}
	}
}