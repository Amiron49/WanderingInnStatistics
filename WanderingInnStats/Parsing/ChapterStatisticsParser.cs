using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Core;

namespace WanderingInnStats.Parsing
{
    public class ChapterStatisticsParser
    {
        private readonly ILogger _logger;
        private readonly WanderingInnDefinitions _definitions;

        private readonly ITextParser[] _parsers;

        public ChapterStatisticsParser(ILogger logger, WanderingInnDefinitions definitions)
        {
            _logger = logger;
            _definitions = definitions;

            _parsers = new ITextParser[]
            {
                new BracketsParser(_logger),
                new SimpleWordStats(),
                new CharacterStats(),
                new EntityMentionStats()
            };
        }

        public async Task<List<(Chapter Chapter, IWanderingInnStatistics Statistics)>> Create(IEnumerable<Chapter> chapters)
        {
            var chaptersAsAsync = chapters.Select(async chapter =>
            {
                _logger.LogInformation("Parsing: {chapter}", chapter.Name);
                var statistics = await Create(chapter);
                return (chapter, statistics);
            });

            var tasks = chaptersAsAsync.ToList();
            try
            {
                return (await Task.WhenAll(tasks)).ToList();
            }
            catch
            {
                // Task.WhenAll rethrows only the first failure; report every chapter that failed.
                var failures = tasks.Where(x => x.IsFaulted).SelectMany(x => x.Exception!.InnerExceptions).ToList();
                if (failures.Count <= 1)
                    throw;
                throw new AggregateException(
                    $"{failures.Count} chapters failed to parse: {string.Join(", ", failures.OfType<ChapterParseException>().Select(x => $"'{x.ChapterName}'"))}",
                    failures);
            }
        }

        public Task<IWanderingInnStatistics> Create(Chapter chapter)
        {
            return Task.Run(() =>
            {
                var stats = new WanderingInnStatistics();

                foreach (var parser in _parsers)
                {
                    try
                    {
                        parser.Parse(chapter.Text, stats, _definitions);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        throw new ChapterParseException(chapter, parser.GetType().Name, e);
                    }
                }

                return (IWanderingInnStatistics)stats;
            });
        }
    }

    public static class AsyncHelper
    {
        public static async Task<List<T>> ToListAsyncInParallel<T>(this IAsyncEnumerable<T> enumerable, int maxConcurrent = 24)
        {
            var result = new ConcurrentBag<T>();

            await enumerable.AsyncParallelForEach(async arg =>
            {
                await Task.Run(() => result.Add(arg));
            }, maxConcurrent);

            return result.ToList();
        }

        public static async Task ForEachInParallel<T>(this IAsyncEnumerable<T> enumerable, Func<T, Task> action, int maxConcurrent = 24)
        {
            await enumerable.AsyncParallelForEach(async arg => { await action(arg); }, maxConcurrent);
        }

        private static async Task AsyncParallelForEach<T>(this IAsyncEnumerable<T> source, Func<T, Task> body,
            int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
        {
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            var block = new ActionBlock<T>(body, options);
            await foreach (var item in source)
                block.Post(item);
            block.Complete();
            await block.Completion;
        }
    }

    /// <summary>A parser failed on one chapter; names the chapter and parser so the cause can be found.</summary>
    public class ChapterParseException : Exception
    {
        public ChapterParseException(Chapter chapter, string parser, Exception inner)
            : base($"Parsing chapter '{chapter.Name}' ({chapter.Volume.Trim()}) failed in {parser}: {inner.Message}", inner)
        {
            ChapterName = chapter.Name;
            ChapterUrl = chapter.Url;
            Parser = parser;
        }

        public string ChapterName { get; }
        public string ChapterUrl { get; }
        public string Parser { get; }
    }
}
