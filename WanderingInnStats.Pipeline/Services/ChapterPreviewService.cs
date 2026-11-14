using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using WanderingInnStats.Pipeline.Data;

namespace WanderingInnStats.Pipeline.Services;

public record FixedTextResult(string? Text, string? Error, bool IsDirty);

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

    public static FixedTextResult GetFixedText(Chapter chapter)
    {
        try
        {
            var fixedText = ChapterFixing.GetFixedText(chapter);
            var fixedChapter = new Chapter { Name = chapter.Name, Volume = chapter.Volume, Url = chapter.Url, Text = fixedText };
            return new FixedTextResult(fixedText, null, SafeIsDirty(fixedChapter));
        }
        catch (Exception e)
        {
            return new FixedTextResult(null, e.Message, SafeIsDirty(chapter));
        }
    }

    public static bool SafeIsDirty(Chapter chapter)
    {
        try
        {
            return ChapterFixing.IsDirty(chapter);
        }
        catch
        {
            // FindDirtyChapters slices Text[..20] and throws on very short texts.
            return true;
        }
    }

    public async Task<ChapterPreview> PreviewAsync(int chapterId, CancellationToken cancellationToken = default)
    {
        StoredChapter stored;
        await using (var db = _dbFactory.CreateDbContext())
            stored = await db.Chapters.SingleAsync(x => x.Id == chapterId, cancellationToken);

        var chapter = stored.ToChapter();
        var fixedResult = GetFixedText(chapter);
        if (fixedResult.Text != null)
            chapter.Text = fixedResult.Text;

        var definitions = _pipeline.LastInferredDefinitions;
        var approximate = definitions == null;

        if (definitions == null)
        {
            var characters = await _wikiService.GetIncludedCharactersAsync(cancellationToken);
            definitions = await StatisticsPipeline.InferDefinitions(new List<Chapter> { chapter }, characters);
        }

        var parser = new ChapterStatisticsParser(NullLogger.Instance, definitions);
        var statistics = await parser.Create(chapter);
        return new ChapterPreview(statistics, approximate, fixedResult.Error);
    }
}
