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
    private const string BridgePortKey = "bridge.port";
    private const string BridgeFallbackKey = "bridge.autoFallback";
    private const string BridgeTokenKey = "bridge.token";

    public static int BridgePort
    {
        get => Preferences.Default.Get(BridgePortKey, BrowserBridge.DefaultPort);
        set => Preferences.Default.Set(BridgePortKey, value);
    }

    /// <summary>Retry Patreon-locked chapters through the browser during normal downloads.</summary>
    public static bool BrowserFallback
    {
        get => Preferences.Default.Get(BridgeFallbackKey, false);
        set => Preferences.Default.Set(BridgeFallbackKey, value);
    }

    /// <summary>The pairing token the extension must present; created on first use.</summary>
    public static async Task<string> GetOrCreateBridgeTokenAsync()
    {
        var token = await ReadSecretAsync(BridgeTokenKey);
        if (!string.IsNullOrEmpty(token))
            return token;
        return await RegenerateBridgeTokenAsync();
    }

    public static async Task<string> RegenerateBridgeTokenAsync()
    {
        var token = BrowserBridge.GenerateToken();
        await WriteSecretAsync(BridgeTokenKey, token);
        return token;
    }

    // SecureStorage can be unavailable for unpackaged Windows apps on some setups; fall back to Preferences then.
    private static async Task<string?> ReadSecretAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return NullIfEmpty(Preferences.Default.Get(key, ""));
        }
    }

    private static async Task WriteSecretAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            Preferences.Default.Set(key, value);
        }
    }

    /// <summary>Starts <paramref name="bridge"/> with the persisted port and token.</summary>
    public static async Task StartBridgeAsync(BrowserBridge bridge)
    {
        var token = await GetOrCreateBridgeTokenAsync();
        bridge.Start(BridgePort, token);
        if (bridge.LastError != null)
            Serilog.Log.Warning("Browser bridge: {error}", bridge.LastError);
    }

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
