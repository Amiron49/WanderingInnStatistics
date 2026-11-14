using Microsoft.Extensions.DependencyInjection;
using WanderingInnStats.Pipeline.Data;
using WanderingInnStats.Pipeline.Services;
using WanderingInnStats.Scrapping;

namespace WanderingInnStats.Pipeline;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWanderingInnPipeline(this IServiceCollection services, WorkspaceOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<WorkspaceDbFactory>();
        // Started by the host (the App); stays disconnected otherwise.
        services.AddSingleton<BrowserBridge>();
        services.AddSingleton<ChapterService>();
        services.AddSingleton<WikiService>();
        services.AddSingleton<StatisticsPipeline>();
        services.AddSingleton<ChapterPreviewService>();
        services.AddSingleton<LegacyImporter>();
        return services;
    }
}
