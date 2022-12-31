using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	public class KnownBrackets : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(KnownBrackets);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[(?<content>[^\]\[]+)\]")
		};

		public KnownBrackets(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var withoutBrackets = match.Groups["content"].Value.Singularize(false);

			if (wanderingInnDefinitions.Classes.Contains(withoutBrackets))
			{
				statistics.Classes.Increment(withoutBrackets, hint: "class");
				return true;
			}


			if (wanderingInnDefinitions.Skills.Contains(withoutBrackets))
			{
				statistics.Skills.Increment(new Skill(withoutBrackets, SkillType.Skill));
				return true;
			}

			if (wanderingInnDefinitions.Spells.Contains(withoutBrackets))
			{
				statistics.Skills.Increment(new Skill(withoutBrackets, SkillType.Spell));
				return true;
			}

			return false;
		}
	}
}