using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Spell – Burning Blades obtained!]
	/// </summary>
	public class SpellObtain : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(SpellObtain);

		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Spell(( –)|(:))\s{1,2}(?<skill>[^\]\[]+) (O|o)btained(!|\.)\]"),
		};

		public SpellObtain(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var value = match.Groups["skill"].Value.Singularize(false);
			statistics.SpellObtains.Increment(value);
			return true;
		}
	}
}