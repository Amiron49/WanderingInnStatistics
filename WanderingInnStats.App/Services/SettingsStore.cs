using WanderingInnStats.Pipeline;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.App.Services;

/// <summary>Persists <see cref="WorkspaceOptions"/> in MAUI Preferences. The options instance itself is a singleton and mutated in place.</summary>
public static class SettingsStore
{
    private const string RootKey = "workspace.root";
    private const string ExportKey = "workspace.export";
    private const string ViewerKey = "workspace.viewer";
    private const string CopyKey = "workspace.copyToViewer";
    private const string ScrapeDelayKey = "scrape.delayMs";

    public static WorkspaceOptions Load()
    {
        var options = new WorkspaceOptions();
        options.Root = Preferences.Default.Get(RootKey, options.Root);
        options.ExportDirectory = NullIfEmpty(Preferences.Default.Get(ExportKey, ""));
        options.ViewerDataDirectory = NullIfEmpty(Preferences.Default.Get(ViewerKey, ""));
        options.CopyToViewer = Preferences.Default.Get(CopyKey, false);
        return options;
    }

    public static void Save(WorkspaceOptions options)
    {
        Preferences.Default.Set(RootKey, options.Root);
        Preferences.Default.Set(ExportKey, options.ExportDirectory ?? "");
        Preferences.Default.Set(ViewerKey, options.ViewerDataDirectory ?? "");
        Preferences.Default.Set(CopyKey, options.CopyToViewer);
    }

    /// <summary>Applies the persisted delay to the global <see cref="ScrapeGate"/>.</summary>
    public static void LoadScrapeDelay()
    {
        ScrapeGate.Delay = TimeSpan.FromMilliseconds(Preferences.Default.Get(ScrapeDelayKey, (int)ScrapeGate.DefaultDelay.TotalMilliseconds));
    }

    public static void SaveScrapeDelay()
    {
        Preferences.Default.Set(ScrapeDelayKey, (int)ScrapeGate.Delay.TotalMilliseconds);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
