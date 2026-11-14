using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using WanderingInnStats.Core;

namespace WanderingInnStats.Parsing
{
    /// <summary>
    /// Counts mentions of the wiki's species, monsters and locations. Each kind is matched with a single regex of all its
    /// terms, longest first, so "Dire Wolves" counts once and not also as "Wolves". Text inside [brackets] is ignored.
    /// </summary>
    public class EntityMentionStats : ITextParser
    {
        private static readonly Regex BracketsRegex = new(@"\[[^\]\[]*\]", RegexOptions.Compiled);
        private static readonly ConditionalWeakTable<WanderingInnDefinitions, Matchers> Cache = new();

        public void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
        {
            var matchers = Cache.GetValue(wanderingInnDefinitions, d => new Matchers(d));
            if (matchers.IsEmpty)
                return;

            var text = BracketsRegex.Replace(content, " ");
            matchers.Species?.Count(text, statistics.SpeciesMentions);
            matchers.Monsters?.Count(text, statistics.MonsterMentions);
            matchers.Locations?.Count(text, statistics.LocationMentions);
        }

        private sealed class Matchers
        {
            public Matchers(WanderingInnDefinitions definitions)
            {
                var characterNames = definitions.Characters.Select(x => x.Name).ToHashSet();

                Species = Matcher.Create(definitions.Species, characterNames);
                Monsters = Matcher.Create(definitions.Monsters, characterNames);
                Locations = Matcher.Create(definitions.Locations, characterNames);
            }

            public Matcher? Species { get; }
            public Matcher? Monsters { get; }
            public Matcher? Locations { get; }
            public bool IsEmpty => Species == null && Monsters == null && Locations == null;
        }

        internal sealed class Matcher
        {
            private readonly Regex _regex;
            private readonly Dictionary<string, string> _termToName;
            private readonly HashSet<string> _foldable;

            private Matcher(Regex regex, Dictionary<string, string> termToName, HashSet<string> foldable)
            {
                _regex = regex;
                _termToName = termToName;
                _foldable = foldable;
            }

            /// <param name="excludedTerms">Terms that are character names; those mentions belong to the character.</param>
            public static Matcher? Create(IEnumerable<EntityDefinition> definitions, ISet<string> excludedTerms)
            {
                var termToName = new Dictionary<string, string>();
                var foldable = new HashSet<string>();
                foreach (var definition in definitions)
                foreach (var term in definition.MentionTerms())
                {
                    if (excludedTerms.Contains(term.Term) || !termToName.TryAdd(term.Term, definition.Name))
                        continue;
                    if (term.IgnoreFirstLetterCase)
                        foldable.Add(term.Term);
                }

                if (termToName.Count == 0)
                    return null;

                var alternatives = termToName.Keys
                    .OrderByDescending(x => x.Length)
                    .Select(term => foldable.Contains(term)
                        ? $"[{char.ToUpperInvariant(term[0])}{char.ToLowerInvariant(term[0])}]" + Regex.Escape(term.Substring(1))
                        : Regex.Escape(term));

                var regex = new Regex($@"(?<![\w'’-])(?:{string.Join("|", alternatives)})(?![\w-])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
                return new Matcher(regex, termToName, foldable);
            }

            public void Count(string text, Dictionary<string, int> target)
            {
                foreach (Match match in _regex.Matches(text))
                {
                    var term = match.Value;
                    if (!_termToName.TryGetValue(term, out var name))
                    {
                        var capitalised = char.ToUpperInvariant(term[0]) + term.Substring(1);
                        if (!_foldable.Contains(capitalised) || !_termToName.TryGetValue(capitalised, out name))
                            continue;
                    }

                    target[name] = target.TryGetValue(name, out var count) ? count + 1 : 1;
                }
            }
        }
    }
}
