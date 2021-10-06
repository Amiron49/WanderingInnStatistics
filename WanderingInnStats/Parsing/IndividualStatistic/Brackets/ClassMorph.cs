using System.Collections.Generic;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// Conditions Met: Innkeeper → Awesome Innkeeper Class!
	/// Conditions Met: Leader → Chieftain Class!
	/// </summary>
	public class ClassMorphParser : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassMorphParser);
		protected override IEnumerable<Regex> Regexes { get; } = new Regex[]
		{
			new(@"\[Conditions Met:\s(?<classFrom>[^\]\[]+) → (?<classTo>[^\]\[]+) Class!\]"),
			new(@"\[(?<classFrom>[^\]\[]+) → (?<classTo>[^\]\[]+) (C|c)lass!\]"),
		};

		public ClassMorphParser(ILogger logger) : base(logger)
		{
		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var classFrom = match.Groups["classFrom"].Value.Singularize(false);
			var classTo = match.Groups["classTo"].Value.Singularize(false);
			statistics.ClassMorphs.Increment(new ClassMorph(classFrom, classTo));
			return true;
		}
	}
}