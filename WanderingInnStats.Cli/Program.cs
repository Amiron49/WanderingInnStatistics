using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using WanderingInnStats.Pipeline;
using WanderingInnStats.Pipeline.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WanderingInnStats.Cli
{
    /// <summary>
    /// Headless entry point. Usage: WanderingInnStats.Cli [--root &lt;dir&gt;] [--download-missing] [--wiki [--force]] [--no-stats]
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var options = new WorkspaceOptions();
            var rootIndex = Array.IndexOf(args, "--root");
            if (rootIndex >= 0 && rootIndex + 1 < args.Length)
                options.Root = args[rootIndex + 1];

            options.EnsureDirectories();
            ConfigureSerilog(options);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var services = new ServiceCollection()
                .AddLogging(builder => builder.AddSerilog(dispose: false))
                .AddWanderingInnPipeline(options)
                .BuildServiceProvider();

            try
            {
                var importer = services.GetRequiredService<LegacyImporter>();
                if (await importer.IsWorkspaceEmptyAsync(cts.Token) && importer.HasLegacyData)
                {
                    Log.Information("Workspace is empty, importing legacy caches");
                    await importer.ImportAsync(cts.Token);
                }

                if (args.Contains("--download-missing"))
                {
                    var chapterService = services.GetRequiredService<ChapterService>();
                    var diff = await chapterService.GetTocDiffAsync(cts.Token);
                    await chapterService.ApplyTocAsync(diff, cts.Token);
                    var missing = await chapterService.GetNotDownloadedAsync(cts.Token);
                    Log.Information("Downloading {count} chapters", missing.Count);
                    var result = await chapterService.DownloadAsync(missing, null, cts.Token);
                    Log.Information("Downloaded {ok}, failed {failed}", result.Succeeded, result.Failed);
                }

                if (args.Contains("--wiki"))
                {
                    var wiki = services.GetRequiredService<WikiService>();
                    var wikiResult = await wiki.DownloadAllAsync(forceRefresh: args.Contains("--force"), null, cts.Token);
                    Log.Information("Wiki: {summary}", wikiResult.Summary);
                }

                if (!args.Contains("--no-stats"))
                {
                    var pipeline = services.GetRequiredService<StatisticsPipeline>();
                    ILogger parserLogger = new SerilogLoggerFactory().CreateLogger("Lel");
                    var progress = new Progress<PipelineStage>(stage => Log.Information("Stage: {stage}", stage));
                    var result = await pipeline.RunAsync(parserLogger, progress, cts.Token);
                    Log.Information("Done. Unknowns: {unknown}. Wrote {stats} and {defs}", result.UnknownCount, result.StatisticsFile, result.DefinitionsFile);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Cancelled");
                return 2;
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Failed");
                return 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static void ConfigureSerilog(WorkspaceOptions options)
        {
            if (File.Exists(options.LogFile))
                File.Delete(options.LogFile);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(options.LogFile)
                .CreateLogger();
        }
    }
}
