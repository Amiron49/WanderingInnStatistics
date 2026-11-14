using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Humanizer;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    public class WanderingInnDefinitions
    {
        public List<CharacterDefinition> Characters { get; set; } = new();
        public List<string> Skills { get; set; } = new();
        public List<string> Spells { get; set; } = new();
        public List<string> Classes { get; set; } = new();
        public List<string> Unknown { get; set; } = new();

        public List<EntityDefinition> Species { get; set; } = new();
        public List<EntityDefinition> Monsters { get; set; } = new();
        public List<EntityDefinition> Locations { get; set; } = new();

        /// <summary>Class names (singular) from the wiki. They are always classes, whatever the brackets' context suggests.</summary>
        [JsonIgnore]
        public HashSet<string> WikiClasses { get; set; } = new();

        /// <summary>Spell names (singular) from the wiki. They are always spells.</summary>
        [JsonIgnore]
        public HashSet<string> WikiSpells { get; set; } = new();

        /// <summary>
        /// Adds the wiki's ground truth. Classes and spells are seeded into <see cref="Classes"/>/<see cref="Spells"/>
        /// and win over what the brackets' context suggests; species, monsters and locations are counted as mentions.
        /// </summary>
        public void Add(IEnumerable<WikiEntityRaw> entities)
        {
            foreach (var entity in entities)
            {
                switch (entity.Kind)
                {
                    case WikiEntityKind.Class:
                        foreach (var name in new[] { entity.Name }.Concat(entity.Aliases))
                            WikiClasses.Add(name.Singularize(false));
                        break;
                    case WikiEntityKind.Spell:
                        foreach (var name in new[] { entity.Name }.Concat(entity.Aliases))
                            WikiSpells.Add(name.Singularize(false));
                        break;
                    case WikiEntityKind.Species:
                        Species.Add(EntityDefinition.From(entity));
                        break;
                    case WikiEntityKind.Monster:
                        Monsters.Add(EntityDefinition.From(entity));
                        break;
                    case WikiEntityKind.Location:
                        Locations.Add(EntityDefinition.From(entity));
                        break;
                }
            }

            // A name the wiki lists as both stays a spell: class lists sometimes mention spells, the spell list never classes.
            WikiClasses.ExceptWith(WikiSpells);
            ApplyGroundTruth();
        }

        /// <summary>Copies the wiki ground truth (not the inferred names) of <paramref name="other"/>.</summary>
        public void AddGroundTruthOf(WanderingInnDefinitions other)
        {
            WikiClasses.UnionWith(other.WikiClasses);
            WikiSpells.UnionWith(other.WikiSpells);
            Species.AddRange(other.Species);
            Monsters.AddRange(other.Monsters);
            Locations.AddRange(other.Locations);
            ApplyGroundTruth();
        }

        private readonly Dictionary<string, (List<string> List, int Count, HashSet<string> Set)> _lookups = new();
        private readonly object _lookupLock = new();

        public bool IsKnownClass(string name) => Lookup(nameof(Classes), Classes).Contains(name);
        public bool IsKnownSkill(string name) => Lookup(nameof(Skills), Skills).Contains(name);
        public bool IsKnownSpell(string name) => Lookup(nameof(Spells), Spells).Contains(name);

        /// <summary>A set for <paramref name="list"/>, rebuilt when the list was replaced or changed size.</summary>
        private HashSet<string> Lookup(string key, List<string> list)
        {
            lock (_lookupLock)
            {
                if (_lookups.TryGetValue(key, out var cached) && ReferenceEquals(cached.List, list) && cached.Count == list.Count)
                    return cached.Set;
                var set = new HashSet<string>(list);
                _lookups[key] = (list, list.Count, set);
                return set;
            }
        }

        private void ApplyGroundTruth()
        {
            Classes = Classes.Where(x => !WikiSpells.Contains(x)).ToList().Merge(WikiClasses.OrderBy(x => x));
            Spells = Spells.Where(x => !WikiClasses.Contains(x)).ToList().Merge(WikiSpells.OrderBy(x => x));
            Skills = Skills.Where(x => !WikiClasses.Contains(x) && !WikiSpells.Contains(x)).ToList();
            Unknown = Unknown.Where(x => !WikiClasses.Contains(x) && !WikiSpells.Contains(x)).ToList();
        }

        public void Add(IEnumerable<IWanderingInnStatistics> statistics)
        {
            foreach (var statistic in statistics)
                Add(statistic);
        }

        public void Add(IWanderingInnStatistics statistics)
        {
            var classes = Classes
                .Merge(statistics.ClassObtains)
                .Merge(statistics.ClassObtains)
                .Merge(statistics.Classes)
                .Merge(statistics.ClassLevelUps.Select(x => x.Key.Name))
                .Merge(statistics.ClassWithLevels.Select(x => x.Key.Name))
                .Merge(statistics.ClassMorphs.Select(x => x.Key.From))
                .Merge(statistics.ClassMorphs.Select(x => x.Key.To))
                .Merge(statistics.ClassConsolidationRemovals);

            Classes = classes.RemovePlurals();

            var spells = statistics.Skills.Where(x => x.Key.Type == SkillType.Spell).Select(x => x.Key.Name)
                .Except(Classes)
                .ToList().RemovePlurals();

            var morphsToThatAreSpellsBasedOnFrom = statistics.SkillMorphs.Where(morph => spells.Contains(morph.Key.From)).Select(x => x.Key.To);
            var morphsFromThatAreSpellsBasedOnTo = statistics.SkillMorphs.Where(morph => spells.Contains(morph.Key.To)).Select(x => x.Key.From);

            Spells = Spells.RemovePlurals()
                .Merge(spells)
                .Merge(morphsToThatAreSpellsBasedOnFrom)
                .Merge(morphsFromThatAreSpellsBasedOnTo).ToList();

            var skills = statistics.Skills.Where(x => x.Key.Type == SkillType.Skill).Select(x => x.Key.Name)
                .Except(Spells)
                .Except(Classes)
                .ToList();

            Skills = Skills
                .Merge(skills)
                .Merge(statistics.SkillObtains)
                .Except(Classes)
                .Except(Spells).ToList();

            Unknown = Unknown.Merge(statistics.UnknownBrackets)
                .Except(Skills)
                .Except(Spells)
                .Except(Classes).ToList();

            ApplyGroundTruth();
        }

        public void Add(List<CharacterRaw> parsedCharacters)
        {
            var characterDefinitions = parsedCharacters.Select(CreateCharacterDefinition).Distinct().ToList();
            var converted = characterDefinitions.Where(x => x.Name != "Gnoll Geneva").ToList();
            Characters.AddRange(converted);
        }

        public void CheckForCharacterConflicts()
        {
            var potentialConflicts = Characters.ToList();

            var allConflicts = new Dictionary<CharacterDefinition, List<CharacterDefinition>>();

            foreach (var definition in Characters)
            {
                potentialConflicts.Remove(definition);

                var conflicts = potentialConflicts.Where(x => definition.ContainsMention(x.NameParts) is not (MentionMatch.None or MentionMatch.CommonWordMatch))
                    .ToList();

                if (conflicts.Count > 5)
                    throw new Exception($"{definition.Name} has too many character definition conflicts: {conflicts.Count}: {string.Join(", ", conflicts.Select(x => x.Name))}");

                if (conflicts.Any())
                    allConflicts.Add(definition, conflicts);
            }

            if (allConflicts.Count > 8)
                throw new Exception($"We got too many character definition collisions: {allConflicts.Count}: "
                                    + string.Join("; ", allConflicts.Select(x => $"{x.Key.Name} ~ {string.Join(", ", x.Value.Select(y => y.Name))}")));
        }

        public static CharacterDefinition CreateCharacterDefinition(CharacterRaw raw)
        {
            CharacterDefinition characterDefinition = new(raw.WikiUrl, raw.Name)
            {
                Affiliations = raw.Affiliations?.SelectMany(x => x.Value).ToArray() ?? Array.Empty<string>(),
                Age = raw.Age,
                Aliases = raw.Aliases,
                Gender = raw.Gender,
                Occupations = raw.Occupations,
                Residence = raw.Residence,
                Species = raw.Species,
                FamilyMembers = raw.FamilyMembers
            };

            return characterDefinition;
        }
    }
}