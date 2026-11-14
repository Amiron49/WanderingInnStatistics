using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using WanderingInnStats.Pipeline.Data;

namespace WanderingInnStats.Pipeline.Services;

/// <param name="Applied">The fix steps that changed the text.</param>
/// <param name="RefusedFix">The step whose safety check threw <paramref name="Error"/>.</param>
/// <param name="DirtyReason">Why the (fixed) text still looks dirty, or null.</param>
public record FixedTextResult(string? Text, string? Error, bool IsDirty, ChapterFix Applied = ChapterFix.None, ChapterFix RefusedFix = ChapterFix.None,
    string? DirtyReason = null);

public record ChapterPreview(IWanderingInnStatistics Statistics, bool Approximate, string? FixupError);

public class ChapterPreviewService
{
    private readonly WorkspaceDbFactory _dbFactory;
    private readonly StatisticsPipeline _pipeline;
    private readonly WikiService _wikiService;

    public ChapterPreviewService(WorkspaceDbFactory dbFactory, StatisticsPipeline pipeline, WikiService wikiService)
    {
        _dbFactory = dbFactory;
        _pipeline = pipeline;
        _wikiService = wikiService;
    }

    public static FixedTextResult GetFixedText(Chapter chapter, ChapterFix disabled = ChapterFix.None)
    {
        try
        {
            var (fixedText, applied) = ChapterFixing.GetFixed(chapter, disabled);
            var fixedChapter = new Chapter { Name = chapter.Name, Volume = chapter.Volume, Url = chapter.Url, Text = fixedText };
            var reason = SafeDirtyReason(fixedChapter, disabled);
            return new FixedTextResult(fixedText, null, reason != null, applied, ChapterFix.None, reason);
        }
        catch (Exception e)
        {
            var reason = SafeDirtyReason(chapter, disabled);
            return new FixedTextResult(null, e.Message, reason != null, ChapterFix.None, (e as ChapterFixupException)?.Fix ?? ChapterFix.None, reason);
        }
    }

    public static bool SafeIsDirty(Chapter chapter, ChapterFix disabled = ChapterFix.None) => SafeDirtyReason(chapter, disabled) != null;

    private static string? SafeDirtyReason(Chapter chapter, ChapterFix disabled)
    {
        try
        {
            return ChapterFixing.DirtyReason(chapter, disabled);
        }
        catch (Exception e)
        {
            // Treat anything the dirty check can't handle as dirty, so it gets looked at.
            return $"the dirty check failed: {e.Message}";
        }
    }

    public async Task<ChapterPreview> PreviewAsync(int chapterId, CancellationToken cancellationToken = default)
    {
        StoredChapter stored;
        await using (var db = _dbFactory.CreateDbContext())
            stored = await db.Chapters.SingleAsync(x => x.Id == chapterId, cancellationToken);

        var chapter = stored.ToChapter();
        var fixedResult = GetFixedText(chapter, stored.DisabledFixes);
        if (fixedResult.Text != null)
            chapter.Text = fixedResult.Text;

        var definitions = _pipeline.LastInferredDefinitions;
        var approximate = definitions == null;

        if (definitions == null)
        {
            var characters = await _wikiService.GetIncludedCharactersAsync(cancellationToken);
            var entities = await _wikiService.GetIncludedEntitiesAsync(null, cancellationToken);
            definitions = await StatisticsPipeline.InferDefinitions(new List<Chapter> { chapter }, characters, entities);
        }

        var parser = new ChapterStatisticsParser(NullLogger.Instance, definitions);
        var statistics = await parser.Create(chapter);
        return new ChapterPreview(statistics, approximate, fixedResult.Error);
    }
}
