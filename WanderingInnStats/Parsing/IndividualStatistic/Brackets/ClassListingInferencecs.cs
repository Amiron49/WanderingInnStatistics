using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// classes such as [Stalwart], [Defender], [Defender2] and [Lineholder]
	/// classes like [Stalwart] and [Defender]
	/// </summary>
	public class ClassListingInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassListingInference);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"classes such as( (and )?\[[^\]\[]+\],?)+"),
			new(@"classes like( (and )?\[[^\]\[]+\],?)+"),
		};

		public ClassListingInference(ILogger logger) : base(logger)
		{
		}

		private Regex _internal = new(@"\[(?<content>[^\]\[]+)\]");
		
		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var matches = _internal.Matches(match.Value);

			foreach (var bracket in matches.ToList())
			{
				var @class = bracket.Groups["content"].Value.Singularize(false);
				statistics.Classes.Increment(@class);
			}
			
			return true;
		}
	}
}