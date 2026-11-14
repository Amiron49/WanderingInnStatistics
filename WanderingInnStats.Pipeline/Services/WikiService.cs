using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Core;
using WanderingInnStats.Pipeline.Data;
using WanderingInnStats.Pipeline.Diagnostics;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.Pipeline.Services;

public record WikiProgress(int Done, int Total, string Current, string Stage);

public record WikiPageFailure(string Url, string Error);

public record WikiDownloadResult(int Characters, int Skipped, IReadOnlyList<WikiPageFailure> Failures);

public record SanityCheckResult(bool Ok, string? Error);

public class WikiService
{
    private const int MaxSnapshots = 3;

    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WikiService> _logger;

    public WikiService(WorkspaceDbFactory dbFactory, WorkspaceOptions options, ILogger<WikiService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _logger = logger;
    }

    /// <param name="forceRefresh">
    /// Deletes the scrapper's HTML cache (<see cref="WorkspaceOptions.ScrapperWikiCacheFile"/>) first, so every page is re-fetched.
    /// Without it, already cached pages are served from the cache and only new pages are fetched.
    /// </param>
    public async Task<WikiDownloadResult> DownloadAllAsync(bool forceRefresh, IProgress<WikiProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(WorkspaceOptions.ScrapperWikiCacheDirectory);

        if (forceRefresh && File.Exists(WorkspaceOptions.ScrapperWikiCacheFile))
        {
            _logger.LogInformation("Deleting wiki HTML cache {file}", WorkspaceOptions.ScrapperWikiCacheFile);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(WorkspaceOptions.ScrapperWikiCacheFile);
        }

        var characters = new List<CharacterRaw>();
        var failures = new List<WikiPageFailure>();
        var snapshots = 0;

        using (var scrapper = new WikiScrapper())
        using (var capture = await ConsoleCapture.StartAsync(cancellationToken))
        {
            progress?.Report(new WikiProgress(0, 0, "Category:Characters", "Listing pages"));
            _logger.LogInformation("Fetching character listing pages");

            List<CharacterListingPage> pages;
            try
            {
                pages = await scrapper.GetCharacterListingPages();
            }
            catch (Exception e)
            {
                var snapshot = await ScrapeDiagnostics.CaptureAsync(WikiScrapper.CategoryUrl, WikiScrapper.ListingSelectors, _options.DiagnosticsDirectory, cancellationToken);
                throw new ScrapeException($"WikiScrapper.GetCharacterListingPages failed ({e.GetType().Name}: {e.Message})", e, snapshot, capture.Take());
            }

            var links = WikiScrapper.GetCharacterLinks(pages);

            _logger.LogInformation("Found {count} character pages on {pages} listing pages", links.Count, pages.Count);

            if (links.Count == 0)
            {
                var snapshot = await ScrapeDiagnostics.CaptureAsync(WikiScrapper.CategoryUrl, WikiScrapper.ListingSelectors, _options.DiagnosticsDirectory, cancellationToken);
                throw new ScrapeException("The wiki category listing yielded no character pages; stored characters were left untouched", null, snapshot, capture.Take());
            }

            for (var i = 0; i < links.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WikiProgress(i, links.Count, links[i], "Characters"));
                capture.Take();

                string? error = null;
                try
                {
                    var character = await scrapper.GetCharacter(links[i]);
                    if (character != null)
                        characters.Add(character);
                    else
                        error = "No infobox found. " + capture.Take().Trim();
                }
                catch (Exception e)
                {
                    error = e.ToString();
                }

                if (error == null)
                    continue;

                if (snapshots++ < MaxSnapshots)
                    error += "\n\n" + await ScrapeDiagnostics.CaptureAsync(links[i], WikiScrapper.CharacterSelectors, _options.DiagnosticsDirectory, cancellationToken);

                failures.Add(new WikiPageFailure(links[i], error));
                _logger.LogWarning("Failed parsing {url}: {error}", links[i], error.Split('\n')[0]);
            }

            progress?.Report(new WikiProgress(links.Count, links.Count, "", "Saving"));
        }

        if (characters.Count == 0)
            throw new ScrapeException($"No character page could be parsed ({failures.Count} failures); stored characters were left untouched. First failure: {failures.FirstOrDefault()?.Url}",
                null, null, failures.FirstOrDefault()?.Error);

        await ReplaceCharactersAsync(characters, cancellationToken);
        _logger.LogInformation("Wiki download done: {count} characters, {failed} failed", characters.Count, failures.Count);
        return new WikiDownloadResult(characters.Count, failures.Count, failures);
    }

    /// <summary>Replaces all stored characters, keeping the Excluded flag of characters that already existed.</summary>
    public async Task ReplaceCharactersAsync(IEnumerable<CharacterRaw> characters, CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var existingExcluded = await db.Characters.Where(x => x.Excluded).Select(x => x.WikiUrl).ToListAsync(cancellationToken);
        var excludedSet = existingExcluded.ToHashSet();

        await db.Characters.ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        // The wiki can list the same page twice; first one wins.
        var order = 0;
        foreach (var character in characters.DistinctBy(x => x.WikiUrl))
        {
            var stored = StoredCharacter.From(character, order++, now);
            if (excludedSet.Contains(stored.WikiUrl))
                stored.Excluded = true;
            db.Characters.Add(stored);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<CharacterRaw>> GetIncludedCharactersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        var stored = await db.Characters.Where(x => !x.Excluded).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        return stored.Select(x => x.ToCharacter()).ToList();
    }

    public async Task SetExcludedAsync(string wikiUrl, bool excluded)
    {
        await using var db = _dbFactory.CreateDbContext();
        var row = await db.Characters.SingleAsync(x => x.WikiUrl == wikiUrl);
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
