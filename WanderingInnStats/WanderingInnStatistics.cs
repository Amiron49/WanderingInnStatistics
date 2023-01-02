using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;

namespace WanderingInnStats
{
    public interface IWanderingInnStatistics
    {
        long Words { get; }
        long Characters { get; }
        Dictionary<CharacterDefinition, int> CharacterMentions { get; }
        Dictionary<string, int> UnknownBrackets { get; }
        Dictionary<string, int> CancelledBrackets { get; }
        Dictionary<string, int> ClassObtains { get; }
        Dictionary<ClassMorph, int> ClassMorphs { get; }
        Dictionary<SkillMorph, int> SkillMorphs { get; }
        Dictionary<string, int> ClassConsolidationRemovals { get; }
        Dictionary<string, int> Classes { get; }
        Dictionary<ClassWithLevel, int> ClassWithLevels { get; }
        Dictionary<ClassWithLevel, int> ClassLevelUps { get; }
        Dictionary<string, int> SkillObtains { get; }
        Dictionary<string, int> SpellObtains { get; }
        Dictionary<string, int> SkillLoss { get; }
        Dictionary<Skill, int> Skills { get; }
        Dictionary<string, int> SkillsSimple { get; }
        Dictionary<string, int> SkillsSkills { get; }
        Dictionary<string, int> SkillsSpells { get; }
        Dictionary<string, int> OtherWordsUsage { get; }
    }

    public abstract class AccumulatingStatistics : IWanderingInnStatistics
    {
        public long Words => Children.Sum(x => x.Words);
        public long Characters => Children.Sum(x => x.Characters);
        public Dictionary<CharacterDefinition, int> CharacterMentions => Children.Select(x => x.CharacterMentions).Accumulate();
        public Dictionary<string, int> UnknownBrackets => Children.Select(x => x.UnknownBrackets).Accumulate();
        public Dictionary<string, int> CancelledBrackets => Children.Select(x => x.CancelledBrackets).Accumulate();
        public Dictionary<string, int> ClassObtains => Children.Select(x => x.ClassObtains).Accumulate();
        public Dictionary<ClassMorph, int> ClassMorphs => Children.Select(x => x.ClassMorphs).Accumulate();
        public Dictionary<SkillMorph, int> SkillMorphs => Children.Select(x => x.SkillMorphs).Accumulate();
        public Dictionary<string, int> ClassConsolidationRemovals => Children.Select(x => x.ClassConsolidationRemovals).Accumulate();
        public Dictionary<string, int> Classes => Children.Select(x => x.Classes).Accumulate();
        public Dictionary<ClassWithLevel, int> ClassWithLevels => Children.Select(x => x.ClassWithLevels).Accumulate();
        public Dictionary<ClassWithLevel, int> ClassLevelUps => Children.Select(x => x.ClassLevelUps).Accumulate();
        public Dictionary<string, int> SkillObtains => Children.Select(x => x.SkillObtains).Accumulate();
        public Dictionary<string, int> SpellObtains => Children.Select(x => x.SpellObtains).Accumulate();
        public Dictionary<string, int> SkillLoss => Children.Select(x => x.SkillLoss).Accumulate();
        public Dictionary<Skill, int> Skills => Children.Select(x => x.Skills).Accumulate();

        public Dictionary<string, int> SkillsSimple
        {
            get
            {
                var skillsGrouped = Skills.GroupBy(x => x.Key.Name)
                    .Select(grouping => grouping.Aggregate(new KeyValuePair<string, int>("", 0), (a, b) => new KeyValuePair<string, int>(b.Key.Name, a.Value + b.Value)));

                return skillsGrouped.ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public Dictionary<string, int> SkillsSkills => Skills.Where(x => x.Key.Type == SkillType.Skill).ToDictionary(pair => pair.Key.Name, pair => pair.Value);
        public Dictionary<string, int> SkillsSpells => Skills.Where(x => x.Key.Type == SkillType.Spell).ToDictionary(pair => pair.Key.Name, pair => pair.Value);
        public Dictionary<string, int> OtherWordsUsage => Children.Select(x => x.OtherWordsUsage).Accumulate();

        protected abstract IEnumerable<IWanderingInnStatistics> Children { get; }
    }

    public class FullStatistics : AccumulatingStatistics
    {
        public List<VolumeStatistics> Volumes { get; set; }
        protected override IEnumerable<IWanderingInnStatistics> Children => Volumes;

        public FullStatistics(List<VolumeStatistics> volumes)
        {
            Volumes = volumes;
        }
    }

    public class VolumeStatistics : AccumulatingStatistics
    {
        public string Name { get; }
        public List<ChapterStatistics> Chapters { get; }
        protected override IEnumerable<IWanderingInnStatistics> Children => Chapters;

        public VolumeStatistics(string name, List<ChapterStatistics> chapters)
        {
            Name = name;
            Chapters = chapters;
        }
    }

    public class ChapterStatistics : WanderingInnStatistics
    {
        public Chapter Chapter { get; }

        public ChapterStatistics(Chapter chapter, IWanderingInnStatistics other)
        {
            Chapter = chapter;
            Characters = other.Characters;
            CharacterMentions = other.CharacterMentions;
            Classes = other.Classes;
            CancelledBrackets = other.CancelledBrackets;
            ClassObtains = other.ClassObtains;
            ClassMorphs = other.ClassMorphs;
            SkillMorphs = other.SkillMorphs;
            ClassConsolidationRemovals = other.ClassConsolidationRemovals;
            ClassLevelUps = other.ClassLevelUps;
            SkillObtains = other.SkillObtains;
            SpellObtains = other.SpellObtains;
            SkillLoss = other.SkillLoss;
            Skills = other.Skills;
            OtherWordsUsage = other.OtherWordsUsage;
            UnknownBrackets = other.UnknownBrackets;
            ClassWithLevels = other.ClassWithLevels;
            Words = other.Words;
        }
    }

    public class WanderingInnStatistics : IWanderingInnStatistics
    {
        public long Words { get; set; }
        public long Characters { get; set; }
        public Dictionary<CharacterDefinition, int> CharacterMentions { get; protected init; } = new();
        public Dictionary<string, int> UnknownBrackets { get; protected init; } = new();
        public Dictionary<string, int> CancelledBrackets { get; protected init; } = new();
        public Dictionary<string, int> ClassObtains { get; protected init; } = new();
        public Dictionary<ClassMorph, int> ClassMorphs { get; protected init; } = new();
        public Dictionary<SkillMorph, int> SkillMorphs { get; protected init; } = new();
        public Dictionary<string, int> ClassConsolidationRemovals { get; protected init; } = new();
        public Dictionary<string, int> Classes { get; protected init; } = new();
        public Dictionary<ClassWithLevel, int> ClassWithLevels { get; protected init; } = new();
        public Dictionary<ClassWithLevel, int> ClassLevelUps { get; protected init; } = new();

        public Dictionary<string, int> SkillObtains { get; protected init; } = new();
        public Dictionary<string, int> SpellObtains { get; protected init; } = new();
        public Dictionary<string, int> SkillLoss { get; protected init; } = new();
        public Dictionary<Skill, int> Skills { get; protected init; } = new();
        public Dictionary<string, int> SkillsSimple
        {
            get
            {
                var skillsGrouped = Skills.GroupBy(x => x.Key.Name)
                    .Select(grouping => grouping.Aggregate(new KeyValuePair<string, int>("", 0), (a, b) => new KeyValuePair<string, int>(b.Key.Name, a.Value + b.Value)));

                return skillsGrouped.ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public Dictionary<string, int> SkillsSkills => Skills.Where(x => x.Key.Type == SkillType.Skill).ToDictionary(pair => pair.Key.Name, pair => pair.Value);
        public Dictionary<string, int> SkillsSpells => Skills.Where(x => x.Key.Type == SkillType.Spell).ToDictionary(pair => pair.Key.Name, pair => pair.Value);
        public Dictionary<string, int> OtherWordsUsage { get; protected init; } = new();
    }
}