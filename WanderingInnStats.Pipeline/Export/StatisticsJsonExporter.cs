using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using WanderingInnStats.Parsing;

namespace WanderingInnStats.Pipeline.Export
{
    public static class StatisticsJsonExporter
    {
        public static JsonSerializerOptions CreateOptions()
        {
            var jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new CustomDictionaryConverter<Skill>());
            jsonSerializerOptions.Converters.Add(new CustomDictionaryConverter<CharacterDefinition>());
            jsonSerializerOptions.Converters.Add(new CustomDictionaryConverter<ClassWithLevel>());
            jsonSerializerOptions.Converters.Add(new CustomDictionaryConverter<SkillMorph>());
            jsonSerializerOptions.Converters.Add(new CustomDictionaryConverter<ClassMorph>());
            jsonSerializerOptions.Converters.Add(new CustomConverter<ChapterStatistics>());
            jsonSerializerOptions.Converters.Add(new CustomConverter<VolumeStatistics>());
            jsonSerializerOptions.Converters.Add(new CustomConverter<FullStatistics>());
            jsonSerializerOptions.Converters.Add(new CustomConverter<WanderingInnStatistics>());
            return jsonSerializerOptions;
        }

        public static async Task Serialise<T>(string path, T thing, CancellationToken cancellationToken = default)
        {
            await using var file = new FileStream(path, FileMode.Create);
            await JsonSerializer.SerializeAsync(file, thing, CreateOptions(), cancellationToken);
        }

        /// <summary>The frontend does not need the chapter texts; blank them before export.</summary>
        public static void StripChapterTexts(FullStatistics statistics)
        {
            foreach (var chapter in statistics.Volumes.SelectMany(x => x.Chapters).Select(x => x.Chapter))
                chapter.Text = "";
        }
    }

    public class CustomDictionaryConverter<T> : JsonConverter<Dictionary<T, int>> where T : IJsonMe
    {
        public override Dictionary<T, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException($"{GetType().Name} only writes statistics JSON; reading it back isn't supported.");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<T, int> value, JsonSerializerOptions options)
        {
            if (!typeof(IJsonMe).IsAssignableFrom(value.GetType().GenericTypeArguments[0]))
                throw new InvalidOperationException($"Can't export {value.GetType()}: its key type doesn't implement {nameof(IJsonMe)}.");

            var keys = value.Select(x => (x.Key.JsonEquivalent, x));
            var duplicates = keys.GroupBy(x => x.JsonEquivalent).Where(x => x.Count() > 1).ToList();


            //TODO find a solution
            var converted = value.Where(x => duplicates.All(dup => dup.Key != x.Key.JsonEquivalent)).ToDictionary(x => x.Key.JsonEquivalent, x => x.Value);

            var serialize = JsonSerializer.Serialize(converted, options);
            JsonDocument.Parse(serialize).WriteTo(writer);
        }
    }

    public class CustomConverter<TWanderingInnStatistics> : JsonConverter<TWanderingInnStatistics> where TWanderingInnStatistics : IWanderingInnStatistics
    {
        public override TWanderingInnStatistics? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException($"{GetType().Name} only writes statistics JSON; reading it back isn't supported.");
        }

        public override void Write(Utf8JsonWriter writer, TWanderingInnStatistics value, JsonSerializerOptions options)
        {
            var asDumboDictionary = new Dictionary<string, object>()
            {
                {nameof(IWanderingInnStatistics.Words), value.Words},
                {nameof(IWanderingInnStatistics.Characters), value.Characters}
            };


            asDumboDictionary.AddIfNotNull(value, statistics => statistics.Classes);

            if (value.SkillsSimple.Any())
            {
                asDumboDictionary.Add("Skills", value.SkillsSimple);
            }

            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SkillsSkills);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SkillsSpells);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.CancelledBrackets);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.CharacterMentions);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.ClassObtains);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.ClassMorphs);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SkillLoss);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SkillMorphs);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SkillObtains);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SpellObtains);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.UnknownBrackets);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.ClassConsolidationRemovals);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.ClassLevelUps);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.OtherWordsUsage);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.SpeciesMentions);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.MonsterMentions);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.LocationMentions);

            switch (value)
            {
                case VolumeStatistics asVolume:
                    asDumboDictionary.Add(nameof(VolumeStatistics.Name), asVolume.Name);
                    asDumboDictionary.Add(nameof(VolumeStatistics.Chapters), asVolume.Chapters);
                    break;
                case ChapterStatistics asChapter:
                    asDumboDictionary.Add(nameof(ChapterStatistics.Chapter), asChapter.Chapter);
                    break;
                case FullStatistics asFull:
                    asDumboDictionary.Add(nameof(FullStatistics.Volumes), asFull.Volumes);
                    break;
            }

            var serialize = JsonSerializer.Serialize(asDumboDictionary, options);
            JsonDocument.Parse(serialize).WriteTo(writer);
        }
    }

    public static class HateJsons
    {
        public static void AddIfNotNull<T>(this Dictionary<string, object> thingy, IWanderingInnStatistics value,
            Expression<Func<IWanderingInnStatistics, Dictionary<T, int>>> target) where T : notnull
        {
            var dictionary = target.Compile()(value);

            if (!dictionary.Any())
                return;

            var propertyName = ((MemberExpression) target.Body).Member.Name;

            thingy.Add(propertyName, dictionary);
        }
    }
}
