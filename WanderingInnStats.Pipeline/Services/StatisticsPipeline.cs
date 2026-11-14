using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using Humanizer;
using WanderingInnStats.Parsing;
using WanderingInnStats.Parsing.IndividualStatistic.Brackets;
using WanderingInnStats.Pipeline.Data;
using WanderingInnStats.Pipeline.Export;

namespace WanderingInnStats.Pipeline.Services;

public enum PipelineStage
{
    Loading,
    CharacterSanityCheck,
    ChapterFixing,
    FirstPass,
    SecondPass,
    Grouping,
    Exporting,
    Done
}

public record PipelineResult(
    int RunId,
    int ChapterCount,
    int CharacterCount,
    int UnknownCount,
    string StatisticsFile,
    string DefinitionsFile,
    IReadOnlyList<string> CopiedTo);

public class StatisticsPipeline
{
    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly WikiService _wikiService;

    public StatisticsPipeline(WorkspaceDbFactory dbFactory, WorkspaceOptions options, WikiService wikiService)
    {
        _dbFactory = dbFactory;
        _options = options;
        _wikiService = wikiService;
    }

    /// <summary>Definitions inferred by the most recent successful run, used for accurate per-chapter previews.</summary>
    public WanderingInnDefinitions? LastInferredDefinitions { get; private set; }

    public static IReadOnlyList<PipelineStage> Stages { get; } = Enum.GetValues<PipelineStage>().Where(x => x != PipelineStage.Done).ToList();

    /// <param name="logger">Receives the parser's per-bracket debug output, as the old CLI's Serilog logger did.</param>
    public async Task<PipelineResult> RunAsync(ILogger logger, IProgress<PipelineStage>? progress = null, CancellationToken cancellationToken = default)
    {
        var run = new StatisticsRun { StartedAt = DateTime.UtcNow, Status = RunStatus.Running };
        await using (var db = _dbFactory.CreateDbContext())
        {
            db.Runs.Add(run);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var result = await Task.Run(() => RunInternal(run, logger, progress, cancellationToken), cancellationToken);
            await FinishRun(run, RunStatus.Succeeded, null);
            return result;
        }
        catch (OperationCanceledException)
        {
            await FinishRun(run, RunStatus.Cancelled, null);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Statistics generation failed");
            await FinishRun(run, RunStatus.Failed, e.Message);
            throw;
        }
    }

    private async Task<PipelineResult> RunInternal(StatisticsRun run, ILogger logger, IProgress<PipelineStage>? progress, CancellationToken ct)
    {
        progress?.Report(PipelineStage.Loading);
        var chapters = await LoadChaptersAsync(ct);
        var characters = await _wikiService.GetIncludedCharactersAsync(ct);
        var entities = await _wikiService.GetIncludedEntitiesAsync(null, ct);
        run.ChapterCount = chapters.Count;
        run.CharacterCount = characters.Count;
        logger.LogInformation("Loaded {chapters} chapters, {characters} characters and wiki ground truth: {entities}", chapters.Count, characters.Count,
            string.Join(", ", entities.GroupBy(x => x.Kind).Select(g => $"{g.Count()} {g.Key}")));

        if (chapters.Count == 0)
            throw new InvalidOperationException("No downloaded chapters to process.");

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.CharacterSanityCheck);
        var sanity = WikiService.RunSanityCheck(characters);
        if (!sanity.Ok)
            throw new InvalidOperationException($"Character sanity check failed: {sanity.Error}");
        logger.LogInformation("Character Sanity Ok");

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.ChapterFixing);
        DirtyChapterFixing(chapters, logger);

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.FirstPass);
        var inferredDefinition = await InferDefinitions(chapters, characters, entities);

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.SecondPass);
        var statisticsParser = new ChapterStatisticsParser(logger, inferredDefinition);
        var finalChapterStatistics = await statisticsParser.Create(chapters);

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.Grouping);
        var asVolumeStatistics = finalChapterStatistics
            .GroupBy(x => x.Chapter.Volume)
            .Select(volumeGroup =>
            {
                var chapterStatistics = volumeGroup
                    .Select(chapterStatistic => new ChapterStatistics(chapterStatistic.Chapter, chapterStatistic.Statistics))
                    .ToList();
                return new VolumeStatistics(volumeGroup.Key, chapterStatistics);
            }).ToList();

        var wanderingInnStatistics = new FullStatistics(asVolumeStatistics);

        var finalDefinitions = new WanderingInnDefinitions();
        finalDefinitions.Add(wanderingInnStatistics);
        finalDefinitions.Characters.AddRange(inferredDefinition.Characters);
        finalDefinitions.AddGroundTruthOf(inferredDefinition);
        run.UnknownCount = finalDefinitions.Unknown.Count;
        logger.LogInformation("Unknowns: {unknown}", finalDefinitions.Unknown.Count);

        ct.ThrowIfCancellationRequested();
        progress?.Report(PipelineStage.Exporting);
        Directory.CreateDirectory(_options.EffectiveExportDirectory);
        StatisticsJsonExporter.StripChapterTexts(wanderingInnStatistics);
        await StatisticsJsonExporter.Serialise(_options.StatisticsExportFile, wanderingInnStatistics, ct);
        await StatisticsJsonExporter.Serialise(_options.DefinitionsExportFile, finalDefinitions, ct);
        run.StatisticsFile = _options.StatisticsExportFile;
        run.DefinitionsFile = _options.DefinitionsExportFile;

        var copiedTo = new List<string>();
        if (_options.CopyToViewer && !string.IsNullOrWhiteSpace(_options.ViewerDataDirectory))
        {
            foreach (var file in new[] { _options.StatisticsExportFile, _options.DefinitionsExportFile })
            {
                var target = Path.Combine(_options.ViewerDataDirectory!, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
                copiedTo.Add(target);
            }
        }

        logger.LogInformation("Exported {statistics} and {definitions}", run.StatisticsFile, run.DefinitionsFile);

        LastInferredDefinitions = inferredDefinition;
        progress?.Report(PipelineStage.Done);

        return new PipelineResult(run.Id, chapters.Count, characters.Count, finalDefinitions.Unknown.Count,
            run.StatisticsFile, run.DefinitionsFile, copiedTo);
    }

    private async Task<List<Chapter>> LoadChaptersAsync(CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Chapters
            .Where(x => !x.Excluded && x.RawText != null)
            .OrderBy(x => x.TocOrder)
            .ToListAsync(ct);
        return stored.Select(x => x.ToChapter()).ToList();
    }

    /// <summary>
    /// A first pass over <paramref name="chapters"/> infers classes, skills and spells from the brackets' context;
    /// the wiki's <paramref name="entities"/> are then added as ground truth on top, along with the characters.
    /// </summary>
    public static async Task<WanderingInnDefinitions> InferDefinitions(List<Chapter> chapters, List<CharacterRaw> characters, List<WikiEntityRaw>? entities = null)
    {
        entities ??= new List<WikiEntityRaw>();
        RaceHelper.Configure(RaceNames(entities));

        var groundTruth = new WanderingInnDefinitions();
        groundTruth.Add(entities);

        var wanderingInnDefinitions = new WanderingInnDefinitions();
        var firstPassParser = new ChapterStatisticsParser(NullLogger.Instance, wanderingInnDefinitions);
        var firstPass = await firstPassParser.Create(chapters);

        var inferredDefinition = new WanderingInnDefinitions();
        inferredDefinition.Add(firstPass.Select(x => x.Statistics));
        inferredDefinition.AddGroundTruthOf(groundTruth);
        inferredDefinition.Add(characters);
        return inferredDefinition;
    }

    /// <summary>
    /// Species usable as "Drake [Guardsman]": top-level ones, and sub-entries that are a variant of their parent
    /// ("Hobgoblins" under Goblins), but not ambiguous ones like "Soldier" under Antinium.
    /// </summary>
    private static IEnumerable<string> RaceNames(IEnumerable<WikiEntityRaw> entities) => entities
        .Where(x => x.Kind == WikiEntityKind.Species)
        .Where(x => x.Parent == null || x.Name.Contains(x.Parent.Singularize(false), StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Name);

    private static void DirtyChapterFixing(List<Chapter> chapters, ILogger logger)
    {
        var dirty = ChapterFixing.FindDirtyChapters(chapters);
        logger.LogInformation("Found {count} dirty chapters", dirty.Count);
        ChapterFixing.Fixup(chapters);
        dirty = ChapterFixing.FindDirtyChapters(chapters).Where(x => x.Volume.Trim() != "Volume 8").ToList();
        logger.LogInformation("{count} dirty chapters remaining", dirty.Count);
        if (dirty.Count > 0)
            throw new InvalidOperationException($"Still have dirty chapters: {string.Join(", ", dirty.Select(x => x.Name))}");
    }

    private async Task FinishRun(StatisticsRun run, RunStatus status, string? error)
    {
        run.Status = status;
        run.Error = error;
        run.FinishedAt = DateTime.UtcNow;
        await using var db = _dbFactory.CreateDbContext();
        db.Runs.Update(run);
        await db.SaveChangesAsync();
    }
}
