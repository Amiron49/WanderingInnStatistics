using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Skill – Basic Cleaning obtained!]
	/// [Skill – Basic Cleaning Learned.]
	/// </summary>
	public class SkillObtain : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SkillObtain);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Skill(( –)|(:))\s{1,2}(?<skill>[^\]\[]+) (O|o)btained(!|\.)\]"),
			new(@"\[Skill –\s{1,2}(?<skill>[^\]\[]+) Learned\.\]")
		};

		public SkillObtain(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["skill"].Value.Singularize(false);
			statistics.SkillObtains.Increment(value);
			return true;
		}
	}
}