using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Core;
using WanderingInnStats.Pipeline.Data;
using WanderingInnStats.Pipeline.Diagnostics;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.Pipeline.Services;

/// <summary>The lists taken from the wiki.</summary>
public enum WikiSource
{
    Characters,
    Spells,
    Classes,
    Species,
    Monsters,
    Locations
}

public record WikiProgress(int Done, int Total, string Current, string Stage);

public record WikiPageFailure(string Url, string Error);

/// <param name="Error">Why the source wasn't stored; its previous data was kept. Null when it was stored.</param>
public record WikiSourceResult(WikiSource Source, int Count, string? Error);

public record WikiDownloadResult(IReadOnlyList<WikiSourceResult> Sources, IReadOnlyList<WikiPageFailure> Failures, int Requests)
{
    public int Characters => Sources.FirstOrDefault(x => x.Source == WikiSource.Characters)?.Count ?? 0;

    public string Summary => string.Join(", ", Sources.Select(x => x.Error == null ? $"{x.Count} {x.Source.ToString().ToLowerInvariant()}" : $"{x.Source} failed"))
                             + (Failures.Count > 0 ? $", {Failures.Count} pages failed" : "")
                             + $" ({Requests} requests)";
}

public record SanityCheckResult(bool Ok, string? Error);

public record WikiSourceInfo(WikiSource Source, int Count, int Excluded, DateTime? LastDownload);

public class WikiService
{
    private const int MaxSnapshots = 3;

    public static readonly IReadOnlyList<WikiSource> AllSources = Enum.GetValues<WikiSource>();

    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WikiService> _logger;

    public WikiService(WorkspaceDbFactory dbFactory, WorkspaceOptions options, ILogger<WikiService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _logger = logger;
    }

    public static WikiEntityKind? KindOf(WikiSource source) => source switch
    {
        WikiSource.Characters => null,
        WikiSource.Spells => WikiEntityKind.Spell,
        WikiSource.Classes => WikiEntityKind.Class,
        WikiSource.Species => WikiEntityKind.Species,
        WikiSource.Monsters => WikiEntityKind.Monster,
        WikiSource.Locations => WikiEntityKind.Location,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };

    public static WikiSource SourceOf(WikiEntityKind kind) => AllSources.First(x => KindOf(x) == kind);

    public static string SourceUrl(WikiSource source) => WikiScrapper.SourceUrl(KindOf(source));

    /// <summary>Rough number of requests a download of <paramref name="sources"/> takes when nothing is cached.</summary>
    public static int EstimatedRequests(IEnumerable<WikiSource> sources) => sources.Sum(x => WikiScrapper.EstimatedRequests(KindOf(x)));

    public Task<WikiDownloadResult> DownloadAllAsync(bool forceRefresh, IProgress<WikiProgress>? progress = null, CancellationToken cancellationToken = default)
        => DownloadAsync(AllSources, forceRefresh, progress, cancellationToken);

    /// <summary>
    /// Downloads <paramref name="sources"/> from the wiki API and replaces what is stored for each one that succeeded.
    /// A source that fails or yields nothing keeps its stored data. A blocked request (4xx/5xx, Cloudflare) stops everything.
    /// </summary>
    /// <param name="forceRefresh">
    /// Deletes the scrapper's response cache (<see cref="WorkspaceOptions.ScrapperWikiCacheFile"/>) first, so everything is re-fetched.
    /// Without it, cached API responses are reused and only requests not made before hit the network.
    /// </param>
    public async Task<WikiDownloadResult> DownloadAsync(IReadOnlyCollection<WikiSource> sources, bool forceRefresh, IProgress<WikiProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(WorkspaceOptions.ScrapperWikiCacheDirectory);

        if (forceRefresh && File.Exists(WorkspaceOptions.ScrapperWikiCacheFile))
        {
            _logger.LogInformation("Deleting wiki cache {file}", WorkspaceOptions.ScrapperWikiCacheFile);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(WorkspaceOptions.ScrapperWikiCacheFile);
        }

        var results = new List<WikiSourceResult>();
        var failures = new List<WikiPageFailure>();
        var snapshots = 0;
        var index = 0;

        using var scrapper = new WikiScrapper();
        using var capture = await ConsoleCapture.StartAsync(cancellationToken);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stage = source.ToString();
            var done = index++;
            progress?.Report(new WikiProgress(done, sources.Count, "", stage));
            var pageProgress = new Progress<(int pages, string current)>(p =>
                progress?.Report(new WikiProgress(done, sources.Count, $"{p.pages} pages, {p.current}", stage)));

            try
            {
                int count;
                if (source == WikiSource.Characters)
                {
                    var fetched = await scrapper.GetCharactersAsync(pageProgress, cancellationToken);
                    failures.AddRange(fetched.Failures.Select(x => new WikiPageFailure(x.Url, x.Error)));
                    if (fetched.Items.Count == 0)
                        throw new InvalidOperationException("The character category yielded no characters; stored characters were left untouched");
                    await ReplaceCharactersAsync(fetched.Items, cancellationToken);
                    count = fetched.Items.Count;
                }
                else
                {
                    var kind = KindOf(source)!.Value;
                    var fetched = await scrapper.GetEntitiesAsync(kind, pageProgress, cancellationToken);
                    failures.AddRange(fetched.Failures.Select(x => new WikiPageFailure(x.Url, x.Error)));
                    if (fetched.Items.Count == 0)
                        throw new InvalidOperationException($"The wiki yielded no {stage.ToLowerInvariant()}; stored {stage.ToLowerInvariant()} were left untouched");
                    await ReplaceEntitiesAsync(kind, fetched.Items, cancellationToken);
                    count = fetched.Items.Count;
                }

                results.Add(new WikiSourceResult(source, count, null));
                _logger.LogInformation("Wiki {source}: {count} stored", source, count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e) when (ScrapeDiagnostics.FindBlocked(e) != null)
            {
                // The server is refusing us: stop instead of carrying on with the other sources.
                var snapshot = await ScrapeDiagnostics.SnapshotAsync(e, SourceUrl(source), Array.Empty<string>(), _options.DiagnosticsDirectory, cancellationToken);
                throw new ScrapeException(
                    $"Stopped while downloading {stage}: {e.Message}. Sources downloaded before were stored ({string.Join(", ", results.Select(x => x.Source))}); fetched responses stay cached.",
                    e, snapshot, capture.Take());
            }
            catch (Exception e)
            {
                var error = e.ToString();
                if (e is not ScrapeException && snapshots++ < MaxSnapshots)
                    error += "\n\n" + await ScrapeDiagnostics.CaptureAsync(SourceUrl(source), Array.Empty<string>(), _options.DiagnosticsDirectory, cancellationToken);
                var console = capture.Take().Trim();
                if (console.Length > 0)
                    error += "\n\nScrapper console output:\n" + console;

                results.Add(new WikiSourceResult(source, 0, e.Message));
                failures.Add(new WikiPageFailure(SourceUrl(source), error));
                _logger.LogWarning("Wiki {source} failed: {error}", source, e.Message);
            }
        }

        progress?.Report(new WikiProgress(sources.Count, sources.Count, "", "Done"));

        if (results.All(x => x.Error != null))
            throw new ScrapeException($"Every wiki source failed; nothing was stored. First error: {results.FirstOrDefault()?.Error}",
                null, null, failures.FirstOrDefault()?.Error);

        return new WikiDownloadResult(results, failures, scrapper.RequestsMade);
    }

    /// <summary>
    /// Replaces all stored characters. Characters excluded before stay excluded, matched by url or by name
    /// (the name match carries the flags over from the old fandom wiki).
    /// </summary>
    public async Task ReplaceCharactersAsync(IEnumerable<CharacterRaw> characters, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var existingExcluded = await db.Characters.Where(x => x.Excluded).Select(x => new { x.WikiUrl, x.Name }).ToListAsync(cancellationToken);
        var excludedUrls = existingExcluded.Select(x => x.WikiUrl).ToHashSet();
        var excludedNames = existingExcluded.Select(x => x.Name).ToHashSet();

        await db.Characters.ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        // The wiki can list the same page twice; first one wins.
        var order = 0;
        foreach (var character in characters.DistinctBy(x => x.WikiUrl))
        {
            var stored = StoredCharacter.From(character, order++, now);
            if (excludedUrls.Contains(stored.WikiUrl) || excludedNames.Contains(stored.Name))
                stored.Excluded = true;
            db.Characters.Add(stored);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Replaces the stored entities of <paramref name="kind"/>, keeping the Excluded flags of entries that already existed.</summary>
    public async Task ReplaceEntitiesAsync(WikiEntityKind kind, IEnumerable<WikiEntityRaw> entities, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var excluded = (await db.WikiEntities.Where(x => x.Kind == kind && x.Excluded).Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();

        await db.WikiEntities.Where(x => x.Kind == kind).ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var order = 0;
        foreach (var entity in entities.Where(x => x.Kind == kind).DistinctBy(x => x.Name))
        {
            var stored = StoredWikiEntity.From(entity, order++, now);
            stored.Excluded = excluded.Contains(stored.Id);
            db.WikiEntities.Add(stored);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<CharacterRaw>> GetIncludedCharactersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Characters.Where(x => !x.Excluded).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        return stored.Select(x => x.ToCharacter()).ToList();
    }

    /// <summary>The included entities, of <paramref name="kind"/> or of every kind.</summary>
    public async Task<List<WikiEntityRaw>> GetIncludedEntitiesAsync(WikiEntityKind? kind = null, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var query = db.WikiEntities.Where(x => !x.Excluded);
        if (kind != null)
            query = query.Where(x => x.Kind == kind);
        var stored = await query.ToListAsync(cancellationToken);
        return stored.OrderBy(x => x.Kind).ThenBy(x => x.SortOrder).Select(x => x.ToEntity()).ToList();
    }

    public async Task<List<WikiSourceInfo>> GetSourceInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var characters = await db.Characters
            .GroupBy(x => 1)
            .Select(g => new { Count = g.Count(), Excluded = g.Count(x => x.Excluded), Last = g.Max(x => x.DownloadedAt) })
            .FirstOrDefaultAsync(cancellationToken);
        var entities = await db.WikiEntities
            .GroupBy(x => x.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count(), Excluded = g.Count(x => x.Excluded), Last = g.Max(x => x.DownloadedAt) })
            .ToListAsync(cancellationToken);

        return AllSources.Select(source =>
        {
            if (source == WikiSource.Characters)
                return new WikiSourceInfo(source, characters?.Count ?? 0, characters?.Excluded ?? 0, characters?.Last);
            var row = entities.FirstOrDefault(x => x.Kind == KindOf(source));
            return new WikiSourceInfo(source, row?.Count ?? 0, row?.Excluded ?? 0, row?.Last);
        }).ToList();
    }

    public async Task SetExcludedAsync(string wikiUrl, bool excluded)
    {
        await using var db = _dbFactory.CreateDbContext();
        var row = await db.Characters.SingleAsync(x => x.WikiUrl == wikiUrl);
        row.Excluded = excluded;
        await db.SaveChangesAsync();
    }

    public async Task SetEntityExcludedAsync(string id, bool excluded)
    {
        await using var db = _dbFactory.CreateDbContext();
        var row = await db.WikiEntities.SingleAsync(x => x.Id == id);
        row.Excluded = excluded;
        await db.SaveChangesAsync();
    }

    public async Task<SanityCheckResult> RunSanityCheckAsync(CancellationToken cancellationToken = default)
    {
        var characters = await GetIncludedCharactersAsync(cancellationToken);
        return RunSanityCheck(characters);
    }

    public static SanityCheckResult RunSanityCheck(List<CharacterRaw> characters)
    {
        try
        {
            var sanityDefinition = new WanderingInnDefinitions();
            sanityDefinition.Add(characters);
            sanityDefinition.CheckForCharacterConflicts();
            return new SanityCheckResult(true, null);
        }
        catch (Exception e)
        {
            return new SanityCheckResult(false, e.Message);
        }
    }
}
