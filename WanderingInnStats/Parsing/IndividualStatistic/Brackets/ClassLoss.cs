using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Class Conditions: Princess failed]
	/// [Class – Princess lost.]
	/// </summary>
	public class ClassLoss : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassLoss);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Class Conditions: (?<class>[^\]\[]+) failed\]"),
			new(@"\[Class – (?<class>[^\]\[]+) lost.\]"),
		};

		public ClassLoss(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var className = match.Groups["class"].Value.Singularize(false);
			statistics.Classes.Increment(className, hint: "class");
			return true;
		}
	}
}