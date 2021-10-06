using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Skill Change – Power Strike → Mirage Cut!]
	/// </summary>
	public class SkillMorphParser : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SkillMorphParser);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Skill Change – (?<skillFrom>[^\]\[]+) → (?<skillTo>[^\]\[]+)!\]"),
		};

		public SkillMorphParser(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var from = match.Groups["skillFrom"].Value.Singularize(false);
			var to = match.Groups["skillTo"].Value.Singularize(false);
			statistics.SkillMorphs.Increment(new SkillMorph(from, to));
			return true;
		}
	}
}