using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// skills such as [lel], [lel], [lel] and [lel]
	/// skills like [lel] and [lel]
	/// </summary>
	public class SkillListingInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SkillListingInference);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"(S|s)kills such as( (and )?\[[^\]\[]+\],?)+"),
			new(@"(S|s)kills like( (and )?\[[^\]\[]+\],?)+"),
		};

		public SkillListingInference(ILogger logger) : base(logger)
		{
		}

		private Regex _internal = new(@"\[(?<content>[^\]\[]+)\]");
		
		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var matches = _internal.Matches(match.Value);

			foreach (var bracket in matches.ToList())
			{
				var skill = bracket.Groups["content"].Value.Singularize(false);
				statistics.Skills.Increment(new Skill(skill, SkillType.Skill));
			}
			
			return true;
		}
	}
}