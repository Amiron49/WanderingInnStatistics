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
			var racesGigaString = "((" + string.Join(")|(", RaceHelper.Races.Select(Regex.Escape)) + "))";

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
        /// <summary>The species known before the wiki was used as ground truth; always part of <see cref="Races"/>.</summary>
        public static readonly IReadOnlyList<string> BaseRaces = new[]
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

        private static readonly object Lock = new();
        private static string[] _races = BaseRaces.ToArray();

        /// <summary>Singular species names, used to recognise "Drake [Guardsman]" and as words that don't identify a character.</summary>
        public static string[] Races
        {
            get
            {
                lock (Lock)
                    return _races;
            }
        }

        /// <summary>Bumped by <see cref="Configure"/>, so caches built from <see cref="Races"/> know to rebuild.</summary>
        public static int Version { get; private set; }

        /// <summary>
        /// Sets <see cref="Races"/> to <see cref="BaseRaces"/> plus the singular of each single-word, capitalised name in
        /// <paramref name="wikiSpecies"/>. Call before creating parsers.
        /// </summary>
        public static void Configure(IEnumerable<string> wikiSpecies)
        {
            var extra = wikiSpecies
                .Select(x => x.Trim())
                .Where(x => x.Length >= 3 && char.IsUpper(x[0]) && !x.Contains(' '))
                .Select(x => x.Singularize(false))
                .Where(x => x.Length >= 3);

            var races = BaseRaces.Concat(extra).Distinct().ToArray();
            lock (Lock)
            {
                if (races.SequenceEqual(_races))
                    return;
                _races = races;
                Version++;
            }
        }
    }
}