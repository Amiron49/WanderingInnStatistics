using Serilog;
using Serilog.Events;
using WanderingInnStats.App.Services;
using WanderingInnStats.Pipeline;

namespace WanderingInnStats.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        var options = SettingsStore.Load();
        SettingsStore.LoadScrapeDelay();
        var logBuffer = new LogBuffer();

        // Parser debug output goes to the file only; the UI buffer filters on its own MinimumLevel.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(logBuffer)
            .WriteTo.File(Path.Combine(FileSystem.AppDataDirectory, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Information)
            .CreateLogger();

        builder.Logging.AddSerilog(dispose: true);

        builder.Services.AddSingleton(logBuffer);
        builder.Services.AddSingleton<JobRunner>();
        builder.Services.AddWanderingInnPipeline(options);

        var app = builder.Build();

        var bridge = app.Services.GetRequiredService<WanderingInnStats.Scrapping.BrowserBridge>();
        _ = Task.Run(async () =>
        {
            try
            {
                await SettingsStore.StartBridgeAsync(bridge);
            }
            catch (Exception e)
            {
                Log.Error(e, "Starting the browser bridge failed");
            }
        });

        return app;
    }
}
