using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WanderingInnStats.Cli
{
    public static class Program
    {
        public static async Task Main()
        {
            const string tempRoot = @"W:\temp\wanderingInn\";

            if (!Directory.Exists(tempRoot))
                throw new Exception($"Temp folder '{tempRoot}' does not exist");

            string rawChaptersCacheFile = Path.Combine(tempRoot, "dump.json");
            string rawCharactersCacheFile = Path.Combine(tempRoot, "characters-raw.json");
            string frontEndDefinitions = Path.Combine(tempRoot, "definitions-frontend.json");
            string frontEndStatistics = Path.Combine(tempRoot, "statistics-frontend.json");

            await Serialise(Path.GetTempFileName(), new WanderingInnStatistics());
            await Serialise(Path.GetTempFileName(), new FullStatistics(new List<VolumeStatistics>()));


            var chapters = await ScrappingHelper.ScrapeBlog(rawChaptersCacheFile);
            chapters = chapters.Where(x => x.Name != "Glossary").ToList();

            var characters = await ScrappingHelper.ScrapeWikiForCharacters(rawCharactersCacheFile);
            characters = characters.Where(x => !x.Name.Contains("Chapter 8.17 H") && !x.Name.Contains("Gnoll Geneva")).ToList();

            CharacterDefinitionSanityCheck(characters);
            DirtyChapterFixing(chapters);

            var inferredDefinition = await InferDefinitions(chapters, characters);

            var msLogger = CreateLogger();
            var statisticsParser = new ChapterStatisticsParser(msLogger, inferredDefinition);
            var finalChapterStatistics = await statisticsParser.Create(chapters);
            var groupedByVolume = finalChapterStatistics.GroupBy(x => x.Chapter.Volume);

            var asVolumeStatistics = groupedByVolume.Select(volumeGroup =>
            {
                var chapterStatistics = volumeGroup.Select(chapterStatistic =>
                {
                    var (chapter, statistics) = chapterStatistic;
                    var converted = new ChapterStatistics(chapter, statistics);
                    return converted;
                }).ToList();
                return new VolumeStatistics(volumeGroup.Key, chapterStatistics);
            }).ToList();

            var wanderingInnStatistics = new FullStatistics(asVolumeStatistics);

            var finalDefinitions = new WanderingInnDefinitions();
            finalDefinitions.Add(wanderingInnStatistics);
            finalDefinitions.Characters.AddRange(inferredDefinition.Characters);

            Log.Information("Unknowns: {unknown}", finalDefinitions.Unknown.Count);
            Console.WriteLine("Done");
            Console.WriteLine("Exporting final jsons");

            //Can't ba arsed 
            foreach (var chapter in wanderingInnStatistics.Volumes.SelectMany(x => x.Chapters).Select(x => x.Chapter))
                chapter.Text = "";

            await Serialise(frontEndStatistics, wanderingInnStatistics);
            await Serialise(frontEndDefinitions, finalDefinitions);

            Log.CloseAndFlush();
            Console.ReadKey();
        }

        private static async Task<WanderingInnDefinitions> InferDefinitions(List<Chapter> chapters, List<CharacterRaw> characters)
        {
            Console.WriteLine("Starting: First Pass");
            var wanderingInnDefinitions = new WanderingInnDefinitions();
            var firstPassParser = new ChapterStatisticsParser(NullLogger.Instance, wanderingInnDefinitions);
            var firstPass = await firstPassParser.Create(chapters);

            var inferredDefinition = new WanderingInnDefinitions();
            inferredDefinition.Add(firstPass.Select(x => x.Statistics));
            inferredDefinition.Add(characters);
            return inferredDefinition;
        }

        private static void DirtyChapterFixing(List<Chapter> chapters)
        {
            var dirty = ChapterFixing.FindDirtyChapters(chapters);
            Console.WriteLine($"Found {dirty.Count} dirty chapters");
            ChapterFixing.Fixup(chapters);
            dirty = ChapterFixing.FindDirtyChapters(chapters).Where(x => x.Volume != "Volume 8\n").ToList();
            Console.WriteLine($"{dirty.Count} dirty chapters remaining");
            if (dirty.Count > 0)
                throw new Exception("Still have dirty chapters");
        }

        private static void CharacterDefinitionSanityCheck(List<CharacterRaw> characters)
        {
            Console.WriteLine("Character Sanity Check");
            var sanityDefinition = new WanderingInnDefinitions();
            sanityDefinition.Add(characters);
            sanityDefinition.CheckForCharacterConflicts();
            Console.WriteLine("Character Sanity Ok");
        }

        private static ILogger CreateLogger()
        {
            if (File.Exists(@"W:\temp\wanderingInn\log.log"))
                File.Delete(@"W:\temp\wanderingInn\log.log");

            var loggerFactory = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(@"W:\temp\wanderingInn\log.log");

            Log.Logger = loggerFactory.CreateLogger();
            var msLogger = new SerilogLoggerFactory().CreateLogger("Lel");
            return msLogger;
        }

        private static async Task Serialise<T>(string path, T thing)
        {
            Console.WriteLine("Writing Json");
            await using var file = new FileStream(path, FileMode.Create);
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
            await JsonSerializer.SerializeAsync(file, thing, jsonSerializerOptions);
        }
    }

    public class CustomDictionaryConverter<T> : JsonConverter<Dictionary<T, int>> where T : IJsonMe
    {
        public override Dictionary<T, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<T, int> value, JsonSerializerOptions options)
        {
            if (!typeof(IJsonMe).IsAssignableFrom(value.GetType().GenericTypeArguments[0]))
                throw new Exception("lmao");

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
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, TWanderingInnStatistics value, JsonSerializerOptions options)
        {
            var asDumboDictionary = new Dictionary<string, object>()
            {
                {nameof(IWanderingInnStatistics.Words), value.Words},
                {nameof(IWanderingInnStatistics.Characters), value.Characters}
            };

            asDumboDictionary.AddIfNotNull(value, statistics => statistics.Classes);
            asDumboDictionary.AddIfNotNull(value, statistics => statistics.Skills);
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