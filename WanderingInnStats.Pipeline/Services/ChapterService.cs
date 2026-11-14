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
    string? LastError,
    bool PatreonLocked);

public record DownloadProgress(int Done, int Total, string Current, int Failed);

public record DownloadResult(int Succeeded, int Failed, int OverridesMarkedStale, int PatreonLocked);

public enum ChapterSource
{
    /// <summary>Plain HTTP request from the app.</summary>
    Http,

    /// <summary>Loaded by the user's browser through the <see cref="BrowserBridge"/>.</summary>
    Browser
}

/// <param name="BrowserFallback">With <see cref="ChapterSource.Http"/>: retry Patreon-locked chapters through the browser when it's connected.</param>
public record DownloadMode(ChapterSource Source, bool BrowserFallback)
{
    public static readonly DownloadMode Http = new(ChapterSource.Http, false);
    public static readonly DownloadMode Browser = new(ChapterSource.Browser, false);
}

public class ChapterService
{

    /// <summary>Per download batch, only snapshot the first few failures; if everything fails they're all the same anyway.</summary>
    private const int MaxSnapshotsPerBatch = 3;

    /// <summary>After this many browser fetches in a row that still got the Patreon prompt, the browser apparently isn't logged in.</summary>
    private const int MaxBrowserLockedInARow = 3;

    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<ChapterService> _logger;
    private readonly BrowserBridge _bridge;

    public ChapterService(WorkspaceDbFactory dbFactory, WorkspaceOptions options, ILogger<ChapterService> logger, BrowserBridge bridge)
    {
        _dbFactory = dbFactory;
        _options = options;
        _logger = logger;
        _bridge = bridge;
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
                x.OverrideText != null, x.OverrideStale, x.Excluded, x.DownloadedAt, x.LastError, x.PatreonLocked))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Stored chapter ids by url, for linking to chapters named in errors.</summary>
    public async Task<Dictionary<string, int>> GetIdsByUrlAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls.Distinct().ToList();
        await using var db = _dbFactory.CreateDbContext();
        var rows = await db.Chapters.Where(x => urlList.Contains(x.Url)).Select(x => new { x.Url, x.Id }).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Url, x => x.Id);
    }

    /// <summary>Stored chapter ids by name, for linking chapter names in stored error texts. Ambiguous names are left out.</summary>
    public async Task<Dictionary<string, int>> GetIdsByNameAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var rows = await db.Chapters.Select(x => new { x.Name, x.Id }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.Name.Trim()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Id);
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

    public async Task<List<ChapterRef>> GetPatreonLockedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Chapters
            .Where(x => x.PatreonLocked)
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

    /// <summary>Downloads over plain HTTP, without the browser fallback.</summary>
    public Task<DownloadResult> DownloadAsync(IReadOnlyList<ChapterRef> chapters, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        DownloadAsync(chapters, DownloadMode.Http, progress, cancellationToken);

    /// <summary>
    /// Downloads <paramref name="chapters"/> one after the other and stores each result right away.
    /// Stops (throws <see cref="ScrapeException"/>) when the server refuses us, when the browser extension is gone,
    /// or when the browser keeps seeing the Patreon prompt, so it isn't logged in (or the tier is too low).
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(IReadOnlyList<ChapterRef> chapters, DownloadMode mode, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int done = 0, failed = 0, stale = 0, snapshots = 0, locked = 0, browserLockedInARow = 0;
        var usedBrowser = false;

        try
        {
            foreach (var chapterRef in chapters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new DownloadProgress(done, chapters.Count, chapterRef.Name, failed));

                Core.Chapter? downloaded = null;
                string? error = null;
                Exception? stopReason = null;
                PageSnapshot? snapshot = null;
                var isLocked = false;
                var viaBrowser = false;
                try
                {
                    downloaded = await FetchChapterAsync(chapterRef, mode, () => viaBrowser = usedBrowser = true, cancellationToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    error = e.ToString();
                    isLocked = e is PatreonLockedException;
                    if (e is ScrapeBlockedException or BrowserBridgeUnavailableException)
                        stopReason = e;

                    // A blocked response carries its own body, so snapshotting it costs no extra request. Locked pages and
                    // bridge failures need no snapshot: the message already says what's wrong.
                    if (e is ScrapeBlockedException || (!isLocked && stopReason == null && snapshots < MaxSnapshotsPerBatch))
                    {
                        snapshots++;
                        snapshot = await ScrapeDiagnostics.SnapshotAsync(e, chapterRef.Url, Scrapper.ChapterSelectors, _options.DiagnosticsDirectory, cancellationToken);
                        error += "\n\n" + snapshot;
                    }
                }

                if (stopReason is BrowserBridgeUnavailableException bridgeError)
                    // Says nothing about the chapter itself, so its stored state is left alone.
                    throw new ScrapeException(
                        $"Stopped after {done} of {chapters.Count} chapters ({done - failed} downloaded, saved): {bridgeError.Message}",
                        bridgeError, null, null);

                if (viaBrowser)
                    browserLockedInARow = isLocked ? browserLockedInARow + 1 : 0;
                if (isLocked)
                    locked++;

                await SaveDownloadAsync(chapterRef, downloaded, error, isLocked, () => stale++, cancellationToken);
                if (downloaded == null)
                    failed++;

                done++;
                progress?.Report(new DownloadProgress(done, chapters.Count, chapterRef.Name, failed));

                // The server is refusing us (4xx/5xx or Cloudflare): stop instead of hammering it with the rest of the batch.
                if (stopReason is ScrapeBlockedException blocked)
                    throw new ScrapeException(
                        $"Stopped after {done} of {chapters.Count} chapters ({done - failed} downloaded, saved): {blocked.Message}",
                        blocked, snapshot, null);

                if (browserLockedInARow >= MaxBrowserLockedInARow)
                    throw new ScrapeException(
                        $"Stopped after {done} of {chapters.Count} chapters ({done - failed} downloaded, saved): the browser also got the Patreon " +
                        $"unlock prompt for the last {MaxBrowserLockedInARow} chapters. Log into wanderinginn.com with Patreon in that browser " +
                        "(Settings → Browser bridge → Open login page) or check that your tier is high enough.",
                        null, null, error);
            }
        }
        finally
        {
            if (usedBrowser)
                await _bridge.FinishAsync();
        }

        return new DownloadResult(done - failed, failed, stale, locked);
    }

    private async Task<Core.Chapter> FetchChapterAsync(ChapterRef chapterRef, DownloadMode mode, Action usingBrowser, CancellationToken cancellationToken)
    {
        if (mode.Source == ChapterSource.Browser)
        {
            usingBrowser();
            return await Scrapper.GetChapterViaBrowser(_bridge, chapterRef.Volume, chapterRef.Name, chapterRef.Url, cancellationToken);
        }

        try
        {
            return await Scrapper.GetChapter(chapterRef.Volume, chapterRef.Name, chapterRef.Url, cancellationToken);
        }
        catch (PatreonLockedException) when (mode.BrowserFallback && _bridge.IsConnected)
        {
            _logger.LogInformation("{chapter} is Patreon-locked; retrying via the browser", chapterRef.Name);
            usingBrowser();
            return await Scrapper.GetChapterViaBrowser(_bridge, chapterRef.Volume, chapterRef.Name, chapterRef.Url, cancellationToken);
        }
    }

    private async Task SaveDownloadAsync(ChapterRef chapterRef, Core.Chapter? downloaded, string? error, bool isLocked, Action markedStale,
        CancellationToken cancellationToken)
    {
        await using var db = _dbFactory.CreateDbContext();
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
                markedStale();
            }

            row.RawText = downloaded.Text;
            row.Date = downloaded.Date;
            row.Comments = downloaded.Comments;
            row.DownloadedAt = DateTime.UtcNow;
            row.LastError = null;
            row.PatreonLocked = false;
            _logger.LogInformation("Downloaded {chapter}", chapterRef.Name);
        }
        else
        {
            row.LastError = error;
            if (isLocked)
                row.PatreonLocked = true;
            _logger.LogWarning("Failed downloading {chapter}: {error}", chapterRef.Name, error);
        }

        await db.SaveChangesAsync(cancellationToken);
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
