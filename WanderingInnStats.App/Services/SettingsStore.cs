using WanderingInnStats.Pipeline;

namespace WanderingInnStats.App.Services;

/// <summary>Persists <see cref="WorkspaceOptions"/> in MAUI Preferences. The options instance itself is a singleton and mutated in place.</summary>
public static class SettingsStore
{
    private const string RootKey = "workspace.root";
    private const string ExportKey = "workspace.export";
    private const string ViewerKey = "workspace.viewer";
    private const string CopyKey = "workspace.copyToViewer";

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

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
