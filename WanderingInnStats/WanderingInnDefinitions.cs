using System;
using System.Collections.Generic;
using System.Linq;
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

                var conflicts = potentialConflicts.Where(x => definition.ContainsMention(x.NameParts) is not MentionMatch.None or MentionMatch.CommonWordMatch)
                    .ToList();

                if (conflicts.Count > 5)
                    throw new Exception($"{definition.Name} has too many character definition conflicts: {allConflicts.Count}");

                if (conflicts.Any())
                    allConflicts.Add(definition, conflicts);
            }

            if (allConflicts.Count > 8)
                throw new Exception($"We got too many character definition collisions: {allConflicts.Count}");
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