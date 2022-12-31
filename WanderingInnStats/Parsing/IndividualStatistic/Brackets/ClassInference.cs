using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing.IndividualStatistic.Brackets
{
	/// <summary>
	/// [Innkeeper] class
	/// </summary>
	public class ClassInference : AbstractDestructiveRegexParser
	{
		protected override string Name => nameof(ClassInference);
		protected override IEnumerable<Regex> Regexes { get; }

        public ClassInference(ILogger logger) : base(logger)
		{
			var racesGigaString = "((" + string.Join(")|(", RaceHelper.Races) + "))";

			Regexes = new Regex[]
			{
				new(@"\[(?<class>[^\]\[]+)\] class"),
				new(@"(hire|become) an? \[(?<class>[^\]\[]+)\]"),
				new(@"(H|h)e(’s|\swas) (an?|no) \[(?<class>[^\]\[]+)\]"),
				new(@"I (was|am) (an?|no) \[(?<class>[^\]\[]+)\]"),
				new(@"((expert)|(experienced)) \[(?<class>[^\]\[]+)\]"),
				new(@$"{racesGigaString} \[(?<class>[^\]\[]+)\]", RegexOptions.Compiled),
				new(@"\[(?<class>[^\]\[]+)\] (stared|hesitated)"),
			};

		}

		protected override bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var className = match.Groups["class"].Value.Singularize(false);
			statistics.Classes.Increment(className, hint: "class");
			
			return true;
		}
	}

    public static class RaceHelper
    {
        public static string[] Races =
        {
            "Antinium",
            "Beastkin",
            "Centaur",
            "Demon",
            "Djinn",
            "Dragon",
            "Drake",
            "Dullahan",
            "Dwarf",
            "Elf",
            "Garuda",
            "Gazer",
            "Gnoll",
            "Goblin",
            "Harpy",
            "Halfling",
            "Human",
            "Jinn",
            "Minotaur",
            "Naga",
            "Oldblood",
            "Rashkghar",
            "Scorchling",
            "Selphid",
            "Troll",
            "Vampire",
            "male"
        };
    }
}