using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Humanizer;
using WanderingInnStats.Core;

namespace WanderingInnStats.Parsing
{
    /// <summary>A text that counts as a mention. <see cref="IgnoreFirstLetterCase"/>: also matches "zombies" for "Zombies".</summary>
    public readonly record struct MentionTerm(string Term, bool IgnoreFirstLetterCase);

    /// <summary>A species, monster or location from the wiki whose mentions are counted.</summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class EntityDefinition
    {
        /// <summary>Wiki aliases that are ordinary words ("Scalelings / Children").</summary>
        private static readonly HashSet<string> GenericWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Children", "Child", "People", "Folk", "Ones", "Things", "Beasts", "Monsters", "Spirits", "Creatures", "Animals", "Unconquered"
        };

        /// <summary>Monster names that are common words in lower case, so only the capitalised form counts.</summary>
        private static readonly HashSet<string> CapitalisedOnly = new(StringComparer.OrdinalIgnoreCase)
        {
            "Nightmares", "Nightmare", "Beakers", "Beaker", "Mimics", "Mimic", "Crawlers", "Crawler", "Featherballs", "Featherball"
        };

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WikiEntityKind Kind { get; init; }
        public string Name { get; init; } = "";
        public string[] Aliases { get; init; } = Array.Empty<string>();
        public string? Parent { get; init; }
        public string? WikiUrl { get; init; }

        public static EntityDefinition From(WikiEntityRaw raw) => new()
        {
            Kind = raw.Kind,
            Name = raw.Name,
            Aliases = raw.Aliases,
            Parent = raw.Parent,
            WikiUrl = raw.WikiUrl
        };

        /// <summary>
        /// What counts as a mention: the name and aliases; for species and monsters also in singular and plural.
        /// Monster names also match in lower case ("a pack of wolves"); aliases only as written.
        /// Single-word sub-entries ("Cat" under Beastkin, "Soldier" under Antinium) are too ambiguous and give nothing.
        /// </summary>
        public IEnumerable<MentionTerm> MentionTerms()
        {
            if (Parent != null && !Name.Contains(' ') && !Name.Contains('-'))
                return Array.Empty<MentionTerm>();

            var inflect = Kind is WikiEntityKind.Species or WikiEntityKind.Monster;

            IEnumerable<string> Forms(string x) => inflect ? new[] { x, x.Singularize(false), x.Pluralize(false) } : new[] { x };

            var names = Forms(Name).Select(x => new MentionTerm(x, Kind == WikiEntityKind.Monster && !CapitalisedOnly.Contains(x)));
            var aliases = Aliases
                .Where(x => !(GenericWords.Contains(x.Trim()) && !x.Trim().Contains(' ')))
                .SelectMany(Forms)
                .Select(x => new MentionTerm(x, false));

            return names.Concat(aliases)
                .Select(x => x with { Term = x.Term.Trim() })
                .Where(x => x.Term.Length >= 3 && char.IsUpper(x.Term[0]) && !x.Term.Contains('[') && !x.Term.Contains('\n'))
                .Where(x => !GenericWords.Contains(x.Term))
                .DistinctBy(x => x.Term);
        }
    }
}
