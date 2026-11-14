using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Pipeline.Data;
using WanderingInnStats.Pipeline.Diagnostics;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.Pipeline.Services;

public record ChapterRef(string Volume, string Name, string Url);

public enum TocChangeKind
{
    New,
    Missing,
    Renamed,
    Unchanged
}

public record TocChange(TocChangeKind Kind, ChapterRef Toc, int TocOrder, string? StoredVolume, string? StoredName);

public class TocDiff
{
    public List<TocChange> Changes { get; } = new();
    public IEnumerable<TocChange> New => Changes.Where(x => x.Kind == TocChangeKind.New);
    public IEnumerable<TocChange> Missing => Changes.Where(x => x.Kind == TocChangeKind.Missing);
    public IEnumerable<TocChange> Renamed => Changes.Where(x => x.Kind == TocChangeKind.Renamed);
    public int UnchangedCount => Changes.Count(x => x.Kind == TocChangeKind.Unchanged);
}

public record ChapterSummary(
    int Id,
    string Url,
    string Volume,
    string Name,
    int TocOrder,
    bool InToc,
    DateTime Date,
    int Comments,
    bool IsDownloaded,
    int Length,
    bool HasOverride,
    bool OverrideStale,
    bool Excluded,
    DateTime? DownloadedAt,
    string? LastError);

public record DownloadProgress(int Done, int Total, string Current, int Failed);

public record DownloadResult(int Succeeded, int Failed, int OverridesMarkedStale);

public class ChapterService
{

    /// <summary>Per download batch, only snapshot the first few failures; if everything fails they're all the same anyway.</summary>
    private const int MaxSnapshotsPerBatch = 3;

    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<ChapterService> _logger;

    public ChapterService(WorkspaceDbFactory dbFactory, WorkspaceOptions options, ILogger<ChapterService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<TocDiff> GetTocDiffAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching table of contents");

        Toc toc;
        using (var capture = await ConsoleCapture.StartAsync(cancellationToken))
        {
            try
            {
                toc = await Scrapper.GetToc();
            }
            catch (Exception e)
            {
                var consoleOutput = capture.Take();
                _logger.LogWarning("Scrapper.GetToc threw {type}; capturing a snapshot of {url}", e.GetType().Name, Scrapper.TocUrl);
                var snapshot = await ScrapeDiagnostics.SnapshotAsync(e, Scrapper.TocUrl, Scrapper.TocSelectors, _options.DiagnosticsDirectory, cancellationToken);
                _logger.LogWarning("{snapshot}", snapshot.ToString());
                throw new ScrapeException($"Scrapper.GetToc failed ({e.GetType().Name}: {e.Message})", e, snapshot, consoleOutput);
            }
        }

        if (toc.Contents.Count == 0)
        {
            var snapshot = await ScrapeDiagnostics.CaptureAsync(Scrapper.TocUrl, Scrapper.TocSelectors, _options.DiagnosticsDirectory, cancellationToken);
            throw new ScrapeException("Scrapper.GetToc returned an empty table of contents", null, snapshot, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Chapters
            .Select(x => new { x.Url, x.Volume, x.Name })
            .ToDictionaryAsync(x => x.Url, cancellationToken);

        var diff = new TocDiff();
        var seen = new HashSet<string>();

        for (var i = 0; i < toc.Contents.Count; i++)
        {
            var (volume, name, url) = toc.Contents[i];
            var tocRef = new ChapterRef(volume, name, url);

            // The TOC occasionally lists the same url twice; first one wins.
            if (!seen.Add(url))
                continue;

            if (!stored.TryGetValue(url, out var existing))
                diff.Changes.Add(new TocChange(TocChangeKind.New, tocRef, i, null, null));
            else if (existing.Name != name || existing.Volume != volume)
                diff.Changes.Add(new TocChange(TocChangeKind.Renamed, tocRef, i, existing.Volume, existing.Name));
            else
                diff.Changes.Add(new TocChange(TocChangeKind.Unchanged, tocRef, i, existing.Volume, existing.Name));
        }

        foreach (var missing in stored.Values.Where(x => !seen.Contains(x.Url)))
            diff.Changes.Add(new TocChange(TocChangeKind.Missing, new ChapterRef(missing.Volume, missing.Name, missing.Url), -1, missing.Volume, missing.Name));

        _logger.LogInformation("TOC: {new} new, {missing} missing, {renamed} renamed, {unchanged} unchanged",
            diff.New.Count(), diff.Missing.Count(), diff.Renamed.Count(), diff.UnchangedCount);

        return diff;
    }

    /// <summary>
    /// Persists a TOC diff: inserts placeholder rows for new chapters, applies renames / ordering and
    /// marks chapters that vanished from the TOC.
    /// </summary>
    public async Task ApplyTocAsync(TocDiff diff, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Chapters.ToDictionaryAsync(x => x.Url, cancellationToken);

        foreach (var change in diff.Changes)
        {
            switch (change.Kind)
            {
                case TocChangeKind.New:
                    db.Chapters.Add(new StoredChapter
                    {
                        Url = change.Toc.Url,
                        Volume = change.Toc.Volume,
                        Name = change.Toc.Name,
                        TocOrder = change.TocOrder,
                        InToc = true,
                        Excluded = ExclusionRules.IsChapterExcludedByDefault(change.Toc.Name)
                    });
                    break;
                case TocChangeKind.Renamed:
                case TocChangeKind.Unchanged:
                    var row = stored[change.Toc.Url];
                    row.Volume = change.Toc.Volume;
                    row.Name = change.Toc.Name;
                    row.TocOrder = change.TocOrder;
                    row.InToc = true;
                    break;
                case TocChangeKind.Missing:
                    stored[change.Toc.Url].InToc = false;
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>List view without loading the (large) chapter texts.</summary>
    public async Task<List<ChapterSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Chapters
            .OrderBy(x => x.TocOrder)
            .Select(x => new ChapterSummary(
                x.Id, x.Url, x.Volume, x.Name, x.TocOrder, x.InToc, x.Date, x.Comments,
                x.RawText != null, x.RawText == null ? 0 : x.RawText.Length,
                x.OverrideText != null, x.OverrideStale, x.Excluded, x.DownloadedAt, x.LastError))
            .ToListAsync(cancellationToken);
    }

    public async Task<StoredChapter?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Chapters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>Runs the dirty check over every downloaded chapter's effective text. Returns the dirty chapter ids.</summary>
    public async Task<HashSet<int>> FindDirtyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var dirty = new HashSet<int>();
        await foreach (var row in db.Chapters.AsNoTracking().Where(x => x.RawText != null).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var fixedResult = ChapterPreviewService.GetFixedText(row.ToChapter());
            if (fixedResult.IsDirty || fixedResult.Error != null)
                dirty.Add(row.Id);
        }

        return dirty;
    }

    public async Task<List<(string Name, string Error)>> GetErrorsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls.ToList();
        await using var db = _dbFactory.CreateDbContext();
        var rows = await db.Chapters
            .Where(x => urlList.Contains(x.Url) && x.LastError != null)
            .OrderBy(x => x.TocOrder)
            .Select(x => new { x.Name, x.LastError })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Name, x.LastError!)).ToList();
    }

    public async Task<List<ChapterRef>> GetNotDownloadedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Chapters
            .Where(x => x.RawText == null)
            .OrderBy(x => x.TocOrder)
            .Select(x => new ChapterRef(x.Volume, x.Name, x.Url))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChapterRef>> GetRefsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        await using var db = _dbFactory.CreateDbContext();
        return await db.Chapters
            .Where(x => idList.Contains(x.Id))
            .OrderBy(x => x.TocOrder)
            .Select(x => new ChapterRef(x.Volume, x.Name, x.Url))
            .ToListAsync(cancellationToken);
    }

    public async Task<DownloadResult> DownloadAsync(IReadOnlyList<ChapterRef> chapters, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int done = 0, failed = 0, stale = 0, snapshots = 0;

        foreach (var chapterRef in chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DownloadProgress(done, chapters.Count, chapterRef.Name, failed));

            Core.Chapter? downloaded = null;
            string? error = null;
            ScrapeBlockedException? blocked = null;
            PageSnapshot? snapshot = null;
            try
            {
                downloaded = await Scrapper.GetChapter(chapterRef.Volume, chapterRef.Name, chapterRef.Url, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                error = e.ToString();
                blocked = e as ScrapeBlockedException;

                // A blocked response carries its own body, so snapshotting it costs no extra request.
                if (blocked != null || snapshots < MaxSnapshotsPerBatch)
                {
                    snapshots++;
                    snapshot = await ScrapeDiagnostics.SnapshotAsync(e, chapterRef.Url, Scrapper.ChapterSelectors, _options.DiagnosticsDirectory, cancellationToken);
                    error += "\n\n" + snapshot;
                }
            }

            await using (var db = _dbFactory.CreateDbContext())
            {
                var row = await db.Chapters.SingleOrDefaultAsync(x => x.Url == chapterRef.Url, cancellationToken);
                if (row == null)
                {
                    var maxOrder = await db.Chapters.Select(x => (int?)x.TocOrder).MaxAsync(cancellationToken) ?? -1;
                    row = new StoredChapter
                    {
                        Url = chapterRef.Url,
                        Volume = chapterRef.Volume,
                        Name = chapterRef.Name,
                        TocOrder = maxOrder + 1,
                        Excluded = ExclusionRules.IsChapterExcludedByDefault(chapterRef.Name)
                    };
                    db.Chapters.Add(row);
                }

                if (downloaded != null)
                {
                    if (row.OverrideText != null && row.RawText != null && row.RawText != downloaded.Text)
                    {
                        row.OverrideStale = true;
                        stale++;
                    }

                    row.RawText = downloaded.Text;
                    row.Date = downloaded.Date;
                    row.Comments = downloaded.Comments;
                    row.DownloadedAt = DateTime.UtcNow;
                    row.LastError = null;
                    _logger.LogInformation("Downloaded {chapter}", chapterRef.Name);
                }
                else
                {
                    row.LastError = error;
                    failed++;
                    _logger.LogWarning("Failed downloading {chapter}: {error}", chapterRef.Name, error);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            done++;
            progress?.Report(new DownloadProgress(done, chapters.Count, chapterRef.Name, failed));

            // The server is refusing us (4xx/5xx or Cloudflare): stop instead of hammering it with the rest of the batch.
            if (blocked != null)
                throw new ScrapeException(
                    $"Stopped after {done} of {chapters.Count} chapters ({done - failed} downloaded, saved): {blocked.Message}",
                    blocked, snapshot, null);
        }

        return new DownloadResult(done - failed, failed, stale);
    }

    public async Task SetOverrideAsync(int id, string? overrideText)
    {
        await using var db = _dbFactory.CreateDbContext();
        var row = await db.Chapters.SingleAsync(x => x.Id == id);
        row.OverrideText = string.IsNullOrEmpty(overrideText) ? null : overrideText;
        row.OverrideStale = false;
        await db.SaveChangesAsync();
    }

    public async Task SetExcludedAsync(int id, bool excluded)
    {
        await using var db = _dbFactory.CreateDbContext();
        var row = await db.Chapters.SingleAsync(x => x.Id == id);
        row.Excluded = excluded;
        await db.SaveChangesAsync();
    }
}
