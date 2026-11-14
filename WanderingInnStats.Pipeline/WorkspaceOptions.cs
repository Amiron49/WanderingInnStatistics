namespace WanderingInnStats.Pipeline;

public class WorkspaceOptions
{
    /// <summary>
    /// Root of the working directory. The scrapping assembly hard-codes its wiki HTML cache to
    /// <c>W:/temp/wanderingInn/cache.sqlite</c>, so that directory must exist regardless of this value.
    /// </summary>
    public string Root { get; set; } = @"W:\temp\wanderingInn";

    /// <summary>Where the frontend jsons are written. Defaults to <see cref="Root"/>.</summary>
    public string? ExportDirectory { get; set; }

    /// <summary>Optional: the viewer's src/app folder. Exports are copied there when <see cref="CopyToViewer"/> is on.</summary>
    public string? ViewerDataDirectory { get; set; }

    public bool CopyToViewer { get; set; }

    public const string ScrapperWikiCacheDirectory = @"W:\temp\wanderingInn";
    public const string ScrapperWikiCacheFile = @"W:\temp\wanderingInn\cache.sqlite";

    public string DatabaseFile => Path.Combine(Root, "workspace.sqlite");
    public string EffectiveExportDirectory => string.IsNullOrWhiteSpace(ExportDirectory) ? Root : ExportDirectory!;
    public string StatisticsExportFile => Path.Combine(EffectiveExportDirectory, "statistics-frontend.json");
    public string DefinitionsExportFile => Path.Combine(EffectiveExportDirectory, "definitions-frontend.json");
    public string LogFile => Path.Combine(Root, "log.log");

    /// <summary>HTML snapshots of pages the scrapper failed on.</summary>
    public string DiagnosticsDirectory => Path.Combine(Root, "diagnostics");

    public string LegacyChapterDump => Path.Combine(Root, "dump.json");
    public string LegacyCharacterDump => Path.Combine(Root, "characters-raw.json");
    public string LegacyManualOverrides => Path.Combine(Root, "ManualOverrides");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(EffectiveExportDirectory);
        Directory.CreateDirectory(ScrapperWikiCacheDirectory);
    }
}
