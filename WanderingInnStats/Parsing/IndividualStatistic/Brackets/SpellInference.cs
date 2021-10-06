using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Something] spell
	/// </summary>
	public class SpellInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SpellInference);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<spell>[^\]\[]+)\] spell"),
			new(@"(C|c)ast \[(?<spell>[^\]\[]+)\]"),
			new(@"scroll of \[(?<spell>[^\]\[]+)\]"),
			new(@"\[(?<spell>[^\]\[]+)\] scroll")
		};

		public SpellInference(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["spell"].Value.Singularize(false);
			statistics.Skills.Increment(new Skill(value, SkillType.Spell));
			return true;
		}
	}
}