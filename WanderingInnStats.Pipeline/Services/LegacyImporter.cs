using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Core;
using WanderingInnStats.Pipeline.Data;

namespace WanderingInnStats.Pipeline.Services;

public record LegacyImportResult(int Chapters, int Overrides, int Characters, IReadOnlyList<string> Warnings);

/// <summary>Imports the old CLI caches: dump.json, ManualOverrides/* and characters-raw.json.</summary>
public class LegacyImporter
{
    private readonly WorkspaceDbFactory _dbFactory;
    private readonly WorkspaceOptions _options;
    private readonly WikiService _wikiService;
    private readonly ILogger<LegacyImporter> _logger;

    public LegacyImporter(WorkspaceDbFactory dbFactory, WorkspaceOptions options, WikiService wikiService, ILogger<LegacyImporter> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _wikiService = wikiService;
        _logger = logger;
    }

    public bool HasLegacyData => File.Exists(_options.LegacyChapterDump) || File.Exists(_options.LegacyCharacterDump);

    public async Task<bool> IsWorkspaceEmptyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _dbFactory.CreateDbContext();
        return !await db.Chapters.AnyAsync(cancellationToken) && !await db.Characters.AnyAsync(cancellationToken);
    }

    /// <summary>Upserts chapters by url (overwriting raw text and overrides) and replaces all characters.</summary>
    public async Task<LegacyImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        int chapterCount = 0, overrideCount = 0, characterCount = 0;

        if (File.Exists(_options.LegacyChapterDump))
        {
            List<Chapter> chapters;
            await using (var file = File.OpenRead(_options.LegacyChapterDump))
                chapters = (await JsonSerializer.DeserializeAsync<List<Chapter>>(file, cancellationToken: cancellationToken))!;

            await using var db = _dbFactory.CreateDbContext();
            var existing = await db.Chapters.ToDictionaryAsync(x => x.Url, cancellationToken);
            var seen = new HashSet<string>();

            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                if (!seen.Add(chapter.Url))
                {
                    warnings.Add($"Duplicate url in dump.json, skipped: {chapter.Url}");
                    continue;
                }

                if (!existing.TryGetValue(chapter.Url, out var row))
                {
                    row = new StoredChapter { Url = chapter.Url, Excluded = ExclusionRules.IsChapterExcludedByDefault(chapter.Name) };
                    db.Chapters.Add(row);
                    existing[chapter.Url] = row;
                }

                row.Volume = chapter.Volume;
                row.Name = chapter.Name;
                row.TocOrder = i;
                row.Date = chapter.Date;
                row.Comments = chapter.Comments;
                row.RawText = chapter.Text;
                row.DownloadedAt ??= File.GetLastWriteTimeUtc(_options.LegacyChapterDump);
                row.LastError = null;
                chapterCount++;
            }

            if (Directory.Exists(_options.LegacyManualOverrides))
            {
                foreach (var overrideFile in Directory.GetFiles(_options.LegacyManualOverrides))
                {
                    using var reader = new StreamReader(overrideFile);
                    var url = await reader.ReadLineAsync(cancellationToken);
                    var text = await reader.ReadToEndAsync(cancellationToken);

                    if (url == null || !existing.TryGetValue(url, out var row))
                    {
                        warnings.Add($"Override {Path.GetFileName(overrideFile)} matches no chapter ({url})");
                        continue;
                    }

                    row.OverrideText = text;
                    row.OverrideStale = false;
                    overrideCount++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            warnings.Add($"{_options.LegacyChapterDump} not found");
        }

        if (File.Exists(_options.LegacyCharacterDump))
        {
            List<CharacterRaw> characters;
            await using (var file = File.OpenRead(_options.LegacyCharacterDump))
                characters = (await JsonSerializer.DeserializeAsync<List<CharacterRaw>>(file, cancellationToken: cancellationToken))!;

            await _wikiService.ReplaceCharactersAsync(characters, cancellationToken);
            characterCount = characters.Count;
        }
        else
        {
            warnings.Add($"{_options.LegacyCharacterDump} not found");
        }

        foreach (var warning in warnings)
            _logger.LogWarning("{warning}", warning);
        _logger.LogInformation("Legacy import: {chapters} chapters, {overrides} overrides, {characters} characters",
            chapterCount, overrideCount, characterCount);

        return new LegacyImportResult(chapterCount, overrideCount, characterCount, warnings);
    }
}
