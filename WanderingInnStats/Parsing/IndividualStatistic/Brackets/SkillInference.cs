using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Something] skill
	/// using [Something]
	/// learned [Something]
	/// </summary>
	public class SkillInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SkillInference);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<skill>[^\]\[]+)\] (s|S)kill"),
			new(@"(s|S)kill(\s|—)\[(?<skill>[^\]\[]+)\]"),
			new(@"(s|S)kill(s?) is \[(?<skill>[^\]\[]+)\]"),
			new(@"(s|S)kill(s?) like \[(?<skill>[^\]\[]+)\]"),
			new(@"learned \[(?<skill>[^\]\[]+)\]"),
			new(@"learn \[(?<skill>[^\]\[]+)\]"),
			new(@"learning \[(?<skill>[^\]\[]+)\]"),
			new(@"using \[(?<skill>[^\]\[]+)\]")
		};

		public SkillInference(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["skill"].Value.Singularize(false);
			statistics.Skills.Increment(new Skill(value, SkillType.Skill));

			if (match.Groups.ContainsKey("skill2"))
			{
				var value2 = match.Groups["skill2"].Value.Singularize(false);
				statistics.Skills.Increment(new Skill(value, SkillType.Skill));
			}
			
			return true;
		}
	}
}